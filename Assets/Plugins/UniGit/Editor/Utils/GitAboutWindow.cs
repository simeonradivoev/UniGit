using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitAboutWindow : EditorWindow
	{
		public const string DonateUrl = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=4A4LQGA69LQ5A";
		private string gitVersion;

		private void OnEnable()
		{
			GetGitVersion();
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
			if (GUILayout.Button(GitGUI.GetTempContent("Donate", GitGUI.Styles.Collab, "Support UniGit"), "CN CountBadge"))
			{
				Application.OpenURL(DonateUrl);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(GitGUI.GetTempContent("Source", "View Source on GitHub"), "CN CountBadge"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit");
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(GitGUI.GetTempContent("License", "View UniGit License"), "CN CountBadge"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit/blob/master/LICENSE.md");
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}