using System;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCallbacks
	{
		public event Action RepositoryCreate;
		public void IssueRepositoryCreate()
        {
            RepositoryCreate?.Invoke();
        }

		public event Action<IGitPrefs> OnPrefsChange;
		public void IssueOnPrefsChange(IGitPrefs prefs)
        {
            OnPrefsChange?.Invoke(prefs);
        }

		public event Action<GitRepoStatus, string[]> UpdateRepository;
		public void IssueUpdateRepository(GitRepoStatus repoStatus, string[] paths)
        {
            UpdateRepository?.Invoke(repoStatus, paths);
        }

		public event Action<Repository> OnRepositoryLoad;
		public void IssueOnRepositoryLoad(Repository repository)
        {
            OnRepositoryLoad?.Invoke(repository);
        }

		public event Action EditorUpdate;
		public void IssueEditorUpdate()
        {
            EditorUpdate?.Invoke();
        }

		public event Action UpdateRepositoryStart;
		public void IssueUpdateRepositoryStart()
        {
            UpdateRepositoryStart?.Invoke();
        }

		public event Action RefreshAssetDatabase;
		public void IssueAssetDatabaseRefresh()
        {
            RefreshAssetDatabase?.Invoke();
        }

		public event Action SaveAssetDatabase;
		public void IssueSaveDatabaseRefresh()
        {
            SaveAssetDatabase?.Invoke();
        }

		public event Action<GitAsyncOperation> AsyncStageOperationDone;
		public void IssueAsyncStageOperationDone(GitAsyncOperation operation)
        {
            AsyncStageOperationDone?.Invoke(operation);
        }

		public event Action DelayCall;
		public void IssueDelayCall(bool clear)
		{
			if (DelayCall != null)
			{
				DelayCall.Invoke();
				if (clear) DelayCall = null;
			}
		}

		public event Action OnSettingsChange;
		public void IssueSettingsChange()
        {
            OnSettingsChange?.Invoke();
        }

		public event Action<GitLog.LogEntry> OnLogEntry;
		public void IssueLogEntry(GitLog.LogEntry entry)
        {
            OnLogEntry?.Invoke(entry);
        }

		public event Action OnBeforeAssemblyReload;
		public void IssueBeforeAssemblyReload()
        {
            OnBeforeAssemblyReload?.Invoke();
        }

		#region Asset Postprocessing Events

		public event GitAssetPostprocessors.OnWillSaveAssetsDelegate OnWillSaveAssets;
		public void IssueOnWillSaveAssets(string[] paths,ref string[] outputs)
        {
            OnWillSaveAssets?.Invoke(paths, ref outputs);
        }

		public event Action<string[]> OnPostprocessImportedAssets;
		public void IssueOnPostprocessImportedAssets(string[] paths)
        {
            OnPostprocessImportedAssets?.Invoke(paths);
        }

		public event Action<string[]> OnPostprocessDeletedAssets;
		public void IssueOnPostprocessDeletedAssets(string[] paths)
        {
            OnPostprocessDeletedAssets?.Invoke(paths);
        }

		public event Action<string[],string[]> OnPostprocessMovedAssets;
		public void IssueOnPostprocessMovedAssets(string[] paths,string[] movedFrom)
        {
            OnPostprocessMovedAssets?.Invoke(paths, movedFrom);
        }

		public event Action<PlayModeStateChange> OnPlayModeStateChange;
		public void IssueOnPlayModeStateChange(PlayModeStateChange stateChange)
        {
            OnPlayModeStateChange?.Invoke(stateChange);
        }

		#endregion

		public event Action<string, Rect> ProjectWindowItemOnGUI;
		public void IssueProjectWindowItemOnGUI(string guid,Rect rect)
        {
            ProjectWindowItemOnGUI?.Invoke(guid, rect);
        }
	}
}