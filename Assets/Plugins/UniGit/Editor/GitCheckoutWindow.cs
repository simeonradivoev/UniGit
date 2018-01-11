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

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawBranchSelection();
			force = EditorGUILayout.Toggle(GitGUI.GetTempContent("Force","Override working tree changes"), force);
			return EditorGUI.EndChangeCheck();
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			CheckoutOptions checkoutOptions = new CheckoutOptions()
			{
				OnCheckoutNotify = OnCheckoutNotify,
				OnCheckoutProgress = OnCheckoutProgress
			};

			if(force)
				checkoutOptions.CheckoutModifiers = CheckoutModifiers.Force;

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