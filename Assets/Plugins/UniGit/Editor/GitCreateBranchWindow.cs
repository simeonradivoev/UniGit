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
		private readonly Commit commit;
		private readonly Action onCreated;
		private readonly GitManager gitManager;

		public GitCreateBranchWindow(Commit commit, Action onCreated,GitManager gitManager)
		{
			this.gitManager = gitManager;
			this.commit = commit;
			this.onCreated = onCreated;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(300, 92);
		}

		public override void OnGUI(Rect rect)
		{
			GUILayout.Label(GitGUI.GetTempContent("Create Branch"), GitGUI.Styles.BigTitle, GUILayout.ExpandWidth(true));
			if (commit != null)
			{
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
				EditorGUILayout.LabelField(GitGUI.GetTempContent("Commit SHA"), GitGUI.GetTempContent(commit.Sha));
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
					var branch = gitManager.Repository.CreateBranch(name, commit);
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
					gitManager.MarkDirty(true);
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