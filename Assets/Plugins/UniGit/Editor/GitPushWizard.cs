using System;
using JetBrains.Annotations;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitPushWizard : GitWizardBase
	{
		private PushOptions pushOptions;

		protected override void OnEnable()
		{
			base.OnEnable();
			pushOptions = new PushOptions() {CredentialsProvider = CredentialsHandler, OnPackBuilderProgress = OnPackBuildProgress, OnPushTransferProgress = PushTransferProgress, OnPushStatusError = OnFail,OnNegotiationCompletedBeforePush = GitHookManager.PrePushHandler};
		}

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawBranchSelection();
			DrawCredentials();
			return EditorGUI.EndChangeCheck();
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			try
			{
				using (var repository = new Repository(GitManager.RepoPath))
				{
					repository.Network.Push(repository.Branches[branchNames[selectedBranch]], pushOptions);
					GitManager.Update();
					var window = GitHistoryWindow.GetWindow(true);
					window.ShowNotification(new GUIContent("Push Complete"));
				}
			}
			catch (Exception e)
			{
				if (e is NonFastForwardException)
				{
					GUIContent content = EditorGUIUtility.IconContent("console.warnicon");
					content.text = "Could not push changes to remote. Merge changes with remote before pushing.";
					GetWindow<GitHistoryWindow>().ShowNotification(content);
				}
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private bool OnPackBuildProgress(PackBuilderStage stage, int current, int total)
		{
			if (stage == PackBuilderStage.Deltafying)
			{
				return false;
			}
			bool cancel = EditorUtility.DisplayCancelableProgressBar("Building Pack", stage.ToString(), (float)current / total);
			if (current == total)
			{
				Debug.Log("Pack Building completed.");
			}
			return !cancel;
		}

		private bool PushTransferProgress(int current, int total, long bytes)
		{
			float percent = (float)current / total;
			bool cancel = EditorUtility.DisplayCancelableProgressBar("Transferring", string.Format("Transferring: Sent total of: {0} bytes. {1}%", bytes, (percent * 100).ToString("###")), percent);
			if (total == current)
			{
				Debug.LogFormat("Push Transfer complete. Sent a total of {0} bytes.", bytes);
			}
			return !cancel;
		}

		private void OnFail(PushStatusError error)
		{
			EditorUtility.DisplayDialog("Error while pushing", error.Message, "Ok");
		}
	}
}