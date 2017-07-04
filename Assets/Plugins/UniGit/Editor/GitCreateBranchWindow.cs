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
			return new Vector2(300, 92);
		}

		public override void OnGUI(Rect rect)
		{
			GUILayout.Label(new GUIContent("Create Branch"), "IN BigTitle", GUILayout.ExpandWidth(true));
			if (commit != null)
			{
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
				EditorGUILayout.LabelField(GitGUI.GetTempContent("Commit SHA"), new GUIContent(commit.Sha));
			}
			else
			{
				EditorGUILayout.HelpBox("No selected commit.", MessageType.Warning);
			}

			GitGUI.StartEnable(IsValidBranchName(name) && commit != null);
			GUIContent createBranchContent = GitGUI.GetTempContent("Create Branch");
			if(!IsValidBranchName(name))
				createBranchContent.tooltip = "Invalid Branch Name";
			if (GUILayout.Button(createBranchContent))
			{
				try
				{
					var branch = GitManager.Repository.CreateBranch(name, commit);
					if (branch != null)
					{
						Debug.Log("Branch " + name + " created");
						editorWindow.Close();
						if (onCreated != null)
						{
							onCreated.Invoke();
						}
					}
					else
					{
						Debug.LogError("Could not create branch: " + name);
					}
					
				}
				catch (Exception e)
				{
					Debug.LogError("Could not create branch!");
					Debug.LogException(e);
				}
				finally
				{
					GitManager.MarkDirty(true);
				}
			}
			GitGUI.EndEnable();
		}

		private bool IsValidBranchName(string branchName)
		{
			return !string.IsNullOrEmpty(branchName) && !branchName.Contains(" ");
		}
	}
}