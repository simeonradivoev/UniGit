using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitWindows
	{
		public static event Action<EditorWindow> OnWindowAddedEvent;
		public static event Action<EditorWindow> OnWindowRemovedEvent;

		private static List<EditorWindow> windows;
		public static IEnumerable<EditorWindow> Windows { get { return windows; } }

		public static void Init()
		{
			//windows list needs to be assigned manualy and not directly on definition as unity might call it after windows have subscribed
			windows = new List<EditorWindow>();
		}

		public static void AddWindow(EditorWindow window)
		{
			if(windows == null) return;
			if (windows.Contains(window))
			{
				Debug.LogErrorFormat("Winodw {0} is already in list.",window.GetType().Name);
				return;
			}
			windows.Add(window);
			if(OnWindowAddedEvent != null) OnWindowAddedEvent.Invoke(window);
		}

		public static void RemoveWindow(EditorWindow window)
		{
			if(windows == null) return;
			if (windows.Remove(window))
			{
				if(OnWindowRemovedEvent != null) OnWindowRemovedEvent.Invoke(window);
			}
		}
	}
}