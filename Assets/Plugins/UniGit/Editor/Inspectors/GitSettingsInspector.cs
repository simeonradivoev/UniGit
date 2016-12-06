using UnityEditor;
using UnityEngine;

namespace UniGit.Inspectors
{
	[CustomEditor(typeof(GitSettings))]
	public class GitSettingsInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("Open the 'Git Settings' window to change the settings.",MessageType.Info);
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Open Settings"), "AC Button"))
			{
				GitSettingsWindow.CreateEditor();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}