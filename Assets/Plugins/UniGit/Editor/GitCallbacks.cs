using System;
using LibGit2Sharp;
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
			if(RepositoryCreate != null)
				RepositoryCreate.Invoke();
		}

		public event Action<GitRepoStatus, string[]> UpdateRepository;
		public void IssueUpdateRepository(GitRepoStatus repoStatus, string[] paths)
		{
			if (UpdateRepository != null)
				UpdateRepository.Invoke(repoStatus, paths);
		}

		public event Action<Repository> OnRepositoryLoad;
		public void IssueOnRepositoryLoad(Repository repository)
		{
			if(OnRepositoryLoad != null)
				OnRepositoryLoad.Invoke(repository);
		}

		public event Action EditorUpdate;
		public void IssueEditorUpdate()
		{
			if(EditorUpdate != null)
				EditorUpdate.Invoke();
		}

		public event Action UpdateRepositoryStart;
		public void IssueUpdateRepositoryStart()
		{
			if(UpdateRepositoryStart != null)
				UpdateRepositoryStart.Invoke();
		}

		public event Action RefreshAssetDatabase;
		public void IssueAssetDatabaseRefresh()
		{
			if (RefreshAssetDatabase != null)
				RefreshAssetDatabase.Invoke();
		}

		public event Action SaveAssetDatabase;
		public void IssueSaveDatabaseRefresh()
		{
			if (SaveAssetDatabase != null)
				SaveAssetDatabase.Invoke();
		}

		public event Action<GitAsyncOperation> AsyncStageOperationDone;
		public void IssueAsyncStageOperationDone(GitAsyncOperation operation)
		{
			if (AsyncStageOperationDone != null)
				AsyncStageOperationDone.Invoke(operation);
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
			if(OnSettingsChange != null)
				OnSettingsChange.Invoke();
		}

		#region Asset Postprocessing Events

		public event GitAssetPostprocessors.OnWillSaveAssetsDelegate OnWillSaveAssets;
		public void IssueOnWillSaveAssets(string[] paths,ref string[] outputs)
		{
			if(OnWillSaveAssets != null) OnWillSaveAssets.Invoke(paths, ref outputs);
		}

		public event Action<string[]> OnPostprocessImportedAssets;
		public void IssueOnPostprocessImportedAssets(string[] paths)
		{
			if (OnPostprocessImportedAssets != null) OnPostprocessImportedAssets.Invoke(paths);
		}

		public event Action<string[]> OnPostprocessDeletedAssets;
		public void IssueOnPostprocessDeletedAssets(string[] paths)
		{
			if (OnPostprocessDeletedAssets != null) OnPostprocessDeletedAssets.Invoke(paths);
		}

		public event Action<string[],string[]> OnPostprocessMovedAssets;
		public void IssueOnPostprocessMovedAssets(string[] paths,string[] movedFrom)
		{
			if (OnPostprocessMovedAssets != null) OnPostprocessMovedAssets.Invoke(paths, movedFrom);
		}

		#endregion

		public event Action<string, Rect> ProjectWindowItemOnGUI;
		public void IssueProjectWindowItemOnGUI(string guid,Rect rect)
		{
			if (ProjectWindowItemOnGUI != null)
				ProjectWindowItemOnGUI.Invoke(guid, rect);
		}
	}
}