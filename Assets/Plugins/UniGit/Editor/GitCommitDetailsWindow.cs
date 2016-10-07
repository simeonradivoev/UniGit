using System.IO;
using System.Linq;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;
using Tree = LibGit2Sharp.Tree;

namespace UniGit
{
	public class GitCommitDetailsWindow : PopupWindowContent
	{
		private Commit commit;
		private GUIStyle commitMessageStyle;
		private TreeChanges changes;
		private Tree commitTree;
		private Vector2 scroll;

		public GitCommitDetailsWindow(Commit commit)
		{
			this.commit = commit;
			commitTree = commit.Tree;
			Commit parentCommit = commit.Parents.FirstOrDefault();

			if (parentCommit != null)
			{
				changes = GitManager.Repository.Diff.Compare<TreeChanges>(parentCommit.Tree, commitTree);
			}

			commitMessageStyle = new GUIStyle("TL SelectionButton") {alignment = TextAnchor.UpperLeft,padding = new RectOffset(4,4,4,4),wordWrap = true};
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(512, 256);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUILayout.Space();
			float msgHeight = commitMessageStyle.CalcHeight(new GUIContent(commit.Message), rect.width);
			EditorGUILayout.LabelField(new GUIContent(commit.Message), commitMessageStyle,GUILayout.Height(msgHeight));
			scroll = EditorGUILayout.BeginScrollView(scroll);
			EditorGUILayout.Space();
			if (changes != null)
			{
				foreach (var change in changes)
				{
					//EditorGUILayout.BeginHorizontal();
					//GUILayout.Label(change.Status.ToString(), "AssetLabel");
					EditorGUILayout.BeginHorizontal("ProjectBrowserHeaderBgTop");
					GUILayout.Label(new GUIContent(GitManager.GetDiffTypeIcon(change.Status, true)), GUILayout.Width(16));
					GUILayout.Label(new GUIContent("(" + change.Status + ")"), "AboutWIndowLicenseLabel");
					GUILayout.Space(8);
					foreach (var chunk in change.Path.Split('\\'))
					{
						GUILayout.Label(new GUIContent(chunk), "GUIEditor.BreadcrumbMid");
					}
					//GUILayout.Label(new GUIContent(" (" + change.Status + ") " + change.Path));
					EditorGUILayout.EndHorizontal();
					Rect r = GUILayoutUtility.GetLastRect();
					if (Event.current.type == EventType.ContextClick)
					{
						GenericMenu menu = new GenericMenu();
						if (commit.Parents.Count() == 1)
						{
							menu.AddItem(new GUIContent("Difference with previous commit"), false, () =>
							{
								Commit parent = commit.Parents.Single();
								GitManager.ShowDiff(change.Path, parent, commit);
							});
						}
						else
						{
							menu.AddDisabledItem(new GUIContent(new GUIContent("Difference with previous commit")));
						}
						menu.AddItem(new GUIContent("Difference with HEAD"), false, () =>
						{
							GitManager.ShowDiff(change.Path, commit, GitManager.Repository.Head.Tip);
						});
						menu.ShowAsContext();
					}
					//EditorGUILayout.EndHorizontal();
				}
			}
			else
			{
				DrawTreeEntry(commitTree, 0);
			}
			EditorGUILayout.Space();
			EditorGUILayout.EndScrollView();
		}

		private void DrawTreeEntry(Tree tree, int depth)
		{
			foreach (var file in tree)
			{
				if (file.TargetType == TreeEntryTargetType.Tree)
				{
					EditorGUI.indentLevel = depth;
					EditorGUILayout.LabelField(Path.GetFileName(file.Path));
					DrawTreeEntry(file.Target as Tree, depth + 1);
				}
				else if (!file.Path.EndsWith(".meta"))
				{
					EditorGUI.indentLevel = depth;
					EditorGUILayout.LabelField(file.Path);
				}
			}
		}
	}
}