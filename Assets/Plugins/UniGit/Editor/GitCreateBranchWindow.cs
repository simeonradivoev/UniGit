using System;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCreateBranchWindow : PopupWindowContent
	{
		private string name = "";
		private Commit commit;
		private EditorWindow parentWindow;
		private Action onCreated;

		public GitCreateBranchWindow(EditorWindow parentWindow, Commit commit, Action onCreated)
		{
			this.parentWindow = parentWindow;
			this.commit = commit;
			this.onCreated = onCreated;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(300, 80);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUILayout.Space();
			if (commit != null)
			{
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
				EditorGUILayout.LabelField(GitGUI.GetTempContent("Commit SHA"), new GUIContent(commit.Sha));
			}
			else
			{
				EditorGUILayout.HelpBox("No selected commit.", MessageType.Warning);
			}

			GitGUI.StartEnable(!string.IsNullOrEmpty(name) && commit != null);
			if (GUILayout.Button(GitGUI.GetTempContent("Create Branch")))
			{
				try
				{
					var branch = GitManager.Repository.CreateBranch(name, commit);
					editorWindow.Close();
					parentWindow.ShowNotification(new GUIContent(string.Format("Branch '{0}' created", branch.CanonicalName)));
					if (onCreated != null)
					{
						onCreated.Invoke();
					}
					GitManager.MarkDirty(true);
				}
				catch (Exception e)
				{
					Debug.LogError("Could not create branch!");
					Debug.LogException(e);
				}
			}
			GitGUI.EndEnable();
		}
	}
}