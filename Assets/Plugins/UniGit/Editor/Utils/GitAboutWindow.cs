using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitAboutWindow : EditorWindow
	{
		private GitOverlay gitOverlay;
		private string gitVersion;

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
			System.Diagnostics.Process process = new System.Diagnostics.Process();
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
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

			GUILayout.Label(GitGUI.GetTempContent("UniGit"), "TL Selection H1");

			GUILayout.BeginVertical("IN GameObjectHeader");
			GUILayout.Label(GitGUI.GetTempContent("Created by: Simeon Radivoev"));
			GUILayout.Label(GitGUI.GetTempContent("UniGit Version: " + GitManager.Version));
			GUILayout.Label(GitGUI.GetTempContent("Git Version: " + gitVersion));
			GUILayout.Label(GitGUI.GetTempContent("LibGit2Sharp Version: " + version.InformationalVersion));
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
		}
	}
}