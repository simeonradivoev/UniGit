using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitSettingsWindow : GitUpdatableWindow, IHasCustomMenu
	{
		private GitGeneralSettingsTab generalSettingsTab;
		private GitExternalsSettingsTab externalsSettingsTab;
		private GitRemotesSettingsTab remotesSettingsTab;
		private GitBranchesSettingsTab branchesSettingsTab;
		private GitLFSSettingsTab lfsSettingsTab;
		private GitSecuritySettingsTab securitySettingsTab;
		private GitSettingsTab[] tabs;
		[SerializeField] private SettingTabEnum tab;

		[MenuItem("Window/GIT Settings")]
		public static void CreateEditor()
		{
			GetWindow(true);
		}

		public static GitSettingsWindow GetWindow(bool focus)
		{
			return GetWindow<GitSettingsWindow>("Git Settings",focus);
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			generalSettingsTab = new GitGeneralSettingsTab();
			externalsSettingsTab = new GitExternalsSettingsTab();
			remotesSettingsTab = new GitRemotesSettingsTab();
			branchesSettingsTab = new GitBranchesSettingsTab();
			lfsSettingsTab = new GitLFSSettingsTab();
			securitySettingsTab = new GitSecuritySettingsTab();

			tabs = new GitSettingsTab[]
			{
				generalSettingsTab,
				externalsSettingsTab,
				remotesSettingsTab,
				branchesSettingsTab,
				lfsSettingsTab,
				securitySettingsTab,
			};

			foreach (var settingsTab in tabs)
			{
				settingsTab.OnEnable();
			}
		}

		protected override void OnFocus()
		{
			base.OnFocus();
			LoseFocus();
			if (!GitManager.IsValidRepo) return;
			currentTab.OnFocus();
			OnGitUpdate(null,null);
		}

		[UsedImplicitly]
		private void OnUnfocus()
		{
			LoseFocus();
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			Repaint();
		}

		protected override void OnEditorUpdate()
		{
			
		}

		[UsedImplicitly]
		private void OnGUI()
		{
			if (!GitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI();
				return;
			}

			Event current = Event.current;

			EditorGUILayout.BeginHorizontal("Toolbar");
			EditorGUI.BeginChangeCheck();
			bool value = GUILayout.Toggle(tab == SettingTabEnum.General, GitGUI.GetTempContent("General"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.General;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Externals, GitGUI.GetTempContent("Externals","External Programs Helpers"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Externals;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Remotes, GitGUI.GetTempContent("Remotes","Remote Repositories"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Remotes;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Branches, GitGUI.GetTempContent("Branches"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Branches;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.LFS, GitGUI.GetTempContent("LFS","Git Large File Storage (beta)"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.LFS;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Security, GitGUI.GetTempContent("Security"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Security;
			}
			if (EditorGUI.EndChangeCheck())
			{
				LoseFocus();
				currentTab.OnFocus();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("_Help"), "IconButton"))
			{
				GoToHelp();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			if (GitManager.Repository != null)
			{
				Rect localRect = new Rect(0, 0, position.width, position.height - EditorGUIUtility.singleLineHeight * 1.6f);
				currentTab.OnGUI(localRect,current);
			}
			EditorGUILayout.Space();

			if (current.type == EventType.MouseDown)
			{
				LoseFocus();
			}
		}

		protected override void OnGitUpdate(GitRepoStatus status, string[] paths)
		{
			
		}

		protected override void OnInitialize()
		{
			
		}

		private void GoToHelp()
		{
			Application.OpenURL("https://github.com/simeonradivoev/UniGit/wiki/Setup#configuration");
		}

		#region IHasCustomMenu

		public void AddItemsToMenu(GenericMenu menu)
		{
			foreach (var settingsTab in tabs)
			{
				if (settingsTab is IHasCustomMenu)
				{
					((IHasCustomMenu)settingsTab).AddItemsToMenu(menu);
				}
			}
			menu.AddItem(new GUIContent("Help"),false, GoToHelp);
		}

		#endregion

		protected new void OnDestroy()
		{
			base.OnDestroy();

			foreach (var settingsTab in tabs)
			{
				settingsTab.OnDestroy();
			}
		}

		private GitSettingsTab currentTab
		{
			get
			{
				int tabIndex = Mathf.Max((int)tab, 0);
				if (tabIndex < tabs.Length)
					return tabs[tabIndex];
				return generalSettingsTab;
			}
		}

		[SerializeField]
		private enum SettingTabEnum
		{
			General,
			Externals,
			Remotes,
			Branches,
			LFS,
			Security,
			Ignore
		}
	}
}