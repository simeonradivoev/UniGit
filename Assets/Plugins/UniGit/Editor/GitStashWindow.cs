using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitStashWindow : PopupWindowContent
	{
		private StashCollection stashCollection;
		private string[] stashSim;
		private Vector2 stashScroll;
		private int selectedStash;

		public override void OnOpen()
		{
			base.OnOpen();
			stashCollection = GitManager.Repository.Stashes;
			stashSim = new[] {"Test Stash", "Another Stash"};
		}

		public override void OnGUI(Rect rect)
		{
			if(Event.current.type == EventType.MouseMove) editorWindow.Repaint();
			int stashCount = stashCollection.Count();
			EditorGUILayout.BeginHorizontal("IN BigTitle");
			if (GUILayout.Button(GitGUI.GetTempContent(GitOverlay.icons.stashIcon.image, "Stash Save","Save changes in working directory to stash.")))
			{
				EditorWindow.GetWindow<GitStashSaveWizard>(true);
			}
			EditorGUILayout.EndHorizontal();

			GUI.enabled = true;
			stashScroll = EditorGUILayout.BeginScrollView(stashScroll, GUILayout.ExpandHeight(true));
			int stashId = 0;
			foreach (var stash in stashCollection)
			{
				GUIContent stashContent = new GUIContent(stash.FriendlyName);
				Rect stastRect = GUILayoutUtility.GetRect(stashContent, "MenuItem");
				if (Event.current.type == EventType.Repaint)
				{
					((GUIStyle)"MenuItem").Draw(stastRect, stashContent, stastRect.Contains(Event.current.mousePosition) || stashId == selectedStash, false, false, false);
				}else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && stastRect.Contains(Event.current.mousePosition))
				{
					selectedStash = stashId;
				}
				stashId ++;
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal("ProjectBrowserBottomBarBg");
			GUI.enabled = stashCount > 0;
			if (GUILayout.Button(GitGUI.GetTempContent("Apply","Apply stash to working directory."), "minibuttonleft"))
			{
				if (EditorUtility.DisplayDialog("Apply Stash: " + selectedStash,"Are you sure you want to apply stash ? This will override your current working directory!","Apply","Cancel"))
				{
					stashCollection.Apply(selectedStash);
				}
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Pop","Remove and apply stash to working directory."), "minibuttonmid"))
			{
				if (EditorUtility.DisplayDialog("Pop Stash: " + selectedStash, "Are you sure you want to pop the stash ? This will override your current working directory and remove the stash from the list.", "Pop and Apply", "Cancel"))
				{
					stashCollection.Pop(selectedStash);
				}
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Remove","Remove stash from list"), "minibuttonright"))
			{
				if (EditorUtility.DisplayDialog("Remove Stash: " + selectedStash, "Are you sure you want to remove the stash ? This action cannot be undone!", "Remove", "Cancel"))
				{
					stashCollection.Remove(selectedStash);
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
		}
	}
}