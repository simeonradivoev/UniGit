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

		private GitSettingsTab[] tabs;
		[SerializeField] private int tab;
		[NonSerialized] private readonly InjectionHelper injectionHelper = new InjectionHelper();

        [UniGitInject]
		private void Construct(InjectionHelper parentInjectionHelper)
		{
			injectionHelper.SetParent(parentInjectionHelper);
            injectionHelper.Bind<GitSettingsTab>().To<GitGeneralSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitExternalsSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitRemotesSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitBranchesSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitLFSSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitSecuritySettingsTab>();
		}

		protected override void OnEnable()
		{
			titleContent.text = WindowTitle;
			base.OnEnable();
			injectionHelper.Bind(GetType()).FromInstance(this);
		}

		private void InitTabs()
		{
			if(gitManager == null || !gitManager.IsValidRepo) return;
			if (tabs != null)
			{
				foreach (var settingsTab in tabs)
				{
					settingsTab.Dispose();
				}
				tabs = null;
			}

			try
			{
				tabs = injectionHelper.GetInstances<GitSettingsTab>().ToArray();
			}
			catch (Exception e)
			{
				Debug.LogError("There was a problem while creating the settings window tabs.");
				Debug.LogException(e);
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
			if (gitManager == null || !gitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI(gitManager);
				return;
			}

			Event current = Event.current;

			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			EditorGUI.BeginChangeCheck();
			if (tabs != null)
			{
				for (int i = 0; i < tabs.Length; i++)
				{
					bool value = GUILayout.Toggle(tab == i, tabs[i].Name, EditorStyles.toolbarButton);
					if (value)
					{
						tab = i;
					}
				}
			}
			if (EditorGUI.EndChangeCheck())
			{
				LoseFocus();
				if(currentTab != null) currentTab.OnFocus();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("_Help"), GitGUI.Styles.IconButton))
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
			if (tabs != null)
			{
				foreach (var settingsTab in tabs)
				{
					var customMenu = settingsTab as IHasCustomMenu;
					if (customMenu != null)
					{
						customMenu.AddItemsToMenu(menu);
					}
				}
			}
			menu.AddItem(new GUIContent("Help"),false, GoToHelp);
		}

		#endregion

		protected new void OnDestroy()
		{
			base.OnDestroy();

			if (tabs != null)
			{
				foreach (var settingsTab in tabs)
				{
					settingsTab.Dispose();
				}

				tabs = null;
			}
		}

		private GitSettingsTab currentTab
		{
			get
			{
				if (tabs == null) return null;
				int tabIndex = Mathf.Max((int)tab, 0);
				if (tabIndex < tabs.Length)
					return tabs[tabIndex];
				return null;
			}
		}
	}
}