using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UniGit.Utils
{
	public class GitAboutWindow : EditorWindow
	{
		private GitOverlay gitOverlay;
		private string gitVersion;
		private Vector2 scroll;

		[UsedImplicitly]
		private void OnEnable()
		{
			GitWindows.AddWindow(this);
			GetGitVersion();
		}

		[UniGitInject]
		private void Construct(GitOverlay gitOverlay)
		{
			this.gitOverlay = gitOverlay;
		}

		private void GetGitVersion()
		{
			var process = new System.Diagnostics.Process();
			var startInfo = new System.Diagnostics.ProcessStartInfo
			{
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				FileName = "git",
				Arguments = "--version",
				UseShellExecute = false
			};
			process.StartInfo = startInfo;
			process.Start();
			gitVersion = process.StandardOutput.ReadLine();
			if (!string.IsNullOrEmpty(gitVersion))
				gitVersion = gitVersion.Replace("git version", "");
			process.Close();
			process.Dispose();
		}

		[UsedImplicitly]
		private void OnGUI()
		{
			var version = GlobalSettings.Version;

			GUILayout.Label(GitGUI.GetTempContent("UniGit"), "LODLevelNotifyText", GUILayout.ExpandWidth(true));

			GUILayout.BeginVertical("AC BoldHeader");
			GUILayout.Label(GitGUI.GetTempContent("Created by: Simeon Radivoev"));
            var packageInfo = PackageInfo.FindForAssembly(this.GetType().Assembly);
            GUILayout.Label(GitGUI.GetTempContent("UniGit Version: " + packageInfo.version));
			GUILayout.Label(GitGUI.GetTempContent("Git Version: " + gitVersion));
			GUILayout.Label(GitGUI.GetTempContent("LibGit2Sharp Features: " + version.Features));
			GUILayout.Label(GitGUI.GetTempContent("License:  GNU GENERAL PUBLIC LICENSE, Version 3, 29 June 2007"),EditorStyles.wordWrappedLabel);
			GUILayout.EndVertical();

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.GetTempContent("Donate", gitOverlay.icons.donateSmall.image, "Support UniGit"), "CN CountBadge"))
			{
				GitLinks.GoTo(GitLinks.Donate);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(GitGUI.GetTempContent("Source", gitOverlay.icons.starSmall.image, "View Source on GitHub"), "CN CountBadge"))
			{
				GitLinks.GoTo(GitLinks.Homepage);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(GitGUI.GetTempContent("License", "View UniGit License"), "CN CountBadge"))
			{
				GitLinks.GoTo(GitLinks.License);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			scroll = GUILayout.BeginScrollView(scroll);

			foreach (var credit in GitLinks.Credits)
			{
				if (!string.IsNullOrEmpty(credit.Item2))
				{
					if (GUILayout.Button(GitGUI.GetTempContent(credit.Item1),EditorStyles.centeredGreyMiniLabel))
					{
						GitLinks.GoTo(credit.Item2);
					}
					EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent(credit.Item1));
				}
			}

			GUILayout.EndScrollView();
		}
	}
}