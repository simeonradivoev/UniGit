using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitLFSSettingsTab : GitSettingsTab
	{
		private Vector2 scroll;
		private Rect trackFileRect;
		private readonly GitLfsManager lfsManager;
		private readonly InjectionHelper injectionHelper;

		[UniGitInject]
		public GitLFSSettingsTab(GitManager gitManager, 
			GitSettingsWindow settingsWindow,
			GitLfsManager lfsManager,
			InjectionHelper injectionHelper,
			UniGitData data,
			GitSettingsJson gitSettings,
			GitCallbacks gitCallbacks,
			GitInitializer initializer) 
			: base(new GUIContent("LFS", "Git Large File Storage (beta)"), gitManager,settingsWindow,data,gitSettings,gitCallbacks,initializer)
		{
			this.injectionHelper = injectionHelper;
			this.lfsManager = lfsManager;
		}

		internal override void OnGUI(Rect rect, Event current)
		{

			if (!lfsManager.Installed)
			{
				EditorGUILayout.HelpBox("Git LFS not installed", MessageType.Warning);
				if (GUILayout.Button(GitGUI.GetTempContent("Download")))
				{
					GitLinks.GoTo(GitLinks.GitLFS);
				}
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				gitSettings.DisableGitLFS = EditorGUILayout.Toggle(GitGUI.GetTempContent("Disable Git LFS"), gitSettings.DisableGitLFS);
				if (EditorGUI.EndChangeCheck())
				{
					gitSettings.MarkDirty();
				}

				if (!lfsManager.CheckInitialized())
				{
					EditorGUILayout.HelpBox("Git LFS not Initialized", MessageType.Info);
					if (GUILayout.Button(GitGUI.GetTempContent("Initialize")))
					{
						lfsManager.Initialize();
					}
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent("Settings"), GitGUI.Styles.ProjectBrowserHeaderBgTop);

					using (Configuration c = Configuration.BuildFrom(gitManager.RepoPath))
					{
						string url = c.GetValueOrDefault("lfs.url", "");
						if (string.IsNullOrEmpty(url))
						{
							EditorGUILayout.HelpBox("You should specify a LFS server URL", MessageType.Warning);
						}

						GitGUI.DoConfigStringField(c, GitGUI.GetTempContent("URL"), "lfs.url", "");
					}

					EditorGUILayout.Space();

					scroll = EditorGUILayout.BeginScrollView(scroll);
					foreach (var info in lfsManager.TrackedInfo)
					{
						GUILayout.Label(GitGUI.GetTempContent(info.Extension), GitGUI.Styles.ShurikenModuleTitle);
						GUI.SetNextControlName(info.GetHashCode() + " Extension");
						info.Extension = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Extension"), info.Extension);
						GUI.SetNextControlName(info.GetHashCode() + " Type");
						info.Type = (GitLfsTrackedInfo.TrackType)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Type"), info.Type);

						if (info.IsDirty)
						{
							lfsManager.SaveTracking();
							break;
						}
					}

					EditorGUILayout.EndScrollView();

					if (GUILayout.Button("Track File"))
					{
						PopupWindow.Show(trackFileRect, injectionHelper.CreateInstance<GitLfsTrackPopupWindow>());
					}
					if (current.type == EventType.Repaint)
					{
						trackFileRect = GUILayoutUtility.GetLastRect();
					}
				}
			}

			EditorGUILayout.HelpBox("Git LFS is still in development, and is recommended to use an external program for handling it.", MessageType.Warning);
		}
	}
}