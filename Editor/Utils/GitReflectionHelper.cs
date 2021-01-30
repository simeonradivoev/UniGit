using System;
using System.Reflection;
using UnityEditor;

namespace UniGit.Utils
{
	public class GitReflectionHelper
	{
        public static bool TestsRunning;

		public GitReflectionHelper()
		{
			HasFocusFunction = (Func<EditorWindow,bool>)Delegate.CreateDelegate(typeof(Func<EditorWindow,bool>), typeof(EditorWindow).GetProperty("hasFocus",BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(false));
			ProjectWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
		}

		public Func<EditorWindow, bool> HasFocusFunction { get; }

        public Type ProjectWindowType { get; }
    }
}
