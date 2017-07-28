using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitExternalsSettingsTab : GitSettingsTab
	{
		private readonly GitExternalManager externalManager;

		[UniGitInject]
		public GitExternalsSettingsTab(GitManager gitManager,GitSettingsWindow settingsWindow,GitExternalManager externalManager) 
			: base(new GUIContent("Externals", "External Programs Helpers"), gitManager, settingsWindow)
		{
			this.externalManager = externalManager;
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
			int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("External Program", "The name of the External program to use"), externalManager.SelectedAdapterIndex, externalManager.AdapterNames);
			settings.ExternalProgram = externalManager.AdapterNames[newSelectedIndex].text;
			if (EditorGUI.EndChangeCheck())
			{
				externalManager.SetSelectedAdapter(newSelectedIndex);
				settings.MarkDirty();
			}

			EditorGUILayout.HelpBox("Using external programs is always recommended as UniGit is still in development.", MessageType.Info);
		}
	}
}