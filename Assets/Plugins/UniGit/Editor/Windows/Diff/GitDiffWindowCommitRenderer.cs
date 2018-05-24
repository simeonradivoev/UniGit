using System.IO;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitDiffWindowCommitRenderer : IDiffWindowRenderer
	{
		private readonly GitSettingsJson gitSettings;
		private readonly GitManager gitManager;
		private readonly GitOverlay gitOverlay;
		private readonly GitInitializer initializer;

		private class Styles
		{
			public GUIStyle mergeIndicator;
			public GUIStyle commitMessageFoldoud;
			public GUIStyle commitButton;
		}

		private Styles styles;
		private char commitMessageLastChar;

		[UniGitInject]
		public GitDiffWindowCommitRenderer(GitSettingsJson gitSettings, GitManager gitManager, GitOverlay gitOverlay, GitInitializer initializer)
		{
			this.gitSettings = gitSettings;
			this.gitManager = gitManager;
			this.gitOverlay = gitOverlay;
			this.initializer = initializer;
		}

		public void LoadStyles()
		{
			styles = new Styles()
			{
				mergeIndicator = "AssetLabel",
				commitMessageFoldoud = "IN Foldout",
				commitButton = "DropDownButton"
			};
		}

		internal void DoCommit(RepositoryInformation repoInfo,GitDiffWindow window,ref Vector2 commitScroll)
		{
			var settings = window.GitDiffSettings;

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			if (repoInfo.CurrentOperation == CurrentOperation.Merge)
				GUILayout.Label(GitGUI.GetTempContent("Merge"), styles.mergeIndicator);
			window.CommitMaximized = GUILayout.Toggle(window.CommitMaximized, GitGUI.GetTempContent(gitSettings.ReadFromFile ? "File Commit Message: (Read Only)" : "Commit Message: "), styles.commitMessageFoldoud, GUILayout.Width(gitSettings.ReadFromFile ? 210 : 116));
			if (!window.CommitMaximized)
			{
				if (!gitSettings.ReadFromFile)
				{
					EditorGUI.BeginChangeCheck();
					GUI.SetNextControlName("Commit Message Field");
					settings.commitMessage = EditorGUILayout.TextArea(settings.commitMessage, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					if (EditorGUI.EndChangeCheck())
					{
						window.SaveCommitMessage();
					}
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent(settings.commitMessageFromFile), GUI.skin.textArea, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				}
			}
			EditorGUILayout.EndHorizontal();
			if (window.CommitMaximized)
			{
				commitScroll = EditorGUILayout.BeginScrollView(commitScroll, GUILayout.Height(window.CalculateCommitTextHeight()));
				if (!gitSettings.ReadFromFile)
				{
					EditorGUI.BeginChangeCheck();
					GUI.SetNextControlName("Commit Message Field");
					string newCommitMessage = EditorGUILayout.TextArea(settings.commitMessage, GUILayout.ExpandHeight(true));
					if (EditorGUI.EndChangeCheck())
					{
						if ((Event.current.character == ' ' || Event.current.character == '\0') && !(commitMessageLastChar == ' ' || commitMessageLastChar == '\0'))
						{
							if (Undo.GetCurrentGroupName() == GitDiffWindow.CommitMessageUndoGroup)
							{
								Undo.IncrementCurrentGroup();
							}
						}
						commitMessageLastChar = Event.current.character;
						Undo.RecordObject(window, GitDiffWindow.CommitMessageUndoGroup);
						settings.commitMessage = newCommitMessage;
						window.SaveCommitMessage();
					}
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent(settings.commitMessageFromFile), GUI.skin.textArea,GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndScrollView();
			}

			EditorGUILayout.BeginHorizontal();
			
			if (GUILayout.Button(GitGUI.GetTempContent("Commit"), styles.commitButton))
			{
				GenericMenu commitMenu = new GenericMenu();
				BuildCommitMenu(commitMenu,window);
				commitMenu.ShowAsContext();
			}
			GitGUI.StartEnable(!gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit));
			settings.emptyCommit = GUILayout.Toggle(settings.emptyCommit, GitGUI.GetTempContent("Empty Commit", "Commit the message only without changes"));
			EditorGUI.BeginChangeCheck();
			settings.amendCommit = GUILayout.Toggle(settings.amendCommit, GitGUI.GetTempContent("Amend Commit", "Amend previous commit."));
			if (EditorGUI.EndChangeCheck())
			{
				if (settings.amendCommit)
				{
					window.AmmendCommit();
				}
			}
			settings.prettify = GUILayout.Toggle(settings.prettify, GitGUI.GetTempContent("Prettify", "Prettify the commit message"));
			GitGUI.EndEnable();
			GUILayout.FlexibleSpace();
			if (GitGUI.LinkButtonLayout(gitOverlay.icons.donateSmall, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.Donate);
			}
			if (GitGUI.LinkButtonLayout(GitGUI.Contents.Help,GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.DiffWindowHelp);
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		private void BuildCommitMenu(GenericMenu commitMenu,GitDiffWindow window)
		{
			if(gitManager == null) return;
			commitMenu.AddItem(new GUIContent("✔ Commit"), false, ()=> CommitCallback(window));
			if (!gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit))
			{
				commitMenu.AddItem(new GUIContent("➔ Commit And Push"), false, ()=> CommitAndPushCallback(window));
			}
			else
			{
				commitMenu.AddDisabledItem(new GUIContent("✔ Commit And Push"));
			}
			commitMenu.AddSeparator("");
			commitMenu.AddItem(new GUIContent("Commit Message/✖ Clear"), false, window.ClearCommitMessage);
			commitMenu.AddItem(new GUIContent("Commit Message/📖 Read from file"), gitSettings.ReadFromFile, ()=>ToggleReadFromFile(window));
			if (File.Exists(initializer.GitCommitMessageFilePath))
			{
				commitMenu.AddItem(new GUIContent("Commit Message/✎ Open File"), false, OpenCommitMessageFile);
			}
			else
			{
				commitMenu.AddDisabledItem(new GUIContent("Commit Message/⤷ Open File"));
			}
			commitMenu.AddItem(new GUIContent("Commit Message/♺ Reload"), false, window.ReadCommitMessage);
		}

		private void ToggleReadFromFile(GitDiffWindow window)
		{
			if (gitSettings.ReadFromFile)
			{
				gitSettings.ReadFromFile = false;
				window.ReadCommitMessage();
			}
			else
			{
				gitSettings.ReadFromFile = true;
				window.ReadCommitMessageFromFile();
			}

			gitSettings.MarkDirty();
		}

		private void OpenCommitMessageFile()
		{
			if (File.Exists(initializer.GitCommitMessageFilePath))
			{
				Application.OpenURL(initializer.GitCommitMessageFilePath);
			}
		}

		private void CommitCallback(GitDiffWindow window)
		{
			if (EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes?", "✔ Commit", "✖ Cancel"))
			{
				window.Commit();
			}
		}

		private void CommitAndPushCallback(GitDiffWindow window)
		{
			if (gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit) || EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes and then push them?", "➔ Commit and Push", "✖ Cancel"))
			{
				if (window.Commit())
				{
					UniGitLoader.DisplayWizard<GitPushWizard>("Git Push","Push");
				}
			}
		}
	}
}
