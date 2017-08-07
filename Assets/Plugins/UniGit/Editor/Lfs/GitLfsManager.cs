using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Filters;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitLfsManager : ISettingsAffector
	{
		private readonly bool isInstalled;
		private bool initilized;
		private readonly string version;
		private FilterRegistration lfsRegistration;
		private GitLfsTrackedInfo[] trackedInfo = new GitLfsTrackedInfo[0];
		private readonly GitManager gitManager;

		[UniGitInject]
		public GitLfsManager(GitManager gitManager,GitCallbacks callbacks)
		{
			this.gitManager = gitManager;
			callbacks.UpdateRepository += (s,p) => { UpdateInitilized(); };
			gitManager.AddSettingsAffector(this);

			try
			{
				version = GitHelper.RunExeOutput(gitManager.RepoPath, "git-lfs", "version", null);
				isInstalled = true;
			}
			catch (Exception)
			{
				isInstalled = false;
				return;
			}

			UpdateInitilized();
			if (Initilized)
			{
				RegisterFilter();
				Update();
			}
		}

		public void Update()
		{
			RegisterFilter();

			if (File.Exists(Path.Combine(gitManager.RepoPath, ".gitattributes")))
			{
				using (TextReader file = File.OpenText(Path.Combine(gitManager.RepoPath, ".gitattributes")))
				{
					trackedInfo = file.ReadToEnd().Split('\n').Select(l => GitLfsTrackedInfo.Parse(l)).Where(l => l != null).ToArray();
				}
			}

			UpdateInitilized();
		}

		private void UpdateInitilized()
		{
			initilized = CheckInitialized();
		}

		public void SaveTracking()
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

		private void RegisterFilter()
		{
			if (GlobalSettings.GetRegisteredFilters().All(f => f.Name != "lfs"))
			{
				var filteredFiles = new List<FilterAttributeEntry> {new FilterAttributeEntry("lfs")};
				var filter = new GitLfsFilter("lfs", filteredFiles,this, gitManager);
				GlobalSettings.RegisterFilter(filter);
			}
		}

		public bool Initialize()
		{
			string output = GitHelper.RunExeOutput(gitManager.RepoPath,"git-lfs", "install", null);
			
			if (!Directory.Exists(Path.Combine(gitManager.RepoPath,Path.Combine(".git","lfs"))))
			{
				Debug.LogError("Git-LFS install failed! (Try manually)");
				Debug.LogError(output);
				return false;
			}
			EditorUtility.DisplayDialog("Git LFS Initialized", output, "Ok");
			UpdateInitilized();
			return true;
		}

		public void Track(string extension)
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

		public void Untrack(string extension)
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

		#region Settings Affectos

		public void AffectThreading(ref GitSettingsJson.ThreadingType setting)
		{
			if (isInstalled && Initilized)
			{
				setting.ClearFlags(GitSettingsJson.ThreadingType.Stage);
				setting.ClearFlags(GitSettingsJson.ThreadingType.Unstage);
			}
		}

		#endregion

		public bool CheckInitialized()
		{
			return Directory.Exists(Path.Combine(gitManager.RepoPath, Path.Combine(".git", "lfs"))) && File.Exists(Path.Combine(gitManager.RepoPath,Path.Combine(".git",Path.Combine("hooks", "pre-push"))));
		}

		public bool Initilized
		{
			get { return initilized; }
		}

		public bool Installed
		{
			get { return isInstalled; }
		}

		public string Version
		{
			get { return version; }
		}

		public bool IsEnabled
		{
			get
			{
				return !gitManager.Settings.DisableGitLFS;
			}
		}

		public GitLfsTrackedInfo[] TrackedInfo
		{
			get { return trackedInfo; }
		}
	}
}