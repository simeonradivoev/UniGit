using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		private static GitManager gitManager;

		internal static void Load(GitManager gitManager)
		{
			GitLfsManager.gitManager = gitManager;

			try
			{
				version = GitHelper.RunExeOutput(gitManager.RepoPath,"git-lfs", "version", null);
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
				Update();

			}
		}

		public static void Update()
		{
			RegisterFilter();

			if (File.Exists(Path.Combine(gitManager.RepoPath, ".gitattributes")))
			{
				using (TextReader file = File.OpenText(Path.Combine(gitManager.RepoPath, ".gitattributes")))
				{
					trackedInfo = file.ReadToEnd().Split('\n').Select(l => GitLfsTrackedInfo.Parse(l)).Where(l => l != null).ToArray();
				}
			}
		}

		public static void SaveTracking()
		{
			using (StreamWriter file = File.CreateText(Path.Combine(gitManager.RepoPath, ".gitattributes")))
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
				var filteredFiles = new List<FilterAttributeEntry>();
				filteredFiles.Add(new FilterAttributeEntry("lfs"));
				var filter = new GitLfsFilter("lfs", filteredFiles, gitManager);
				GlobalSettings.RegisterFilter(filter);
			}
		}

		public static bool Initialize()
		{
			string output = GitHelper.RunExeOutput(gitManager.RepoPath,"git-lfs", "install", null);
			
			if (!Directory.Exists(Path.Combine(gitManager.RepoPath,Path.Combine(".git","lfs"))))
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
				string output = GitHelper.RunExeOutput(gitManager.RepoPath,"git-lfs", string.Format("track \"*{0}\"", extension), null);
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
				string output = GitHelper.RunExeOutput(gitManager.RepoPath,"git-lfs", string.Format("track \"*{0}\"", extension), null);
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
			return Directory.Exists(Path.Combine(gitManager.RepoPath, Path.Combine(".git", "lfs"))) && File.Exists(Path.Combine(gitManager.RepoPath,Path.Combine(".git",Path.Combine("hooks", "pre-push"))));
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