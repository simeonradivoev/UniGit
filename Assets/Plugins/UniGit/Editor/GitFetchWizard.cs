using System;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitFetchWizard : GitWizardBase
	{
		private FetchOptions fetchOptions;
		[SerializeField]
		private bool prune;

		protected override void OnEnable()
		{
			base.OnEnable();
			fetchOptions = new FetchOptions()
			{
				CredentialsProvider = CredentialsHandler,
				OnProgress = FetchProgress,
				OnTransferProgress = GitManager.FetchTransferProgressHandler,
				Prune = prune, RepositoryOperationCompleted = FetchOperationCompleted,
				RepositoryOperationStarting = FetchOperationStarting 
			};
		}

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawRemoteSelection();
			DrawCredentials();
			prune = EditorGUILayout.Toggle(GitGUI.GetTempContent("Prune", "Prune all unreachable objects from the object database"), prune);
			return EditorGUI.EndChangeCheck();
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			try
			{
			    GitCommands.Fetch(gitManager.Repository,remotes[selectedRemote].Name, fetchOptions);
#if UNITY_EDITOR
				logger.Log(LogType.Log,"Fetch Complete");
#endif
				var window = UniGitLoader.FindWindow<GitHistoryWindow>();
                if(window != null)
				    window.ShowNotification(new GUIContent("Fetch Complete"));
				gitManager.MarkDirty(true);
			}
			catch (Exception e)
			{
				logger.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		
	}
}