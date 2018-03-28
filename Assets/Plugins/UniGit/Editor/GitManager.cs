using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitManager : IDisposable
	{
		public const string Version = "1.4.1";

		private readonly string repoPath;
		private readonly string settingsPath;

		private Repository repository;
		private readonly GitSettingsJson gitSettings;
		private readonly Queue<Action> actionQueue = new Queue<Action>();	//queue for executing actions on main thread
		private UniGitData gitData;
		private readonly object statusRetriveLock = new object();
		private bool repositoryDirty;	//is the whole repository dirty
		private bool forceSingleThread;	//force single threaded update
		private bool reloadDirty;	//should the GitLib2Sharp repository be recreated with a new instance
		private bool isUpdating;
		private bool inSubModule;
		private string dotGitDirCached;
		private readonly List<AsyncStageOperation> asyncStages = new List<AsyncStageOperation>();
		private readonly HashSet<string> updatingFiles = new HashSet<string>();		//currently updating files, mainly for multi threaded update
		private readonly GitCallbacks callbacks;
		private readonly IGitPrefs prefs;
		private readonly List<ISettingsAffector> settingsAffectors = new List<ISettingsAffector>();
		private readonly GitAsyncManager asyncManager;
		private readonly List<IGitWatcher> watchers = new List<IGitWatcher>();
		private readonly ILogger logger;
		private readonly GitInitializer initializer;

		[UniGitInject]
		public GitManager(string repoPath,
			string settingsPath,
			GitCallbacks callbacks, 
			GitSettingsJson settings, 
			IGitPrefs prefs, 
			GitAsyncManager asyncManager,
			UniGitData gitData,
			ILogger logger,
			GitInitializer initializer)
		{
			this.settingsPath = settingsPath;
			this.gitData = gitData;
			this.repoPath = repoPath;
			this.callbacks = callbacks;
			this.prefs = prefs;
			this.asyncManager = asyncManager;
			this.logger = logger;
			this.initializer = initializer;
			gitSettings = settings;

			Initialize();
		}

		private void Initialize()
		{
			callbacks.EditorUpdate += OnEditorUpdate;
			callbacks.DelayCall += OnDelayedCall;
			//asset postprocessing
			callbacks.OnWillSaveAssets += OnWillSaveAssets;
			callbacks.OnPostprocessImportedAssets += OnPostprocessImportedAssets;
			callbacks.OnPostprocessDeletedAssets += OnPostprocessDeletedAssets;
			callbacks.OnPostprocessMovedAssets += OnPostprocessMovedAssets;
			callbacks.OnPlayModeStateChange += OnPlayModeStateChange;
			callbacks.RepositoryCreate += OnRepositoryCreate;
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

		private void OnRepositoryCreate()
		{
			Update(true);
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
			if (initializer.IsValidRepo)
			{
				StartUpdating(paths);

				if (reloadRepository || repository == null)
				{
					if (repository != null) repository.Dispose();
					repository = CreateRepository(gitSettings.ActiveSubModule);
					callbacks.IssueOnRepositoryLoad(repository);
				}

				if (!forceSingleThread && Threading.IsFlagSet(GitSettingsJson.ThreadingType.Status)) RetreiveStatusThreaded(paths);
				else RetreiveStatus(paths);
			}
		}

		#region Asset Postprocessing

		private void OnWillSaveAssets(string[] projectPaths,ref string[] outputs)
		{
			PostprocessStage(projectPaths);
		}

		private void OnPostprocessImportedAssets(string[] projectPaths)
		{
			PostprocessStage(projectPaths);
		}

		private void OnPostprocessDeletedAssets(string[] projectPaths)
		{
			//automatic deletion is necessary even if AutoStage is off
			PostprocessUnstage(projectPaths);
		}

		private void OnPostprocessMovedAssets(string[] projectPaths,string[] movedFromProjectPaths)
		{
			PostprocessStage(projectPaths);
			//automatic deletion of previously moved asset is necessary even if AutoStage is off
			PostprocessUnstage(movedFromProjectPaths);
		}

		private void PostprocessStage(string[] projectPaths)
		{
			if(repository == null || !initializer.IsValidRepo) return;
			if (prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			string[] pathsFinal = projectPaths.Where(a => !IsEmptyFolder(a)).Select(ToLocalPath).SelectMany(GetPathWithMeta).ToArray();
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
						MarkDirtyAuto(pathsFinal);
					}
				}
				else
				{
					if (autoStage) GitCommands.Stage(repository, pathsFinal);
					MarkDirtyAuto(pathsFinal);
				}
			}
		}

		private void PostprocessUnstage(string[] projectPaths)
		{
			if (repository == null || !initializer.IsValidRepo) return;
			if (prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			string[] pathsFinal = projectPaths.Select(ToLocalPath).SelectMany(GetPathWithMeta).ToArray();
			if (pathsFinal.Length > 0)
			{
				if (gitSettings != null && Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
				{
					AsyncUnstage(pathsFinal);
				}
				else
				{
					GitCommands.Unstage(repository, pathsFinal);
					MarkDirtyAuto(pathsFinal);
				}
			}
		}

		#endregion

		private void CheckNullRepository()
		{
			if (repository == null && initializer.IsValidRepo)
			{
				repository = CreateRepository(gitSettings.ActiveSubModule);
				callbacks.IssueOnRepositoryLoad(repository);
			}
		}

		private Repository CreateRepository(string activeModule)
		{
			var mainRepository = new Repository(repoPath);

			if (!string.IsNullOrEmpty(activeModule))
			{
				var subModule = mainRepository.Submodules[activeModule];
				if (subModule != null && Repository.IsValid(subModule.Path))
				{
					var subModuleRepo = new Repository(subModule.Path);
					mainRepository.Dispose();
					inSubModule = true;
					dotGitDirCached = subModuleRepo.Info.Path;
					return subModuleRepo;
				}
			}
			inSubModule = false;
			dotGitDirCached = mainRepository.Info.Path;
			return mainRepository;
		}

		public void SwitchToSubModule(string path)
		{
			if (gitSettings.ActiveSubModule != path && Repository.IsValid(path))
			{
				gitSettings.ActiveSubModule = path;
				gitSettings.MarkDirty();
				MarkDirty(true);
			}
		}

		public void SwitchToMainRepository()
		{
			gitSettings.ActiveSubModule = null;
			gitSettings.MarkDirty();
			MarkDirty(true);
		}

		public void MarkDirtyAuto(params string[] localPaths)
		{
			if(gitSettings.LazyMode) MarkDirty(localPaths);
			else MarkDirty();
		}

		public void MarkDirty(bool reloadRepo)
		{
			repositoryDirty = true;
			reloadDirty = reloadRepo;
		}

		public void MarkDirty(params string[] paths)
		{
			if (paths.Length <= 0)
			{
				repositoryDirty = true;
			}
			else
			{
				MarkDirty((IEnumerable<string>)paths);
			}
		}

		public void MarkDirty(IEnumerable<string> paths)
		{
			foreach (var path in paths)
			{
				string fixedPath = FixUnityPath(path);
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
					foreach (var submodule in repository.Submodules)
					{
						var e = new GitStatusSubModuleEntry(submodule);
						gitData.RepositoryStatus.Add(e);
						gitData.RepositoryStatus.Update(e.Path,repository.RetrieveStatus(e.Path));
					}

					foreach (var remote in repository.Network.Remotes)
					{
						var e = new GitStatusRemoteEntry(remote);
						gitData.RepositoryStatus.Add(e);
					}
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
				DetectRenamesInIndex = gitSettings.DetectRenames.HasFlag(GitSettingsJson.RenameTypeEnum.RenameInIndex),
				DetectRenamesInWorkDir = gitSettings.DetectRenames.HasFlag(GitSettingsJson.RenameTypeEnum.RenameInWorkDir),
				//this might help with locked ignored files hanging the search
				RecurseIgnoredDirs = false,
				ExcludeSubmodules = true,
				DisablePathSpecMatch = true
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

		internal bool IsFileStaging(string localPath)
		{
			return asyncStages.Any(s => s.LocalPaths.Contains(localPath));
		}

		public Texture2D GetGitStatusIcon()
		{
			if (!initializer.IsValidRepo) return GitGUI.Textures.CollabNew;
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
				callbacks.RepositoryCreate -= OnRepositoryCreate;
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
		public void ShowDiff(string localPath, [NotNull] Commit oldCommit,[NotNull] Commit newCommit,GitExternalManager externalManager)
		{
			if (externalManager.TakeDiff(localPath, oldCommit, newCommit))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(localPath, oldCommit, newCommit);
		}

		public void ShowDiff(string localPath, GitExternalManager externalManager)
		{
			if (string.IsNullOrEmpty(localPath) ||  Repository == null) return;
			if (externalManager.TakeDiff(localPath))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(localPath);
		}

		public void ShowDiffPrev(string localPath, GitExternalManager externalManager)
		{
			if (string.IsNullOrEmpty(localPath) || Repository == null) return;
			var lastCommit = Repository.Commits.QueryBy(localPath).Skip(1).FirstOrDefault();
			if(lastCommit == null) return;
			if (externalManager.TakeDiff(localPath, lastCommit.Commit))
			{
				return;
			}

			var window = UniGitLoader.GetWindow<GitDiffInspector>();
			window.Init(localPath, lastCommit.Commit);
		}

		public void ShowBlameWizard(string localPath, GitExternalManager externalManager)
		{
			if (!string.IsNullOrEmpty(localPath))
			{
				if (externalManager.TakeBlame(localPath))
				{
					return;
				}

				var blameWizard = UniGitLoader.GetWindow<GitBlameWizard>(true);
				blameWizard.SetBlamePath(localPath);
			}
		}

		public bool CanBlame(FileStatus fileStatus)
		{
			return fileStatus.AreNotSet(FileStatus.NewInIndex, FileStatus.Ignored,FileStatus.NewInWorkdir);
		}

		public bool CanBlame(string localPath)
		{
			if (IsDirectory(localPath)) return false;
			return repository.Head[localPath] != null;
		}

		public void AutoStage(params string[] localPaths)
		{
			if (Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
			{
				AsyncStage(localPaths);
			}
			else
			{
				GitCommands.Stage(repository,localPaths);
				MarkDirtyAuto(localPaths);
			}
		}

		public GitAsyncOperation AsyncStage(string[] localPaths)
		{
			var operation = asyncManager.QueueWorker(() =>
			{
			    GitCommands.Stage(repository,localPaths);
			}, (o) =>
			{
				MarkDirtyAuto(localPaths);
				asyncStages.RemoveAll(s => s.Equals(o));
				callbacks.IssueAsyncStageOperationDone(o);
			});
			asyncStages.Add(new AsyncStageOperation(operation,localPaths));
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
				MarkDirtyAuto(paths);
			}
		}

		public GitAsyncOperation AsyncUnstage(string[] localPaths)
		{
			var operation = asyncManager.QueueWorker(() =>
			{
			    GitCommands.Unstage(repository,localPaths);
			}, (o) =>
			{
				MarkDirtyAuto(localPaths);
				asyncStages.RemoveAll(s => s.Equals(o));
				callbacks.IssueAsyncStageOperationDone(o);
			});
			asyncStages.Add(new AsyncStageOperation(operation, localPaths));
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

		public bool IsDirectory(string localPath)
		{
			string projectPath = ToProjectPath(localPath);
			if (IsSubModule(ToProjectPath(localPath)))
			{
				return false;
			}
			if (Path.IsPathRooted(projectPath))
			{
				return Directory.Exists(UniGitPath.Combine(repoPath,projectPath));
			}
			return Directory.Exists(projectPath);
		}

		public bool IsEmptyFolderMeta(string path)
		{
			if (IsMetaPath(path))
			{
				return IsEmptyFolder(path.Substring(0, path.Length - 5));
			}
			return false;
		}

		public bool IsEmptyFolder(string path)
		{
			if (Directory.Exists(path))
			{
				return Directory.GetFileSystemEntries(path).Length <= 0;
			}
			return false;
		}

		public bool IsSubModule(string projectPath)
		{
			return gitData.RepositoryStatus != null && gitData.RepositoryStatus.SubModuleEntries.Any(m => UniGitPath.Compare(m.Path,projectPath));
		}

		public string ToProjectPath(string localPath)
		{
			if (inSubModule) return UniGitPath.Combine(gitSettings.ActiveSubModule, localPath);
			return localPath;
		}

		public string ToLocalPath(string projectPath)
		{
			if (inSubModule) return projectPath.Replace(gitSettings.ActiveSubModule.Replace("\\","/") + "/","");
			return projectPath;
		}

		/// <summary>
		/// If in sub module returns it's repo path
		/// </summary>
		/// <returns></returns>
		public string GetCurrentRepoPath()
		{
			if (inSubModule) return UniGitPath.Combine(repoPath, gitSettings.ActiveSubModule);
			return repoPath;
		}

		public string GetCurrentDotGitFolder()
		{
			if (inSubModule) return dotGitDirCached;
			return UniGitPath.Combine(repoPath,".git");
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

		public static string FixUnityPath(string path)
		{
			return path.Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
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

		public void CheckoutProgressHandler(string path, int completedSteps, int totalSteps)
		{
			if (string.IsNullOrEmpty(path))
			{
				logger.Log(LogType.Log,"Nothing to checkout");
			}
			else
			{
				float percent = (float)completedSteps / totalSteps;
				EditorUtility.DisplayProgressBar("Transferring", string.Format("Checking Out: {0}",path),percent);
			}
		}

		public bool FetchTransferProgressHandler(TransferProgress progress)
		{
			float percent = (float)progress.ReceivedObjects / progress.TotalObjects;
			bool cancel = EditorUtility.DisplayCancelableProgressBar("Transferring", string.Format("Transferring: Received total of: {0} bytes. {1}%", progress.ReceivedBytes, (percent * 100).ToString("###")), percent);
			if (progress.TotalObjects == progress.ReceivedObjects)
			{
#if UNITY_EDITOR
				logger.Log("Transfer Complete. Received a total of " + progress.IndexedObjects + " objects");
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
			if (!initializer.IsValidRepo)
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

		public bool InSubModule
		{
			get { return inSubModule; }
		}

		public Signature Signature
		{
			get { return new Signature(Repository.Config.GetValueOrDefault<string>("user.name"), Repository.Config.GetValueOrDefault<string>("user.email"),DateTimeOffset.Now);}
		}

		public Repository Repository
		{
			get
			{
				CheckNullRepository();
				return repository; 
			}
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

		public string SettingsDirectory
		{
			get { return Path.GetDirectoryName(settingsPath); }
		}

		public Queue<Action> ActionQueue
		{
			get { return actionQueue; }
		}

		#endregion

		public class AsyncUpdateOperation : IEquatable<GitAsyncOperation>
		{
			private readonly GitAsyncOperation operation;
			private readonly string[] localPaths;

			public AsyncUpdateOperation(GitAsyncOperation operation, string[] localPaths)
			{
				this.operation = operation;
				this.localPaths = localPaths;
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

			public string[] LocalPaths
			{
				get { return localPaths; }
			}
		}

		public class AsyncStageOperation : IEquatable<GitAsyncOperation>
		{
			private readonly GitAsyncOperation operation;
			private readonly HashSet<string> localPaths;

			public AsyncStageOperation(GitAsyncOperation operation, IEnumerable<string> localPaths)
			{
				this.operation = operation;
				this.localPaths = new HashSet<string>(localPaths);
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

			public HashSet<string> LocalPaths
			{
				get { return localPaths; }
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