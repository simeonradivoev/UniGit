using System;
using JetBrains.Annotations;
using LibGit2Sharp;
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
			prune = EditorGUILayout.Toggle(new GUIContent("Prune", "Prune all unreachable objects from the object database"), prune);
			return EditorGUI.EndChangeCheck();
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			try
			{
				gitManager.Repository.Network.Fetch(remotes[selectedRemote], fetchOptions);
#if UNITY_EDITOR
				Debug.Log("Fetch Complete");
#endif
				var window = GitHistoryWindow.GetWindow(true);
				window.ShowNotification(new GUIContent("Fetch Complete"));
				gitManager.MarkDirty(true);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		
	}
}