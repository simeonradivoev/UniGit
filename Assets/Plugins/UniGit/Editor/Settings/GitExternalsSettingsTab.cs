using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitExternalsSettingsTab : GitSettingsTab
	{
		private readonly GitExternalManager externalManager;

		[UniGitInject]
		public GitExternalsSettingsTab(GitManager gitManager,
			GitSettingsWindow settingsWindow,
			GitExternalManager externalManager,
			UniGitData data,
			GitSettingsJson gitSettings,
			GitCallbacks gitCallbacks) 
			: base(new GUIContent("Externals", "External Programs Helpers"), gitManager, settingsWindow,data,gitSettings,gitCallbacks)
		{
			this.externalManager = externalManager;
		}

		internal override void OnGUI(Rect rect, Event current)
		{
			if (gitSettings == null) return;

			EditorGUI.BeginChangeCheck();
			gitSettings.ExternalsType = (GitSettingsJson.ExternalsTypeEnum)EditorGUILayout.EnumFlagsField(GitGUI.GetTempContent("External Program Uses", "Use an external program for more advanced features like pushing, pulling, merging and so on"), gitSettings.ExternalsType);
			if (EditorGUI.EndChangeCheck())
			{
				gitSettings.MarkDirty();
			}

			EditorGUI.BeginChangeCheck();
			int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("External Program", "The name of the External program to use"), externalManager.SelectedAdapterIndex, externalManager.AdapterNames);
			gitSettings.ExternalProgram = externalManager.AdapterNames[newSelectedIndex].text;
			if (EditorGUI.EndChangeCheck())
			{
				externalManager.SetSelectedAdapter(newSelectedIndex);
				gitSettings.MarkDirty();
			}

			EditorGUILayout.HelpBox("Using external programs is always recommended as UniGit is still in development.", MessageType.Info);
		}
	}
}