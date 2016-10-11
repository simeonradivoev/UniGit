using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using UniGit.Filters;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitLfsManager
	{
		private static bool isInstalled;
		private static string version;
		private static FilterRegistration lfsRegistration;
		private static GitLfsTrackedInfo[] trackedInfo = new GitLfsTrackedInfo[0];

		internal static void Load()
		{
			try
			{
				version = GitHelper.RunExeOutput("git-lfs", "version", null);
				isInstalled = true;
			}
			catch (Exception)
			{
				isInstalled = false;
				return;
			}

			if (CheckInitialized())
			{
				RegisterFilter();
			}

			Update();
		}

		public static void Update()
		{
			RegisterFilter();

			if (File.Exists(GitManager.RepoPath + @"\.gitattributes"))
			{
				using (TextReader file = File.OpenText(GitManager.RepoPath + @"\.gitattributes"))
				{
					trackedInfo = file.ReadToEnd().Split('\n').Select(l => GitLfsTrackedInfo.Parse(l)).Where(l => l != null).ToArray();
				}
			}
		}

		public static void SaveTracking()
		{
			using (StreamWriter file = File.CreateText(GitManager.RepoPath + @"\.gitattributes"))
			{
				foreach (var info in trackedInfo)
				{
					file.WriteLine(info.ToString());
				}
			}

			Update();
		}

		private static void RegisterFilter()
		{
			if (GlobalSettings.GetRegisteredFilters().All(f => f.Name != "lfs"))
			{
				var filteredFiles = new List<FilterAttributeEntry>
			{
				new FilterAttributeEntry("lfs")
			};
				var filter = new GitLfsFilter("lfs", filteredFiles);
				GlobalSettings.RegisterFilter(filter);
			}
		}

		public static bool Initialize()
		{
			string output = GitHelper.RunExeOutput("git-lfs", "install", null);
			
			if (!Directory.Exists(GitManager.RepoPath + "\\.git\\lfs"))
			{
				Debug.LogError("Git-LFS install failed! (Try manually)");
				Debug.LogError(output);
				return false;
			}
			EditorUtility.DisplayDialog("Git LFS Initialized", output, "Ok");
			return true;
		}

		public static void Track(string extension)
		{
			try
			{
				string output = GitHelper.RunExeOutput("git-lfs", string.Format("track \"*{0}\"", extension), null);
				EditorUtility.DisplayDialog("Track File", output, "Ok");
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("There was a problem while trying to track an extension");
				Debug.LogException(e);
			}
		}

		public static void Untrack(string extension)
		{
			try
			{
				string output = GitHelper.RunExeOutput("git-lfs", string.Format("track \"*{0}\"", extension), null);
				EditorUtility.DisplayDialog("Untrack File", output, "Ok");
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("There was a problem while trying to untrack an extension");
				Debug.LogException(e);
			}
		}

		public static bool CheckInitialized()
		{
			return Directory.Exists(GitManager.RepoPath + @"\.git\lfs") && File.Exists(GitManager.RepoPath + @"\.git\hooks\pre-push");
		}

		public static bool Installed
		{
			get { return isInstalled; }
		}

		public static string Version
		{
			get { return version; }
		}

		public static GitLfsTrackedInfo[] TrackedInfo
		{
			get { return trackedInfo; }
		}
	}
}