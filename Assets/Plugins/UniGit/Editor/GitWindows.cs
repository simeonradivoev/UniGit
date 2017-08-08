using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitWindows
	{
		private static readonly List<EditorWindow> windows = new List<EditorWindow>();
		public static IEnumerable<EditorWindow> Windows { get { return windows; } }

		public static void AddWindow(EditorWindow window)
		{
			if (windows.Contains(window))
			{
				Debug.LogErrorFormat("Winodw {0} is already in list.",window.GetType().Name);
				return;
			}
			windows.Add(window);
		}

		public static void RemoveWindow(EditorWindow window)
		{
			windows.Remove(window);
		}
	}
}