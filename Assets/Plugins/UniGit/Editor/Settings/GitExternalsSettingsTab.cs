using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitExternalsSettingsTab : GitSettingsTab
	{
		public GitExternalsSettingsTab(GitManager gitManager) : base(gitManager)
		{
		}

		internal override void OnGUI(Rect rect, Event current)
		{
			GitSettingsJson settings = gitManager.Settings;
			if (settings == null) return;

			EditorGUI.BeginChangeCheck();
			settings.ExternalsType = (GitSettingsJson.ExternalsTypeEnum)EditorGUILayout.EnumMaskField(GitGUI.GetTempContent("External Program Uses", "Use an external program for more advanced features like pushing, pulling, merging and so on"), settings.ExternalsType);
			if (EditorGUI.EndChangeCheck())
			{
				settings.MarkDirty();
			}

			EditorGUI.BeginChangeCheck();
			int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("External Program", "The name of the External program to use"), GitExternalManager.SelectedAdapterIndex, GitExternalManager.AdapterNames);
			settings.ExternalProgram = GitExternalManager.AdapterNames[newSelectedIndex].text;
			if (EditorGUI.EndChangeCheck())
			{
				GitExternalManager.SetSelectedAdapter(newSelectedIndex);
				settings.MarkDirty();
			}

			EditorGUILayout.HelpBox("Using external programs is always recommended as UniGit is still in development.", MessageType.Info);
		}
	}
}