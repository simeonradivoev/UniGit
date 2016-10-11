using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitLfsTrackPopupWindow : PopupWindowContent
	{
		private EditorWindow focusWindow;
		private string extension;

		public GitLfsTrackPopupWindow(EditorWindow focusWindow)
		{
			this.focusWindow = focusWindow;
		}

		public override void OnGUI(Rect rect)
		{
			extension = EditorGUILayout.TextField(new GUIContent("Extension"), extension);
			GUI.enabled = !string.IsNullOrEmpty(extension);
			if (GUILayout.Button(new GUIContent("Track")))
			{
				GitLfsManager.Track(extension);
				focusWindow.Focus();
				focusWindow.ShowNotification(new GUIContent(extension + " now tracked by LFS."));
				GitLfsManager.Update();
			}
			GUI.enabled = true;
		}
	}
}