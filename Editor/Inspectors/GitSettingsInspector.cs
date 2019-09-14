using UniGit.Utils;
using UnityEditor;
using UnityEngine;
#pragma warning disable 618

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
			if (GUILayout.Button(GitGUI.GetTempContent("Open Settings"), GitGUI.Styles.AddComponentBtn))
			{
			    UniGitLoader.GetWindow<GitSettingsWindow>();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}