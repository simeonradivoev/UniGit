using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitStashSaveWizard : ScriptableWizard, IGitWindow
	{
		private string stashMessage;
		private StashModifiers stashModifiers = StashModifiers.Default;
		private GitManager gitManager;

		public void Construct(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}

		private void OnEnable()
		{
			Construct(GitManager.Instance);
			createButtonName = "Save";
			titleContent = new GUIContent("Stash Save",GitOverlay.icons.stashIcon.image);
		}

		protected override bool DrawWizardGUI()
		{
			EditorGUILayout.LabelField(new GUIContent("Stash Message:"));
			stashMessage = EditorGUILayout.TextArea(stashMessage, GUILayout.Height(EditorGUIUtility.singleLineHeight * 6));
			stashModifiers = (StashModifiers)EditorGUILayout.EnumMaskPopup("Stash Modifiers",stashModifiers);
			return false;
		}

		private void OnWizardCreate()
		{
			gitManager.Repository.Stashes.Add(gitManager.Signature, stashMessage, stashModifiers);
			gitManager.MarkDirty(true);
		}
	}
}