using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitWindows
	{
		public static event Action<EditorWindow> OnWindowAddedEvent;
		public static event Action<EditorWindow> OnWindowRemovedEvent;

		private static List<EditorWindow> windows;
		public static IEnumerable<EditorWindow> Windows => windows;

        public static void Init()
		{
			//windows list needs to be assigned manually and not directly on definition as unity might call it after windows have subscribed
			windows = new List<EditorWindow>();
		}

		public static void AddWindow(EditorWindow window)
		{
			if(windows == null) return;
			if (windows.Contains(window))
			{
				Debug.LogErrorFormat("Window {0} is already in list.",window.GetType().Name);
				return;
			}
			windows.Add(window);
            OnWindowAddedEvent?.Invoke(window);
        }

		public static void RemoveWindow(EditorWindow window)
		{
			if(windows == null) return;
			if (windows.Remove(window))
            {
                OnWindowRemovedEvent?.Invoke(window);
            }
		}

		public static T GetWindow<T>() where T : EditorWindow
		{
			return windows.OfType<T>().FirstOrDefault();
		}
	}
}