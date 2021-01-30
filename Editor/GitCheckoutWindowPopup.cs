using System;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCheckoutWindowPopup : PopupWindowContent
	{
		[SerializeField] private bool force;
		private readonly Branch branch;
		private readonly GitManager gitManager;
		private readonly GitCallbacks gitCallbacks;
		private readonly ILogger logger;

		[UniGitInject]
		public GitCheckoutWindowPopup(GitManager gitManager,Branch branch,ILogger logger,GitCallbacks gitCallbacks)
		{
			this.gitManager = gitManager;
			this.branch = branch;
			this.logger = logger;
			this.gitCallbacks = gitCallbacks;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(256,74);
		}

		public override void OnGUI(Rect rect)
		{
			GUILayout.Label(GitGUI.GetTempContent("Switch to: " + branch.FriendlyName), GitGUI.Styles.BigTitle,GUILayout.ExpandWidth(true));
			force = EditorGUILayout.Toggle(GitGUI.GetTempContent("Force", "Override working tree changes"), force);
			if (GUILayout.Button(GitGUI.GetTempContent("Switch")))
			{
				var checkoutOptions = new CheckoutOptions()
				{
					OnCheckoutNotify = OnCheckoutNotify,
					OnCheckoutProgress = OnCheckoutProgress
				};

				if (force)
					checkoutOptions.CheckoutModifiers = CheckoutModifiers.Force;

				if (branch != null)
				{
					try
					{
						GitCommands.Checkout(gitManager.Repository,branch, checkoutOptions);
					}
					catch (Exception e)
					{
						logger.LogFormat(LogType.Error,"There was a problem while switching to branch: {0}",branch.CanonicalName);
						logger.LogException(e);
					}
					finally
					{
						gitCallbacks.IssueAssetDatabaseRefresh();
						gitManager.MarkDirty(true);
					}
				}
				else
				{
					logger.Log(LogType.Error,"Trying to switch to null branch");
				}
			}
		}

		protected bool OnCheckoutNotify(string path, CheckoutNotifyFlags notifyFlags)
		{
			logger.LogFormat(LogType.Log,"{0} ({1})",path,notifyFlags);
			return true;
		}

		protected void OnCheckoutProgress(string path, int completedSteps, int totalSteps)
		{
			var percent = (float)completedSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Checkout", $"Checking {completedSteps} steps out of {totalSteps}.", percent);
		}
	}
}