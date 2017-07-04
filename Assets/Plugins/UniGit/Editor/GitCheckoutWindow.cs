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

			var branch = GitManager.Repository.Branches[branchNames[selectedBranch]];
			if (branch != null)
			{
				GitManager.Repository.Checkout(branch, checkoutOptions);
			}
			else
			{
				Debug.LogError("Could not find branch with name: " + branchNames[selectedBranch]);
			}
		}
	}
}