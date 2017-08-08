using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitStashSaveWizard : ScriptableWizard
	{
		private string stashMessage;
		private StashModifiers stashModifiers = StashModifiers.Default;
		private GitManager gitManager;

        [UniGitInject]
		private void Construct(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}

		private void OnEnable()
		{
            GitWindows.AddWindow(this);
			createButtonName = "Save";
			titleContent = new GUIContent("Stash Save",GitOverlay.icons.stashIcon.image);
		}

	    private void OnDisable()
	    {
	        GitWindows.RemoveWindow(this);
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