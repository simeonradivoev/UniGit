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
		private const float AnimationDuration = 0.4f;
		private const string WindowTitle = "Git Settings";

		private GitSettingsTab[] tabs;
		[SerializeField] private int tab;
		private int lastTabIndex;
		private readonly InjectionHelper injectionHelper = new InjectionHelper();
		private GitOverlay gitOverlay;
		private GitAnimation gitAnimation;
		private GitAnimation.GitTween animationTween;

        [UniGitInject]
		private void Construct(InjectionHelper parentInjectionHelper,GitOverlay gitOverlay,GitAnimation gitAnimation)
        {
	        this.gitOverlay = gitOverlay;
	        this.gitAnimation = gitAnimation;
			injectionHelper.SetParent(parentInjectionHelper);
            injectionHelper.Bind<GitSettingsTab>().To<GitGeneralSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitExternalsSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitRemotesSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitBranchesSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitLFSSettingsTab>();
			injectionHelper.Bind<GitSettingsTab>().To<GitSecuritySettingsTab>();
	        animationTween = GitAnimation.Empty;
        }

		protected override void OnEnable()
		{
			titleContent.text = WindowTitle;
			base.OnEnable();
			injectionHelper.Bind(GetType()).FromInstance(this);
		}

		private void InitTabs()
		{
			if(gitManager == null || !initializer.IsValidRepo) return;
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
				logger.Log(LogType.Error,"There was a problem while creating the settings window tabs.");
				logger.LogException(e);
			}
		}

		protected override void OnInitialize()
		{
			if (!initializer.IsValidRepo) return;
			if (tabs == null)
			{
				InitTabs();
			}
			if(currentTab != null) currentTab.OnFocus();
			OnGitUpdate(null, null);
		}

		protected override void OnLostFocus()
		{
			base.OnLostFocus();
			if (currentTab != null) currentTab.OnLostFocus();
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
			if (gitManager == null || !initializer.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI(initializer);
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
					if (tab != i && value)
					{
						lastTabIndex = tab;
						tab = i;
						animationTween = gitAnimation.StartAnimation(AnimationDuration,this,GitSettingsJson.AnimationTypeEnum.Settings);
					}
				}
			}
			if (EditorGUI.EndChangeCheck())
			{
				LoseFocus();
				if(currentTab != null) currentTab.OnFocus();
			}
			GUILayout.FlexibleSpace();
			if (GitGUI.LinkButtonLayout(gitOverlay.icons.donateSmall, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.Donate);
			}
			if (GitGUI.LinkButtonLayout(GitGUI.Contents.Help, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.SettingsWindowHelp);
			}
			EditorGUILayout.EndHorizontal();
			Rect lastRect = new Rect(0,0,position.width,EditorGUIUtility.singleLineHeight * 1.5f);

			EditorGUILayout.Space();
			if (gitManager.Repository != null)
			{
				Rect localRect = new Rect(0, 0, position.width, position.height - lastRect.y - lastRect.height);
				if (animationTween.Valid)
				{
					float animTime = GitAnimation.ApplyEasing(animationTween.Percent);
					int animDir = (int)Mathf.Sign(tab - lastTabIndex);
					Matrix4x4 lastMatrix = GUI.matrix;
					GUI.matrix = lastMatrix * Matrix4x4.Translate(new Vector3(localRect.width * (1-animTime) * -animDir, 0));
					if (lastTab != null)
					{
						GUILayout.BeginArea(new Rect(0,lastRect.y + lastRect.height,localRect.width,localRect.height));
						lastTab.OnGUI(localRect,current);
						GUILayout.EndArea();
					}
					GUI.matrix = lastMatrix * Matrix4x4.Translate(new Vector3(localRect.width * animTime * animDir, 0));
					if (currentTab != null)
					{
						GUILayout.BeginArea(new Rect(0,lastRect.y + lastRect.height,localRect.width,localRect.height));
						currentTab.OnGUI(localRect,current);
						GUILayout.EndArea();
					}
					GUI.matrix = lastMatrix;
				}
				else
				{
					if (currentTab != null)
					{
						GUILayout.BeginArea(new Rect(0,lastRect.y + lastRect.height,localRect.width,localRect.height));
						currentTab.OnGUI(localRect,current);
						GUILayout.EndArea();
					}
				}
				
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
			menu.AddItem(new GUIContent("Donate"),false, ()=>{GitLinks.GoTo(GitLinks.Donate);});
			menu.AddItem(new GUIContent("Help"),false, ()=>{GitLinks.GoTo(GitLinks.SettingsWindowHelp);});
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

		private GitSettingsTab lastTab
		{
			get
			{
				if (tabs == null) return null;
				int tabIndex = Mathf.Max(lastTabIndex, 0);
				if (tabIndex < tabs.Length)
					return tabs[tabIndex];
				return null;
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

		//we don't have any need to Git Status in settings
		public override bool IsWatching
		{
			get { return false; }
		}
	}
}