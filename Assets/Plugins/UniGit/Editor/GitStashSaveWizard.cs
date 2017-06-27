using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitStashSaveWizard : ScriptableWizard
	{
		private string stashMessage;
		private StashModifiers stashModifiers = StashModifiers.Default;

		private void OnEnable()
		{
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
			GitManager.Repository.Stashes.Add(GitManager.Signature, stashMessage, stashModifiers);
		}
	}
}