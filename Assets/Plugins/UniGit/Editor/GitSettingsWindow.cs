using System;
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
		private const string WindowTitle = "Git Settings";

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
			GetWindow(true,GitManager.Instance);
		}

		public static GitSettingsWindow GetWindow(bool focus,GitManager gitManager)
		{
			var window = GetWindow<GitSettingsWindow>(false, WindowTitle, focus);
			window.Construct(gitManager);
			return window;
		}

		public override void Construct(GitManager gitManager)
		{
			base.Construct(gitManager);
			InitTabs();
		}

		protected override void OnEnable()
		{
			titleContent.text = WindowTitle;
			base.OnEnable();
			if(gitManager == null)
				Construct(GitManager.Instance);
		}

		private void InitTabs()
		{
			if (tabs != null)
			{
				foreach (var settingsTab in tabs)
				{
					settingsTab.Dispose();
				}
			}

			try
			{
				generalSettingsTab = new GitGeneralSettingsTab(gitManager, this);
				externalsSettingsTab = new GitExternalsSettingsTab(gitManager, this);
				remotesSettingsTab = new GitRemotesSettingsTab(gitManager, this);
				branchesSettingsTab = new GitBranchesSettingsTab(gitManager, this);
				lfsSettingsTab = new GitLFSSettingsTab(gitManager, this);
				securitySettingsTab = new GitSecuritySettingsTab(gitManager, this);
			}
			catch (Exception e)
			{
				Debug.LogError("There was a problem while creating the settings window tabs.");
				Debug.LogException(e);
			}
			finally
			{
				tabs = new GitSettingsTab[]
				{
					generalSettingsTab,
					externalsSettingsTab,
					remotesSettingsTab,
					branchesSettingsTab,
					lfsSettingsTab,
					securitySettingsTab,
				};
			}
		}

		protected override void OnInitialize()
		{
			if (!gitManager.IsValidRepo) return;
			if (tabs == null)
			{
				InitTabs();
			}
			if(currentTab != null) currentTab.OnFocus();
			OnGitUpdate(null, null);
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
			if (!gitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI(gitManager);
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
				if(currentTab != null) currentTab.OnFocus();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("_Help"), "IconButton"))
			{
				GoToHelp();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			if (gitManager.Repository != null)
			{
				Rect localRect = new Rect(0, 0, position.width, position.height - EditorGUIUtility.singleLineHeight * 1.6f);
				if(currentTab != null) currentTab.OnGUI(localRect,current);
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
				settingsTab.Dispose();
			}

			tabs = null;
		}

		private GitSettingsTab currentTab
		{
			get
			{
				if (tabs == null) return null;
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