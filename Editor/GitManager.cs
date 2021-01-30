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
		public const string Version = "1.4.3";

		private Repository repository;
		private readonly GitSettingsJson gitSettings;
        private readonly UniGitData gitData;
		private readonly object statusRetriveLock = new object();
		private bool repositoryDirty;	//is the whole repository dirty
		private bool forceSingleThread;	//force single threaded update
		private bool reloadDirty;	//should the GitLib2Sharp repository be recreated with a new instance
        private string dotGitDirCached;
		private readonly List<AsyncStageOperation> asyncStages = new List<AsyncStageOperation>();
		private readonly HashSet<string> updatingFiles = new HashSet<string>();		//currently updating files, mainly for multi threaded update
		private readonly GitCallbacks callbacks;
        private readonly List<ISettingsAffector> settingsAffectors = new List<ISettingsAffector>();
		private readonly GitAsyncManager asyncManager;
		private readonly List<IGitWatcher> watchers = new List<IGitWatcher>();
		private readonly ILogger logger;
		private readonly GitInitializer initializer;
		private readonly UniGitPaths paths;

		[UniGitInject]
		public GitManager(
			GitCallbacks callbacks, 
			GitSettingsJson settings, 
			IGitPrefs prefs, 
			GitAsyncManager asyncManager,
			UniGitData gitData,
			ILogger logger,
			GitInitializer initializer,
			UniGitPaths paths)
		{
			this.paths = paths;
			this.gitData = gitData;
			this.callbacks = callbacks;
			this.Prefs = prefs;
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
			if(string.IsNullOrEmpty(paths.RepoPath)) return;
			DeleteDirectory(paths.RepoPath);
		}

		private void DeleteDirectory(string targetDir)
		{
			var files = Directory.GetFiles(targetDir);
			var dirs = Directory.GetDirectories(targetDir);

			foreach (var file in files)
			{
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}

			foreach (var dir in dirs)
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

			if (ActionQueue.Count > 0)
			{
				var action = ActionQueue.Dequeue();
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
            if (!initializer.IsValidRepo) return;
            StartUpdating(paths);

            if (reloadRepository || repository == null)
            {
                repository?.Dispose();
                repository = CreateRepository(gitSettings.ActiveSubModule);
                callbacks.IssueOnRepositoryLoad(repository);
            }

            if (!forceSingleThread && Threading.IsFlagSet(GitSettingsJson.ThreadingType.Status)) RetreiveStatusThreaded(paths);
            else RetreiveStatus(paths);
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
			if (Prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			var pathsFinal = projectPaths.Where(a => !IsEmptyFolder(a)).Select(ToLocalPath).SelectMany(GetPathWithMeta).ToArray();
			if (pathsFinal.Length > 0)
			{
				var autoStage = gitSettings != null && gitSettings.AutoStage;
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
			if (Prefs.GetBool(UnityEditorGitPrefs.DisablePostprocess)) return;
			var pathsFinal = projectPaths.Select(ToLocalPath).SelectMany(GetPathWithMeta).ToArray();
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
            if (repository != null || !initializer.IsValidRepo) return;
            repository = CreateRepository(gitSettings.ActiveSubModule);
            callbacks.IssueOnRepositoryLoad(repository);
        }

		private Repository CreateRepository(string activeModule)
		{
			var mainRepository = new Repository(paths.RepoPath);

			if (!string.IsNullOrEmpty(activeModule))
			{
				var subModule = mainRepository.Submodules[activeModule];
				if (subModule != null && Repository.IsValid(UniGitPathHelper.Combine(paths.RepoProjectRelativePath,subModule.Path)))
				{
					var subModuleRepo = new Repository(UniGitPathHelper.Combine(paths.RepoProjectRelativePath, subModule.Path));
					mainRepository.Dispose();
					InSubModule = true;
					dotGitDirCached = subModuleRepo.Info.Path;
					return subModuleRepo;
				}
			}
			InSubModule = false;
			dotGitDirCached = mainRepository.Info.Path;
			return mainRepository;
		}

		public void SwitchToSubModule(string path)
        {
            if (gitSettings.ActiveSubModule == path || !Repository.IsValid(UniGitPathHelper.Combine(paths.RepoProjectRelativePath, path))) return;
            gitSettings.ActiveSubModule = path;
            gitSettings.MarkDirty();
            MarkDirty(true);

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
				var fixedPath = UniGitPathHelper.FixUnityPath(path);
				if(IsDirectory(fixedPath)) continue;
				if(!gitData.DirtyFilesQueue.Contains(fixedPath))
					gitData.DirtyFilesQueue.Add(fixedPath);
			}
		}

		private void RebuildStatus(string[] paths,bool threaded)
		{
			if (paths != null && paths.Length > 0)
			{
				var operations = new List<GitAsyncOperation>();
				foreach (var path in paths)
				{
					operations.Add(asyncManager.QueueWorkerWithLock((p) =>
					{
						gitData.RepositoryStatus.Update(p, repository.RetrieveStatus(p));
						if (IsSubModule(p))
						{
							gitData.RepositoryStatus.Update(p, repository.Submodules[p].RetrieveStatus());
						}
					}, path, gitData.RepositoryStatus.LockObj, threaded));
				}
				while (operations.Any(o => !o.IsDone)) { }  //wait till all done
			}
			else
			{
				lock (gitData.RepositoryStatus.LockObj)
				{
					var options = GetStatusOptions();
					var s = repository.RetrieveStatus(options);
					gitData.RepositoryStatus.Clear();
					gitData.RepositoryStatus.Combine(s);
					foreach (var submodule in repository.Submodules)
					{
						var e = new GitStatusSubModuleEntry(submodule);
						gitData.RepositoryStatus.Add(e);
						gitData.RepositoryStatus.Update(e.Path, repository.RetrieveStatus(e.Path));
					}

					foreach (var remote in repository.Network.Remotes)
					{
						var e = new GitStatusRemoteEntry(remote);
						gitData.RepositoryStatus.Add(e);
					}
				}
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
			asyncManager.QueueWorkerWithLock(() => { RetreiveStatus(paths, true); }, statusRetriveLock,true);
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
				RebuildStatus(paths,threaded);
				FinishUpdating(threaded, paths);
			}
			catch (ThreadAbortException)
			{
				//run status retrieval on main thread if this thread was aborted
				ActionQueue.Enqueue(() =>
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

				//logger.Log(LogType.Error,"Could not retrive Git Status");
				logger.LogException(e);
			}
			finally
			{
				if(!threaded) GitProfilerProxy.EndSample();
			}
		}

		private void StartUpdating(IEnumerable<string> paths)
		{
			IsUpdating = true;
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
				ActionQueue.Enqueue(() =>
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
			IsUpdating = false;
			updatingFiles.Clear();
			callbacks.IssueUpdateRepository(gitData.RepositoryStatus, paths);
			foreach (var watcher in watchers)
			{
				if(!watcher.IsWatching) watcher.MarkDirty();
			}
		}

		internal bool IsFileDirty(string path)
        {
            return gitData.DirtyFilesQueue.Count > 0 && gitData.DirtyFilesQueue.Contains(path);
        }

		internal bool IsFileUpdating(string path)
		{
            if (!IsUpdating) return false;
            return updatingFiles.Count <= 0 || updatingFiles.Contains(path);
        }

		internal bool IsFileStaging(string localPath)
		{
			return asyncStages.Any(s => s.LocalPaths.Contains(localPath));
		}

		public Texture2D GetGitStatusIcon()
		{
			if (!initializer.IsValidRepo) return GitGUI.Textures.CollabNew;
			if (Repository == null) return GitGUI.Textures.Collab;
			if (IsUpdating) return GitGUI.GetTempSpinAnimatedTexture();
			if (gitData.RepositoryStatus.Any(e => e.Status.IsFlagSet(FileStatus.Conflicted))) return GitGUI.Textures.CollabConflict;
			var behindBy = Repository.Head.TrackingDetails.BehindBy;
			var aheadBy = Repository.Head.TrackingDetails.AheadBy;
			if (behindBy.GetValueOrDefault(0) > 0)
			{
				return GitGUI.Textures.CollabPull;
			}
			return aheadBy.GetValueOrDefault(0) > 0 ? GitGUI.Textures.CollabPush : GitGUI.Textures.Collab;
        }

		public void Dispose()
		{
			if (repository != null)
			{
				repository.Dispose();
				repository = null;
			}

            if (callbacks == null) return;
            callbacks.EditorUpdate -= OnEditorUpdate;
            //asset postprocessing
            callbacks.OnWillSaveAssets -= OnWillSaveAssets;
            callbacks.OnPostprocessImportedAssets -= OnPostprocessImportedAssets;
            callbacks.OnPostprocessDeletedAssets -= OnPostprocessDeletedAssets;
            callbacks.OnPostprocessMovedAssets -= OnPostprocessMovedAssets;
            callbacks.OnPlayModeStateChange -= OnPlayModeStateChange;
            callbacks.RepositoryCreate -= OnRepositoryCreate;
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
            if (string.IsNullOrEmpty(localPath)) return;
            if (externalManager.TakeBlame(localPath))
            {
                return;
            }

            var blameWizard = UniGitLoader.GetWindow<GitBlameWizard>(true);
            blameWizard.SetBlamePath(localPath);
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
			},true);
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
			},true);
			asyncStages.Add(new AsyncStageOperation(operation, localPaths));
			return operation;
		}

		public void ExecuteAction(Action action, bool async)
		{
			if (async)
			{
				ActionQueue.Enqueue(action);
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
			var projectPath = ToProjectPath(localPath);
			return !IsSubModule(ToProjectPath(localPath)) && Directory.Exists(Path.IsPathRooted(projectPath) ? UniGitPathHelper.Combine(paths.RepoPath, projectPath) : projectPath);
        }

		public bool IsEmptyFolderMeta(string path)
        {
            return UniGitPathHelper.IsMetaPath(path) && IsEmptyFolder(path.Substring(0, path.Length - 5));
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
			return gitData.RepositoryStatus != null && gitData.RepositoryStatus.SubModuleEntries.Any(m => UniGitPathHelper.Compare(m.Path,projectPath));
		}

		public string ToProjectPath(string localPath)
		{
            if (!string.IsNullOrEmpty(paths.RepoProjectRelativePath))
            {
                localPath = UniGitPathHelper.Combine(paths.RepoProjectRelativePath, localPath);
            }
            
			return InSubModule ? UniGitPathHelper.Combine(gitSettings.ActiveSubModule, localPath) : localPath;
        }

		public string ToLocalPath(string projectPath)
		{
            if (!string.IsNullOrEmpty(paths.RepoProjectRelativePath))
            {
                projectPath = UniGitPathHelper.SubtractDirectory(projectPath, UniGitPathHelper.ToUnityPath(paths.RepoProjectRelativePath));
            }
            return InSubModule ? UniGitPathHelper.SubtractDirectory(projectPath, gitSettings.ActiveSubModule) : projectPath;
        }

		/// <summary>
		/// If in sub module returns it's repo path
		/// </summary>
		/// <returns></returns>
		public string GetCurrentRepoPath()
        {
            return InSubModule ? UniGitPathHelper.Combine(paths.RepoPath, gitSettings.ActiveSubModule) : paths.RepoPath;
        }

		public string GetCurrentDotGitFolder()
        {
            return InSubModule ? dotGitDirCached : UniGitPathHelper.Combine(paths.RepoPath, ".git");
        }

		#region Enumeration helpers

		public IEnumerable<string> GetPathWithMeta(string path)
		{
			if (UniGitPathHelper.IsMetaPath(path))
			{
				var assetPath = AssetPathFromMeta(path);
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
				var metaPath = MetaPathFromAsset(path);
				//if the path is a directory then return only it's meta path
				if (IsDirectory(path))
				{
					yield return metaPath;
					yield break;
				}

                if (string.IsNullOrEmpty(metaPath)) yield break;
                yield return path;
                yield return metaPath;
            }
		}

		public IEnumerable<string> GetPathsWithMeta(IEnumerable<string> paths)
		{
			return paths.SelectMany(GetPathWithMeta);
		}

		public string GetRelativePath(string rootPath)
		{
			return rootPath.Replace(paths.RepoPath, "").TrimStart(UniGitPathHelper.UnityDeirectorySeparatorChar,Path.DirectorySeparatorChar);
		}
		#endregion

		#endregion

		#region Static Helpers

		public static string AssetPathFromMeta(string metaPath)
        {
            return UniGitPathHelper.IsMetaPath(metaPath) ? metaPath.Substring(0, metaPath.Length - 5) : metaPath;
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
		#endregion

		#region Repository Handlers Handlers
		public bool CheckoutNotifyHandler(string path, CheckoutNotifyFlags notifyFlags)
		{
			if (gitSettings.CreateFoldersForDriftingMeta)
			{
				if (UniGitPathHelper.IsMetaPath(path))
				{
					var assetPath = AssetPathFromMeta(path);
					var rootedAssetPath = Path.Combine(paths.RepoPath, assetPath);
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
				var percent = (float)completedSteps / totalSteps;
				EditorUtility.DisplayProgressBar("Transferring", $"Checking Out: {path}",percent);
			}
		}

		public bool FetchTransferProgressHandler(TransferProgress progress)
		{
			var percent = (float)progress.ReceivedObjects / progress.TotalObjects;
			var cancel = EditorUtility.DisplayCancelableProgressBar("Transferring",
                $"Transferring: Received total of: {progress.ReceivedBytes} bytes. {(percent * 100).ToString("###")}%", percent);
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
			Prefs.SetBool(UnityEditorGitPrefs.DisablePostprocess, true);
		}

		public void EnablePostprocessing()
		{
			Prefs.SetBool(UnityEditorGitPrefs.DisablePostprocess, false);
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
			if (IsUpdating)
			{
				return UpdateStatusEnum.Updating;
			}
			return UpdateStatusEnum.Ready;
		}

		public IGitPrefs Prefs { get; }

        public bool IsUpdating { get; private set; }

        public bool IsAsyncStaging => asyncStages.Count > 0;

        public bool IsDirty => gitData.DirtyFilesQueue.Count > 0 || repositoryDirty;

        public bool InSubModule { get; private set; }

        public Signature Signature => new Signature(Repository.Config.GetValueOrDefault<string>("user.name"), Repository.Config.GetValueOrDefault<string>("user.email"),DateTimeOffset.Now);

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
				var newThreading = gitSettings.Threading;
				foreach (var affector in settingsAffectors)
				{
					affector.AffectThreading(ref newThreading);
				}
				return newThreading;
			}
		}

		public Queue<Action> ActionQueue { get; } = new Queue<Action>();

        #endregion

		public class AsyncUpdateOperation : IEquatable<GitAsyncOperation>
		{
			private readonly GitAsyncOperation operation;

            public AsyncUpdateOperation(GitAsyncOperation operation, string[] localPaths)
			{
				this.operation = operation;
				this.LocalPaths = localPaths;
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

			public bool IsDone => operation.IsDone;

            public string[] LocalPaths { get; }
        }

		public class AsyncStageOperation : IEquatable<GitAsyncOperation>
		{
            public AsyncStageOperation(GitAsyncOperation operation, IEnumerable<string> localPaths)
			{
				this.Operation = operation;
				this.LocalPaths = new HashSet<string>(localPaths);
			}

			public bool Equals(GitAsyncOperation other)
			{
				return Operation.Equals(other);
			}

			public override bool Equals(object obj)
			{
				if (obj is GitAsyncOperation)
				{
					return Operation.Equals(obj);
				}
				return ReferenceEquals(this, obj);
			}

			public override int GetHashCode()
			{
				return Operation.GetHashCode();
			}

			public HashSet<string> LocalPaths { get; }

            public GitAsyncOperation Operation { get; }

            public bool IsDone => Operation.IsDone;
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