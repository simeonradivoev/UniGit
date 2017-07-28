using System;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitAutoFetcher
	{
		private readonly GitCredentialsManager credentialsManager;
		private readonly GitManager gitManager;
		private bool needsFetch;

		[UniGitInject]
		public GitAutoFetcher(GitManager gitManager,GitCredentialsManager credentialsManager,GitCallbacks callbacks)
		{
			this.gitManager = gitManager;
			this.credentialsManager = credentialsManager;
			callbacks.EditorUpdate += OnEditorUpdate;
			needsFetch = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
		}

		private void OnEditorUpdate()
		{
			if (needsFetch)
			{
				try
				{
					needsFetch = AutoFetchChanges();
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					needsFetch = false;
				}
			}
		}

		private bool AutoFetchChanges()
		{
			if (gitManager.Repository == null || !gitManager.IsValidRepo || !gitManager.Settings.AutoFetch) return false;
			Remote remote = gitManager.Repository.Network.Remotes.FirstOrDefault();
			if (remote == null) return false;
			GitProfilerProxy.BeginSample("Git automatic fetching");
			try
			{
				gitManager.Repository.Network.Fetch(remote, new FetchOptions()
				{
					CredentialsProvider = credentialsManager.FetchChangesAutoCredentialHandler,
					OnTransferProgress = GitManager.FetchTransferProgressHandler,
					RepositoryOperationStarting = (context) =>
					{
						Debug.Log("Repository Operation Starting");
						return true;
					}
				});
				//Debug.LogFormat("Auto Fetch From remote: {0} - ({1}) successful.", remote.Name, remote.Url);
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("Automatic Fetching from remote: {0} with URL: {1} Failed!", remote.Name, remote.Url);
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			GitProfilerProxy.EndSample();
			return false;
		}
	}
}