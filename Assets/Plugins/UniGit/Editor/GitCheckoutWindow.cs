using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCheckoutWindow : GitWizardBase
	{
		[SerializeField] private bool force;
		private CheckoutOptions checkoutOptions;

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawBranchSelection();
			force = EditorGUILayout.Toggle(GitGUI.GetTempContent("Force","Override working tree changes"), force);
			return EditorGUI.EndChangeCheck();
		}

		protected override void OnEnable()
		{
			checkoutOptions = new CheckoutOptions()
			{
				
			};
			base.OnEnable();
		}

		[UniGitInject]
		private void Construct()
		{
			checkoutOptions.OnCheckoutNotify = gitManager.CheckoutNotifyHandler;
			checkoutOptions.OnCheckoutProgress = gitManager.CheckoutProgressHandler;
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			checkoutOptions.CheckoutModifiers = force ? CheckoutModifiers.Force : CheckoutModifiers.None;
			var branch = gitManager.Repository.Branches[branchNames[selectedBranch]];
			if (branch != null)
			{
				gitManager.Repository.Checkout(branch, checkoutOptions);
			}
			else
			{
				logger.LogFormat(LogType.Error,"Could not find branch with name: {0}",branchNames[selectedBranch]);
			}
		}
	}
}