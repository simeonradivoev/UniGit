using System;
using System.Reflection;
using UnityEditor;

namespace UniGit.Utils
{
	public class GitReflectionHelper
	{
		private Func<EditorWindow, bool> hasFocusFucntion;
		private Type projectWindowType;
		public static bool TestsRunning;

		public GitReflectionHelper()
		{
			hasFocusFucntion = (Func<EditorWindow,bool>)Delegate.CreateDelegate(typeof(Func<EditorWindow,bool>), typeof(EditorWindow).GetProperty("hasFocus", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
			projectWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
		}

		public Func<EditorWindow, bool> HasFocusFucntion
		{
			get { return hasFocusFucntion; }
		}

		public Type ProjectWindowType
		{
			get { return projectWindowType; }
		}
	}
}
