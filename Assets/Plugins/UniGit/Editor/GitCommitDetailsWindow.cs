using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
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
			return new Vector2(640, 256);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUILayout.Space();
			float msgHeight = commitMessageStyle.CalcHeight(GitGUI.GetTempContent(commit.Message), rect.width);
			scroll = EditorGUILayout.BeginScrollView(scroll);
			EditorGUILayout.LabelField(GitGUI.GetTempContent(commit.Message), commitMessageStyle, GUILayout.Height(msgHeight));
			if (changes != null)
			{
				foreach (var change in changes)
				{
					//EditorGUILayout.BeginHorizontal();
					//GUILayout.Label(change.Status.ToString(), "AssetLabel");
					EditorGUILayout.BeginHorizontal("ProjectBrowserHeaderBgTop");
					GUILayout.Label(new GUIContent(GitManager.GetDiffTypeIcon(change.Status, true)) {tooltip = change.Status.ToString()}, GUILayout.Width(16));
					GUILayout.Space(8);
					string[] pathChunks = change.Path.Split('\\');
					for (int i = 0; i < pathChunks.Length; i++)
					{
						string chunk = pathChunks[i];
						if (GUILayout.Button(GitGUI.GetTempContent(chunk), "GUIEditor.BreadcrumbMid"))
						{
							string assetPath = string.Join("/", pathChunks,0,i+1);
							if (assetPath.EndsWith(".meta"))
							{
								assetPath = AssetDatabase.GetAssetPathFromTextMetaFilePath(assetPath);
							}
							var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
							if (asset != null)
							{
								Selection.activeObject = asset;
							}
						}
					}
					//GUILayout.Label(new GUIContent(" (" + change.Status + ") " + change.Path));
					EditorGUILayout.EndHorizontal();
					Rect r = GUILayoutUtility.GetLastRect();
					if (Event.current.type == EventType.ContextClick && r.Contains(Event.current.mousePosition))
					{
						string path = change.Path;
						GenericMenu menu = new GenericMenu();
						if (commit.Parents.Count() == 1)
						{
							
							menu.AddItem(new GUIContent("Difference with previous commit"), false, () =>
							{
								Commit parent = commit.Parents.Single();
								GitManager.ShowDiff(path, parent, commit);
							});
						}
						else
						{
							menu.AddDisabledItem(new GUIContent(new GUIContent("Difference with previous commit")));
						}
						menu.AddItem(new GUIContent("Difference with HEAD"), false, () =>
						{
							GitManager.ShowDiff(path, commit, GitManager.Repository.Head.Tip);
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