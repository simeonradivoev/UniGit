using System;
using System.Linq;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniGit
{
	public class GitSettingsWindow : GitUpdatableWindow, IHasCustomMenu
	{
		private const float AnimationDuration = 0.4f;
		private const string WindowTitle = "Git Settings";

		[NonSerialized] private VisualElement[] tabs;
		[NonSerialized] private ToolbarButton[] toolbarButtons;
		[SerializeField] private int tab;
		private int lastTabIndex = -1;
		private readonly InjectionHelper injectionHelper = new InjectionHelper();
		private GitAnimation gitAnimation;
		private GitAnimation.GitTween animationTween;

		#region Visual Elements

		private VisualElement settingsWindowElement;
		private VisualElement settingsTabsElement;
		private VisualElement tabsToolbar;
		private Button helpButton;
		private Button donateButton;

		#endregion

        [UniGitInject]
		private void Construct(InjectionHelper parentInjectionHelper,GitAnimation gitAnimation)
        {
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
					((GitSettingsTab)settingsTab.userData).Dispose();
				}
				tabs = null;
			}

			try
			{
				var tabsArray = injectionHelper.GetInstances<GitSettingsTab>().ToArray();
				tabs = tabsArray.Select(t => t.ConstructContents()).ToArray();
				toolbarButtons = tabsArray.Select(t => new ToolbarButton()).ToArray();

                for (int i = 0; i < tabsArray.Length; i++)
				{
					tabs[i].userData = tabsArray[i];
					tabsToolbar.Add(toolbarButtons[i]);
                    InitTabVisuals(i, tabsArray[i],tabs[i], toolbarButtons[i]);
				}
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"There was a problem while creating the settings window tabs.");
				logger.LogException(e);
			}
		}

		private void InitTabVisuals(int tabIndex, GitSettingsTab tabData,VisualElement tabVisualElement,ToolbarButton toolbarButton)
		{
			tabVisualElement.style.display = tabIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			tabVisualElement.style.position = Position.Absolute;
			tabVisualElement.style.top = 0;
			tabVisualElement.style.right = 0;
			tabVisualElement.style.left = 0;
			tabVisualElement.style.bottom = 0;
			settingsTabsElement.Add(tabVisualElement);

			toolbarButton.text = tabData.Name.text;
			toolbarButton.userData = tabIndex;
			toolbarButton.SetEnabled(tabIndex != 0);

            toolbarButton.clickable.clicked += () =>
			{
				if (this.tab != tabIndex)
				{
					lastTabIndex = this.tab;
					toolbarButtons[this.tab].SetEnabled(true);
					this.tab = tabIndex;
					toolbarButtons[tabIndex].SetEnabled(false);
                    animationTween = gitAnimation.StartAnimation(AnimationDuration, this,
						GitSettingsJson.AnimationTypeEnum.Settings);
				}
			};
		}

		protected override void OnInitialize()
		{
			if (!initializer.IsValidRepo) return;
			if (tabs == null)
			{
				InitTabs();
			}
			currentTab?.OnFocus();
			OnGitUpdate(null, null);
		}

		protected override void OnLostFocus()
		{
			base.OnLostFocus();
			currentTab?.OnLostFocus();
			LoseFocus();
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			Repaint();
		}

		protected override void OnEditorUpdate()
		{
			
		}

		protected override void Update()
		{
			base.Update();

			bool validRepo = gitManager != null && initializer.IsValidRepo;
            if (settingsWindowElement != null)
            {
                settingsWindowElement.style.display = validRepo && gitManager.Repository != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (validRepo)
			{
				AnimateTabs();
			}
        }

		private void AnimateTabs()
		{
			if (animationTween.Valid)
			{
				float animTime = GitAnimation.ApplyEasing(animationTween.Percent);
				int animDir = (int)Mathf.Sign(tab - lastTabIndex);

				if (lastTabElement != null)
				{
					float pos = (1 - animTime) * -animDir * settingsTabsElement.contentRect.width;

					lastTabElement.style.right = -pos;
					lastTabElement.style.left = pos;

					lastTabElement.style.display = DisplayStyle.Flex;
				}

				if (currentTabElement != null)
				{
					float pos = animTime * animDir * settingsTabsElement.contentRect.width;
					currentTabElement.style.right = -pos;
					currentTabElement.style.left = pos;
					currentTabElement.style.display = DisplayStyle.Flex;
				}
			}
			else
			{
				if (currentTabElement != null)
				{
					currentTabElement.style.display = DisplayStyle.Flex;
				}

				if (lastTabElement != null)
				{
					lastTabElement.style.display = DisplayStyle.None;
				}
			}
        }

		protected override void ConstructGUI(VisualElement root)
		{
			var uxml = resourceManager.LoadUniGitAsset<VisualTreeAsset>("Editor/UI/SettingsWindow.uxml");
			var uss = resourceManager.LoadUniGitAsset<StyleSheet>("Editor/UI/SettingsWindowSheet.uss");
			uxml.CloneTree(root);
			root.styleSheets.Add(uss);

            base.ConstructGUI(root);

            settingsWindowElement = root.Q("SettingsWindow");
            settingsTabsElement = root.Q("SettingsTabs");
            tabsToolbar = root.Q("TabsToolbar");
            helpButton = root.Q<Button>("Help");
            donateButton = root.Q<Button>("Donate");

            helpButton.clickable.clicked += () => GitLinks.GoTo(GitLinks.SettingsWindowHelp);
			donateButton.clickable.clicked += () => GitLinks.GoTo(GitLinks.Donate);
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
					((GitSettingsTab)settingsTab.userData).Dispose();
				}

				tabs = null;
			}
		}

		private VisualElement lastTabElement
		{
			get
			{
				if (tabs == null) return null;
				if (lastTabIndex < 0) return null;
				int tabIndex = lastTabIndex;
				if (tabIndex < tabs.Length)
					return tabs[tabIndex];
				return null;
			}
		}

		private VisualElement currentTabElement
		{
			get
			{
				if (tabs == null) return null;
				if (tab < 0) return null;
				int tabIndex = tab;
				if (tabIndex < tabs.Length)
					return tabs[tabIndex];
				return null;
			}
		}

        private GitSettingsTab lastTab => (GitSettingsTab)lastTabElement?.userData;

        private GitSettingsTab currentTab => (GitSettingsTab) currentTabElement?.userData;

        //we don't have any need to Git Status in settings
        public override bool IsWatching
		{
			get { return false; }
		}
	}
}