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
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.CreateNoWindow = true;
			startInfo.RedirectStandardOutput = true;
			startInfo.FileName = "git";
			startInfo.Arguments = "--version";
			startInfo.UseShellExecute = false;
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

			GUILayout.Label(new GUIContent("UniGit"), "TL Selection H1");

			GUILayout.BeginVertical("IN GameObjectHeader");
			GUILayout.Label(new GUIContent("Created by: Simeon Radivoev"));
			GUILayout.Label(new GUIContent("UniGit Version: " + GitManager.Version));
			GUILayout.Label(new GUIContent("Git Version: " + gitVersion));
			GUILayout.Label(new GUIContent("LibGit2Sharp Version: " + version.InformationalVersion));
			GUILayout.Label(new GUIContent("LibGit2Sharp Features: " + version.Features));
			GUILayout.Label(new GUIContent("License:  GNU GENERAL PUBLIC LICENSE, Version 3, 29 June 2007"),EditorStyles.wordWrappedLabel);
			GUILayout.EndVertical();

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Donate", EditorGUIUtility.FindTexture("Collab"), "Support UniGit"), "CN CountBadge"))
			{
				Application.OpenURL(DonateUrl);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(new GUIContent("Source", "View Source on GitHub"), "CN CountBadge"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit");
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			EditorGUILayout.Space();
			if (GUILayout.Button(new GUIContent("License", "View UniGit License"), "CN CountBadge"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit/blob/master/LICENSE.md");
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}