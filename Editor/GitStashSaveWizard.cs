using JetBrains.Annotations;
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
		private GitOverlay gitOverlay;

        [UniGitInject]
		private void Construct(GitManager gitManager,GitOverlay gitOverlay)
		{
			this.gitManager = gitManager;
			this.gitOverlay = gitOverlay;
		}

		[UsedImplicitly]
		private void OnEnable()
		{
            GitWindows.AddWindow(this);
			createButtonName = "Save";
			titleContent = new GUIContent("Stash Save",gitOverlay.icons.stashIcon.image);
		}

		[UsedImplicitly]
		private void OnDisable()
	    {
	        GitWindows.RemoveWindow(this);
        }

		protected override bool DrawWizardGUI()
		{
			EditorGUILayout.LabelField(GitGUI.GetTempContent("Stash Message:"));
			stashMessage = EditorGUILayout.TextArea(stashMessage, GUILayout.Height(EditorGUIUtility.singleLineHeight * 6));
			stashModifiers = (StashModifiers)EditorGUILayout.EnumFlagsField("Stash Modifiers",stashModifiers);
			return false;
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			gitManager.Repository.Stashes.Add(gitManager.Signature, stashMessage, stashModifiers);
			gitManager.MarkDirty(true);
		}
	}
}