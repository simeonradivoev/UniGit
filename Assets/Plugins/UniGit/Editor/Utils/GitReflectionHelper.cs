using System;
using System.Reflection;
using UnityEditor;

namespace UniGit.Utils
{
	public class GitReflectionHelper
	{
		private Func<EditorWindow, bool> hasFocusFucntion;
		private Type projectWindowType;
		private Type editModeLauncherType;
		private FieldInfo testRunningField;
		

		public GitReflectionHelper()
		{
			hasFocusFucntion = (Func<EditorWindow,bool>)Delegate.CreateDelegate(typeof(Func<EditorWindow,bool>), typeof(EditorWindow).GetProperty("hasFocus", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
			projectWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
			editModeLauncherType = typeof(UnityEditor.TestTools.RequirePlatformSupportAttribute).Assembly.GetType("UnityEditor.TestTools.TestRunner.EditModeLauncher");
			testRunningField = editModeLauncherType.GetField("IsRunning");
		}

		public Func<EditorWindow, bool> HasFocusFucntion
		{
			get { return hasFocusFucntion; }
		}

		public Type ProjectWindowType
		{
			get { return projectWindowType; }
		}

		public FieldInfo TestRunningField
		{
			get { return testRunningField; }
		}
	}
}
