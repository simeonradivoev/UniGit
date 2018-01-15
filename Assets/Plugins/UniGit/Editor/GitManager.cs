using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitManager : IDisposable
	{
		public const string Version = "1.3";

		private readonly string repoPath;
		private readonly string gitPath;
		public string RepoPath { get { return repoPath; } }

		private Repository repository;
		private readonly GitSettingsJson gitSettings;
		private readonly Queue<Action> actionQueue = new Queue<Action>();	//queue for executing actions on main thread
		private UniGitData gitData;
		private readonly object statusRetriveLock = new object();
		private bool repositoryDirty;	//is the whole repository dirty
		private bool forceSingleThread;	//force single threaded update
		private bool reloadDirty;	//should the GitLib2Sharp repository be recreated with a new instance
		private bool isUpdating;
		private readonly List<AsyncStageOperation> asyncStages = new List<AsyncStageOperation>();
		private readonly HashSet<string> updatingFiles = new HashSet<string>();		//currently updating files, mainly for multi threaded update
		private readonly GitCallbacks callbacks;
		private readonly IGitPrefs prefs;
		private readonly List<ISettingsAffector> settingsAffectors = new List<ISettingsAffector>();
		private readonly GitAsyncManager asyncManager;
		private readonly List<IGitWatcher> watchers = new List<IGitWatcher>();
		private readonly ILogger logger;

		[UniGitInject]
		public GitManager(string repoPath, 
			GitCallbacks callbacks, 
			GitSettingsJson settings, 
			IGitPrefs prefs, 
			GitAsyncManager asyncManager,
			UniGitData gitData,
			ILogger logger)
		{
			this.gitData = gitData;
			this.repoPath = repoPath;
			this.callbacks = callbacks;
			this.prefs = prefs;
			this.asyncManager = asyncManager;
			this.logger = logger;
			gitSettings = settings;
			gitPath = UniGitPath.Combine(repoPath, ".git");

			Initialize();
		}

		private void Initialize()
		{
			if (!IsValidRepo)
			{
				return;
			}
			callbacks.EditorUpdate += OnEditorUpdate;
			callbacks.DelayCall += OnDelayedCall;
			//asset postprocessing
			callbacks.OnWillSaveAssets += OnWillSaveAssets;
			callbacks.OnPostprocessImportedAssets += OnPostprocessImportedAssets;
			callbacks.OnPostprocessDeletedAssets += OnPostprocessDeletedAssets;
			callbacks.OnPostprocessMovedAssets += OnPostprocessMovedAssets;
			callbacks.OnPlayModeStateChange += OnPlayModeStateChange;

			CheckNullRepository();
		}

		private void OnPlayModeStateChange(PlayModeStateChange stateChange)
		{
			if(gitSettings.LazyMode) return;
			if (stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.EnteredEditMode)
			{
				MarkDirty();
			}
		}

		private void OnDelayedCall()
		{
			if(gitSettings.LazyMode) return;
			MarkDirty();
		}

		public void InitializeRepository()
		{
			Repository.Init(repoPath);
			Directory.CreateDirectory(GitSettingsFolderPath);
			string newGitIgnoreFile = GitIgnoreFilePath;
			if (!File.Exists(newGitIgnoreFile))
			{
				File.WriteAllText(newGitIgnoreFile, GitIgnoreTemplate.Template);
			}
			else
			{
				logger.Log(LogType.Log,"Git Ignore file already present");
			}

			logger.Log(LogType.Log,"Repository Initialized");
			Initialize();
		}

		internal void InitializeRepositoryAndRecompile()
		{
			InitializeRepository();
			callbacks.IssueAssetDatabaseRefresh();
			callbacks.IssueSaveDatabaseRefresh();
			callbacks.IssueRepositoryCreate();
			Update(true);
		}

		internal static void Recompile()
		{
			var importer = PluginImporter.GetAllImporters().FirstOrDefault(i => i.assetPath.EndsWith("UniGitResources.dll"));
			if (importer == null)
			{
				Debug.LogError("Could not find LibGit2Sharp.dll. You will have to close and open Unity to recompile scripts.");
				return;
			}
			importer.SetCompatibleWithEditor(true);
			importer.SaveAndReimport();
		}

		public void DeleteRepository()
		{
			if(string.IsNullOrEmpty(repoPath)) return;
			DeleteDirectory(repoPath);
		}

		private void DeleteDirectory(string targetDir)
		{
			string[] files = Directory.GetFiles(targetDir);
			string[] dirs = Directory.GetDirectories(targetDir);

			foreach (string file in files)
			{
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}

			foreach (string dir in dirs)
			{
				DeleteDirectory(dir);
			}

			Directory.Delete(targetDir, false);
		}

		internal void OnEditorUpdate()
		{
			var updateStatus = GetUpdateStatus();
			if (updateStatus == UpdateStatusEnum.Ready)
			{
				CheckNullRepository();
				if (CanUpdate())
				{
					if (repositoryDirty || !gitData.Initialized)
					{
						Update(reloadDirty);
						reloadDirty = false;
						repositoryDirty = false;
						if(!gitData.Initialized) logger.Log(LogType.Log,"UniGitData Initialized");
						gitData.Initialized = true;
						gitData.DirtyFilesQueue.Clear();

					}
					else if (gitData.DirtyFilesQueue.Count > 0)
					{
						Update(reloadDirty || repository == null, gitData.DirtyFilesQueue.ToArray());
						gitData.DirtyFilesQueue.Clear();
					}
				}

			}

			if (actionQueue.Count > 0)
			{
				Action action = actionQueue.Dequeue();
				if (action != null)
				{
					try
					{
						action.Invoke();
					}
					catch (Exception e)
					{
						logger.LogException(e);
						throw;
					}
				}
			}

			watchers.RemoveAll(w => !w.IsValid);
		}

		private bool CanUpdate()
		{
			return !gitSettings.LazyMode || watchers.Any(watcher => watcher.IsValid && watcher.IsWatching);
		}

		private void Update(bool reloadRepository,string[] paths = null)
		{
			StartUpdating(paths);

			if (reloadRepository && IsValidRepo)
			{
				if (repository != null) repository.Dispose();
				repository = new Repository(RepoPath);
				callbacks.IssueOnRepositoryLoad(repository);
			}

			if (repository != null)
			{
				if (!forceSingleThread && Threading.IsFlagSet(GitSettingsJson.ThreadingType.Status)) RetreiveStatusThreaded(paths);
				else RetreiveStatus(paths);
			}
		}

		#region Asset Postprocessing

		private void OnWillSaveAssets(string[] paths,ref string[] outputs)
		{
			PostprocessStage(paths);
		}

		private void OnPostprocessImportedAssets(string[] paths)
		{
			PostprocessStage(paths);
		}

		private void OnPostprocessDeletedAssets(string[] paths)
		{
			//automatic deletion is necessary even if AutoStage is off
			PostprocessUnstage(paths);
		}

		private void OnPostprocessMovedAssets(string[] paths,string[] movedFrom)
		{
			PostprocessStage(paths);
			//automatic deletion of previously moved asset is necessary even if AutoStage is off
			PostprocessUnstage(movedFrom);
		}

		private void PostprocessStage(string[] paths)
		{
			if(repository == null || !IsValidRepo) return;
			if (prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			string[] pathsFinal = paths.Where(a => !IsEmptyFolder(a)).SelectMany(GetPathWithMeta).ToArray();
			if (pathsFinal.Length > 0)
			{
				bool autoStage = gitSettings != null && gitSettings.AutoStage;
				if (Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
				{
					if (autoStage)
					{
						AsyncStage(pathsFinal);
					}
					else
					{
						MarkDirty(pathsFinal);
					}
				}
				else
				{
					if (autoStage) GitCommands.Stage(repository, pathsFinal);
					MarkDirty(pathsFinal);
				}
			}
		}

		private void PostprocessUnstage(string[] paths)
		{
			if (repository == null || !IsValidRepo) return;
			if (prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			string[] pathsFinal = paths.SelectMany(GetPathWithMeta).ToArray();
			if (pathsFinal.Length > 0)
			{
				if (gitSettings != null && Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
				{
					AsyncUnstage(pathsFinal);
				}
				else
				{
					GitCommands.Unstage(repository, pathsFinal);
					MarkDirty(pathsFinal);
				}
			}
		}

		#endregion

		private void CheckNullRepository()
		{
			if (IsValidRepo && repository == null)
			{
				repository = new Repository(RepoPath);
				callbacks.IssueOnRepositoryLoad(repository);
			}
		}

		public void MarkDirty()
		{
			repositoryDirty = true;
		}

		public void MarkDirty(bool reloadRepo)
		{
			repositoryDirty = true;
			reloadDirty = reloadRepo;
		}

		public void MarkDirty(params string[] paths)
		{
			MarkDirty((IEnumerable<string>)paths);
		}

		public void MarkDirty(IEnumerable<string> paths)
		{
			foreach (var path in paths)
			{
				string fixedPath = path.Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
				if(IsDirectory(fixedPath)) continue;
				if(!gitData.DirtyFilesQueue.Contains(fixedPath))
					gitData.DirtyFilesQueue.Add(fixedPath);
			}
		}

		private void RebuildStatus(string[] paths)
		{
			gitData.RepositoryStatus.Lock();

			try
			{
				if (paths != null && paths.Length > 0)
				{
					foreach (string path in paths)
					{
						gitData.RepositoryStatus.Update(path, repository.RetrieveStatus(path));
					}
				}
				else
				{
					var options = GetStatusOptions();
					var s = repository.RetrieveStatus(options);
					gitData.RepositoryStatus.Clear();
					gitData.RepositoryStatus.Combine(s);
				}
			}
			finally
			{
				gitData.RepositoryStatus.Unlock();
			}
		}

		private StatusOptions GetStatusOptions()
		{
			return new StatusOptions()
			{
				DetectRenamesInIndex = gitSettings.DetectRenames,
				DetectRenamesInWorkDir = gitSettings.DetectRenames,
				//this might help with locked ignored files hanging the search
				RecurseIgnoredDirs = false
			};
		}

		private void RetreiveStatusThreaded(string[] paths)
		{
			asyncManager.QueueWorkerWithLock(() => { RetreiveStatus(paths, true); }, statusRetriveLock);
		}

		private void RetreiveStatus(string[] paths)
		{
			//reset force single thread as we are going to update on main thread
			forceSingleThread = false;
			GitProfilerProxy.BeginSample("UniGit Status Retrieval");
			try
			{
				RetreiveStatus(paths, false);
			}
			finally
			{
				GitProfilerProxy.EndSample();
			}
		}

		private void RetreiveStatus(string[] paths,bool threaded)
		{
			if (!threaded) GitProfilerProxy.BeginSample("Git Repository Status Retrieval");
			try
			{
				RebuildStatus(paths);
				FinishUpdating(threaded, paths);
			}
			catch (ThreadAbortException)
			{
				//run status retrieval on main thread if this thread was aborted
				actionQueue.Enqueue(() =>
				{
					RetreiveStatus(paths);
				});
				//handle thread abort gracefully
				Thread.ResetAbort();
				logger.Log(LogType.Warning,"Git status threaded retrieval aborted, executing on main thread.");
			}
			catch (Exception e)
			{
				//mark dirty if thread failed
				if (threaded) MarkDirty();
				FinishUpdating(threaded, paths);

				logger.Log(LogType.Error,"Could not retrive Git Status");
				logger.LogException(e);
			}
			finally
			{
				if(!threaded) GitProfilerProxy.EndSample();
			}
		}

		private void StartUpdating(IEnumerable<string> paths)
		{
			isUpdating = true;
			updatingFiles.Clear();
			if (paths != null)
			{
				foreach (var path in paths)
				{
					updatingFiles.Add(path);
				}
			}
			callbacks.IssueUpdateRepositoryStart();
		}

		private void FinishUpdating(bool treaded, string[] paths)
		{
			if (treaded)
			{
				actionQueue.Enqueue(() =>
				{
					FinishUpdating(paths);
				});
			}
			else
			{
				FinishUpdating(paths);
			}
		}

		private void FinishUpdating(string[] paths)
		{
			isUpdating = false;
			updatingFiles.Clear();
			callbacks.IssueUpdateRepository(gitData.RepositoryStatus, paths);
			foreach (var watcher in watchers)
			{
				if(!watcher.IsWatching) watcher.MarkDirty();
			}
		}

		internal bool IsFileDirty(string path)
		{
			if (gitData.DirtyFilesQueue.Count <= 0) return false;
			return gitData.DirtyFilesQueue.Contains(path);
		}

		internal bool IsFileUpdating(string path)
		{
			if (isUpdating)
			{
				if (updatingFiles.Count <= 0) return true;
				return updatingFiles.Contains(path);
			}
			return false;
		}

		internal bool IsFileStaging(string path)
		{
			return asyncStages.Any(s => s.Paths.Contains(path));
		}

		public Texture2D GetGitStatusIcon()
		{
			if (!IsValidRepo) return GitGUI.Textures.CollabNew;
			if (Repository == null) return GitGUI.Textures.Collab;
			if (isUpdating) return GitGUI.GetTempSpinAnimatedTexture();
			if (Repository.Index.Conflicts.Any()) return GitGUI.Textures.CollabConflict;
			int? behindBy = Repository.Head.TrackingDetails.BehindBy;
			int? aheadBy = Repository.Head.TrackingDetails.AheadBy;
			if (behindBy.GetValueOrDefault(0) > 0)
			{
				return GitGUI.Textures.CollabPull;
			}
			if (aheadBy.GetValueOrDefault(0) > 0)
			{
				return GitGUI.Textures.CollabPush;
			}
			return GitGUI.Textures.Collab;
		}

		public void Dispose()
		{
			if (repository != null)
			{
				repository.Dispose();
				repository = null;
			}
			if (callbacks != null)
			{
				callbacks.EditorUpdate -= OnEditorUpdate;
				//asset postprocessing
				callbacks.OnWillSaveAssets -= OnWillSaveAssets;
				callbacks.OnPostprocessImportedAssets -= OnPostprocessImportedAssets;
				callbacks.OnPostprocessDeletedAssets -= OnPostprocessDeletedAssets;
				callbacks.OnPostprocessMovedAssets -= OnPostprocessMovedAssets;
				callbacks.OnPlayModeStateChange -= OnPlayModeStateChange;
			}
		}

		#region Settings Affectors
		public void AddSettingsAffector(ISettingsAffector settingsAffector)
		{
			settingsAffectors.Add(settingsAffector);
		}

		public bool RemoveSettingsAffector(ISettingsAffector affector)
		{
			return settingsAffectors.Remove(affector);
		}

		public bool ContainsAffector(ISettingsAffector affector)
		{
			return settingsAffectors.Contains(affector);
		}
		#endregion

		#region Helpers
		public void ShowDiff(string path, [NotNull] Commit oldCommit,[NotNull] Commit newCommit,GitExternalManager externalManager)
		{
			if (externalManager.TakeDiff(path, oldCommit, newCommit))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(path, oldCommit, newCommit);
		}

		public void ShowDiff(string path, GitExternalManager externalManager)
		{
			if (string.IsNullOrEmpty(path) ||  Repository == null) return;
			if (externalManager.TakeDiff(path))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(path);
		}

		public void ShowDiffPrev(string path, GitExternalManager externalManager)
		{
			if (string.IsNullOrEmpty(path) || Repository == null) return;
			var lastCommit = Repository.Commits.QueryBy(path).Skip(1).FirstOrDefault();
			if(lastCommit == null) return;
			if (externalManager.TakeDiff(path, lastCommit.Commit))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(path, lastCommit.Commit);
		}

		public void ShowBlameWizard(string path, GitExternalManager externalManager)
		{
			if (!string.IsNullOrEmpty(path))
			{
				if (externalManager.TakeBlame(path))
				{
					return;
				}

				var blameWizard = UniGitLoader.GetWindow<GitBlameWizard>(true);
				blameWizard.SetBlamePath(path);
			}
		}

		public bool CanBlame(FileStatus fileStatus)
		{
			return fileStatus.AreNotSet(FileStatus.NewInIndex, FileStatus.Ignored,FileStatus.NewInWorkdir);
		}

		public bool CanBlame(string path)
		{
			if (IsDirectory(path)) return false;
			return repository.Head[path] != null;
		}

		public void AutoStage(params string[] paths)
		{
			if (Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
			{
				AsyncStage(paths);
			}
			else
			{
				GitCommands.Stage(repository,paths);
				MarkDirty(paths);
			}
		}

		public GitAsyncOperation AsyncStage(string[] paths)
		{
			var operation = asyncManager.QueueWorker(() =>
			{
			    GitCommands.Stage(repository,paths);
			}, (o) =>
			{
				MarkDirty(paths);
				asyncStages.RemoveAll(s => s.Equals(o));
				callbacks.IssueAsyncStageOperationDone(o);
			});
			asyncStages.Add(new AsyncStageOperation(operation,paths));
			return operation;
		}

		public void AutoUnstage(params string[] paths)
		{
			if (Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
			{
				AsyncUnstage(paths);
			}
			else
			{
			    GitCommands.Unstage(repository, paths);
				MarkDirty(paths);
			}
		}

		public GitAsyncOperation AsyncUnstage(string[] paths)
		{
			var operation = asyncManager.QueueWorker(() =>
			{
			    GitCommands.Unstage(repository,paths);
			}, (o) =>
			{
				MarkDirty(paths);
				asyncStages.RemoveAll(s => s.Equals(o));
				callbacks.IssueAsyncStageOperationDone(o);
			});
			asyncStages.Add(new AsyncStageOperation(operation, paths));
			return operation;
		}

		public void ExecuteAction(Action action, bool async)
		{
			if (async)
			{
				actionQueue.Enqueue(action);
			}
			else
			{
				action.Invoke();
			}
		}

		public void AddWatcher(IGitWatcher watcher)
		{
			if(watchers.Contains(watcher)) return;
			watchers.Add(watcher);
		}

		public bool RemoveWatcher(IGitWatcher watcher)
		{
			return watchers.Remove(watcher);
		}

		public bool IsDirectory(string path)
		{
			if (Path.IsPathRooted(path))
			{
				return Directory.Exists(UniGitPath.Combine(repoPath,path));
			}
			return Directory.Exists(path);
		}

		#region Enumeration helpers

		public IEnumerable<string> GetPathWithMeta(string path)
		{
			if (IsMetaPath(path))
			{
				string assetPath = AssetPathFromMeta(path);
				yield return path;
				//if the asset belonging to the meta file is a folder just return the meta
				if (IsDirectory(assetPath)) yield break;
				if (!string.IsNullOrEmpty(assetPath))
				{
					yield return assetPath;
				}
			}
			else
			{
				string metaPath = MetaPathFromAsset(path);
				//if the path is a directory then return only it's meta path
				if (IsDirectory(path))
				{
					yield return metaPath;
					yield break;
				}
				if (!string.IsNullOrEmpty(metaPath))
				{
					yield return path;
					yield return metaPath;
				}
			}
		}

		public IEnumerable<string> GetPathsWithMeta(IEnumerable<string> paths)
		{
			return paths.SelectMany(GetPathWithMeta);
		}

		public string GetRelativePath(string rootPath)
		{
			return rootPath.Replace(repoPath, "").TrimStart(UniGitPath.UnityDeirectorySeparatorChar,Path.DirectorySeparatorChar);
		}
		#endregion

		#endregion

		#region Static Helpers

		public static bool IsEmptyFolderMeta(string path)
		{
			if (IsMetaPath(path))
			{
				return IsEmptyFolder(path.Substring(0, path.Length - 5));
			}
			return false;
		}

		public static bool IsEmptyFolder(string path)
		{
			if (Directory.Exists(path))
			{
				return Directory.GetFileSystemEntries(path).Length <= 0;
			}
			return false;
		}

		public static string AssetPathFromMeta(string metaPath)
		{
			if (IsMetaPath(metaPath))
			{
				return metaPath.Substring(0, metaPath.Length - 5);
			}
			return metaPath;
		}

		public static string MetaPathFromAsset(string assetPath)
		{
			return assetPath + ".meta";
		}

		public static bool CanStage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.NewInWorkdir | FileStatus.RenamedInWorkdir | FileStatus.TypeChangeInWorkdir | FileStatus.DeletedFromWorkdir);
		}

		public static bool CanUnstage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInIndex | FileStatus.NewInIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex | FileStatus.DeletedFromIndex);
		}

		public static bool IsPathInAssetFolder(string path)
		{
			return path.StartsWith("Assets");
		}

		public static bool IsMetaPath(string path)
		{
			return path.EndsWith(".meta");
		}
		#endregion

		#region Repository Handlers Handlers
		public bool CheckoutNotifyHandler(string path, CheckoutNotifyFlags notifyFlags)
		{
			if (gitSettings.CreateFoldersForDriftingMeta)
			{
				if (IsMetaPath(path))
				{
					string assetPath = AssetPathFromMeta(path);
					string rootedAssetPath = Path.Combine(repoPath, assetPath);
					if (!Path.HasExtension(assetPath) && !File.Exists(rootedAssetPath) && !Directory.Exists(rootedAssetPath))
					{
						Directory.CreateDirectory(rootedAssetPath);
						logger.LogFormat(LogType.Log,"Folder '{0}' created for drifting '{1}' file.",assetPath,path);
					}
				}
			}
			return true;
		}

		public bool FetchTransferProgressHandler(TransferProgress progress)
		{
			float percent = (float)progress.ReceivedObjects / progress.TotalObjects;
			bool cancel = EditorUtility.DisplayCancelableProgressBar("Transferring", string.Format("Transferring: Received total of: {0} bytes. {1}%", progress.ReceivedBytes, (percent * 100).ToString("###")), percent);
			if (progress.TotalObjects == progress.ReceivedObjects)
			{
#if UNITY_EDITOR
				Debug.Log("Transfer Complete. Received a total of " + progress.IndexedObjects + " objects");
#endif
			}
			//true to continue
			return !cancel;
		}
		#endregion

		public void DisablePostprocessing()
		{
			prefs.SetBool(UnityEditorGitPrefs.DisablePostprocess, true);
		}

		public void EnablePostprocessing()
		{
			prefs.SetBool(UnityEditorGitPrefs.DisablePostprocess, false);
		}

		#region Getters and Setters

		public UpdateStatusEnum GetUpdateStatus()
		{
			if (!IsValidRepo)
			{
				return UpdateStatusEnum.InvalidRepo;
			}
			if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
			{
				return UpdateStatusEnum.SwitchingToPlayMode;
			}
			if (EditorApplication.isCompiling)
			{
				return UpdateStatusEnum.Compiling;
			}
			if (EditorApplication.isUpdating)
			{
				return UpdateStatusEnum.UpdatingAssetDatabase;
			}
			if (isUpdating)
			{
				return UpdateStatusEnum.Updating;
			}
			return UpdateStatusEnum.Ready;
		}

		public IGitPrefs Prefs
		{
			get { return prefs; }
		}

		public string GitSettingsFolderPath
		{
			get { return UniGitPath.Combine(gitPath, Path.Combine("UniGit", "Settings")); }
		}

		public string GitCommitMessageFilePath
		{
			get { return UniGitPath.Combine(gitPath, "UniGit","Settings", "CommitMessage.txt"); }
		}

		public string GitIgnoreFilePath
		{
			get { return UniGitPath.Combine(repoPath, ".gitignore"); }
		}

		public bool IsUpdating
		{
			get { return isUpdating; }
		}

		public bool IsAsyncStaging
		{
			get { return asyncStages.Count > 0; }
		}

		public bool IsDirty
		{
			get { return gitData.DirtyFilesQueue.Count > 0 || repositoryDirty; }
		}

		public Signature Signature
		{
			get { return new Signature(Repository.Config.GetValueOrDefault<string>("user.name"), Repository.Config.GetValueOrDefault<string>("user.email"),DateTimeOffset.Now);}
		}

		public  bool IsValidRepo
		{
			get { return Repository.IsValid(RepoPath); }
		}

		public Repository Repository
		{
			get { return repository; }
		}

		public GitSettingsJson.ThreadingType Threading
		{
			get
			{
				GitSettingsJson.ThreadingType newThreading = gitSettings.Threading;
				foreach (var affector in settingsAffectors)
				{
					affector.AffectThreading(ref newThreading);
				}
				return newThreading;
			}
		}

		public string GitFolderPath
		{
			get { return gitPath; }
		}

		public string SettingsFilePath
		{
			get { return UniGitPath.Combine(gitPath,"UniGit", "Settings.json"); }
		}

		public Queue<Action> ActionQueue
		{
			get { return actionQueue; }
		}

		#endregion

		public class AsyncUpdateOperation : IEquatable<GitAsyncOperation>
		{
			private readonly GitAsyncOperation operation;
			private readonly string[] paths;

			public AsyncUpdateOperation(GitAsyncOperation operation, string[] paths)
			{
				this.operation = operation;
				this.paths = paths;
			}

			public bool Equals(GitAsyncOperation other)
			{
				return operation.Equals(other);
			}

			public override bool Equals(object obj)
			{
				if (obj is GitAsyncOperation)
				{
					return operation.Equals(obj);
				}
				return ReferenceEquals(this,obj);
			}

			public override int GetHashCode()
			{
				return operation.GetHashCode();
			}

			public bool IsDone
			{
				get { return operation.IsDone; }
			}

			public string[] Paths
			{
				get { return paths; }
			}
		}

		public class AsyncStageOperation : IEquatable<GitAsyncOperation>
		{
			private readonly GitAsyncOperation operation;
			private readonly HashSet<string> paths;

			public AsyncStageOperation(GitAsyncOperation operation, IEnumerable<string> paths)
			{
				this.operation = operation;
				this.paths = new HashSet<string>(paths);
			}

			public bool Equals(GitAsyncOperation other)
			{
				return operation.Equals(other);
			}

			public override bool Equals(object obj)
			{
				if (obj is GitAsyncOperation)
				{
					return operation.Equals(obj);
				}
				return ReferenceEquals(this, obj);
			}

			public override int GetHashCode()
			{
				return operation.GetHashCode();
			}

			public HashSet<string> Paths
			{
				get { return paths; }
			}

			public GitAsyncOperation Operation
			{
				get { return operation; }
			}

			public bool IsDone
			{
				get { return operation.IsDone; }
			}
		}

		public enum UpdateStatusEnum
		{
			Ready,
			Other,
			InvalidRepo,
			SwitchingToPlayMode,
			Compiling,
			UpdatingAssetDatabase,
			Updating
		}
	}
}