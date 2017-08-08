using System;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using UniGit.Utils;
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
			pushOptions = new PushOptions() {CredentialsProvider = CredentialsHandler, OnPackBuilderProgress = OnPackBuildProgress, OnPushTransferProgress = PushTransferProgress, OnPushStatusError = OnFail,OnNegotiationCompletedBeforePush = UniGitLoader.HookManager.PrePushHandler};
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
				using (var repository = new Repository(gitManager.RepoPath))
				{
					if (branchNames.Length > 0 && selectedBranch < branchNames.Length)
					{
						repository.Network.Push(repository.Branches[branchNames[selectedBranch]], pushOptions);
						gitManager.MarkDirty();
						var window = UniGitLoader.FindWindow<GitHistoryWindow>();
                        if(window != null)
						    window.ShowNotification(new GUIContent("Push Complete"));
					}
					else
					{
						Debug.LogWarning("No Branch Selected.");
					}
				}
			}
			catch (Exception e)
			{
				if (e is NonFastForwardException)
				{
					GUIContent content = GitGUI.IconContent("console.warnicon", "Could not push changes to remote. Merge changes with remote before pushing.");
					if (focusedWindow != null)
					{
						focusedWindow.ShowNotification(content);
					}
					else
					{
						GitGUI.ShowNotificationOnWindow<GitHistoryWindow>(content,false);
					}
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
#if UNITY_EDITOR
				Debug.Log("Pack Building completed.");
#endif
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