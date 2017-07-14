using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitLFSSettingsTab : GitSettingsTab
	{
		private Rect trackFileRect;

		public GitLFSSettingsTab(GitManager gitManager, GitSettingsWindow settingsWindow) : base(gitManager,settingsWindow)
		{
		}

		internal override void OnGUI(Rect rect, Event current)
		{
			if (!GitLfsManager.Installed)
			{
				EditorGUILayout.HelpBox("Git LFS not installed", MessageType.Warning);
				if (GUILayout.Button(GitGUI.GetTempContent("Download")))
				{
					Application.OpenURL("https://git-lfs.github.com/");
				}
			}
			else
			{
				if (!GitLfsManager.CheckInitialized())
				{
					EditorGUILayout.HelpBox("Git LFS not Initialized", MessageType.Info);
					if (GUILayout.Button(GitGUI.GetTempContent("Initialize")))
					{
						GitLfsManager.Initialize();
					}
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent("Settings"), "ProjectBrowserHeaderBgTop");

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

					foreach (var info in GitLfsManager.TrackedInfo)
					{
						GUILayout.Label(GitGUI.GetTempContent(info.Extension), "ShurikenModuleTitle");
						GUI.SetNextControlName(info.GetHashCode() + " Extension");
						info.Extension = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Extension"), info.Extension);
						GUI.SetNextControlName(info.GetHashCode() + " Type");
						info.Type = (GitLfsTrackedInfo.TrackType)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Type"), info.Type);

						if (info.IsDirty)
						{
							GitLfsManager.SaveTracking();
							break;
						}
					}

					if (GUILayout.Button("Track File"))
					{
						PopupWindow.Show(trackFileRect, new GitLfsTrackPopupWindow(settingsWindow));
					}
					if (current.type == EventType.Repaint)
					{
						trackFileRect = GUILayoutUtility.GetLastRect();
					}
				}
			}

			EditorGUILayout.HelpBox("Git LFS is still in development, and is recommended to use an external program for handling it.", MessageType.Info);
		}
	}
}