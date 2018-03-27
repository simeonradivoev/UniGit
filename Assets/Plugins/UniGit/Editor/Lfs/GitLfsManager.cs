using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Filters;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitLfsManager : ISettingsAffector, IDisposable
	{
		private readonly bool isInstalled;
		private bool initilized;
		private readonly string version;
		private GitLfsTrackedInfo[] trackedInfo = new GitLfsTrackedInfo[0];
		private readonly GitManager gitManager;
		private readonly GitCallbacks gitCallbacks;
		private readonly ILogger logger;
		private readonly GitSettingsJson gitSettings;

		[UniGitInject]
		public GitLfsManager(GitManager gitManager,GitCallbacks gitCallbacks,ILogger logger,GitSettingsJson gitSettings)
		{
			this.gitManager = gitManager;
			this.gitCallbacks = gitCallbacks;
			this.logger = logger;
			this.gitSettings = gitSettings;
			gitCallbacks.UpdateRepository += OnUpdateRepository;
			gitManager.AddSettingsAffector(this);

			try
			{
				version = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(), "git-lfs", "version", null);
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

		private void OnUpdateRepository(GitRepoStatus status, string[] paths)
		{
			UpdateInitilized();
		}

		public void Update()
		{
			RegisterFilter();

			if (File.Exists(UniGitPath.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes")))
			{
				using (TextReader file = File.OpenText(UniGitPath.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes")))
				{
					trackedInfo = file.ReadToEnd().Split(UniGitPath.NewLineChar).Select(GitLfsTrackedInfo.Parse).Where(l => l != null).ToArray();
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
			using (StreamWriter file = File.CreateText(UniGitPath.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes")))
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
				var filter = new GitLfsFilter("lfs", filteredFiles,this, gitManager,logger);
				GlobalSettings.RegisterFilter(filter);
			}
		}

		public bool Initialize()
		{
			string output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs", "install", null);
			
			if (!Directory.Exists(UniGitPath.Combine(gitManager.GetCurrentDotGitFolder(),"lfs")))
			{
				logger.Log(LogType.Error,"Git-LFS install failed! (Try manually)");
				logger.Log(LogType.Error,output);
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
				string output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs", string.Format("track \"*{0}\"", extension), null);
				EditorUtility.DisplayDialog("Track File", output, "Ok");
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"There was a problem while trying to track an extension");
				logger.LogException(e);
			}
		}

		public void Untrack(string extension)
		{
			try
			{
				string output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs", string.Format("track \"*{0}\"", extension), null);
				EditorUtility.DisplayDialog("Untrack File", output, "Ok");
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"There was a problem while trying to untrack an extension");
				logger.LogException(e);
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
			return Directory.Exists(UniGitPath.Combine(gitManager.GetCurrentDotGitFolder(), "lfs")) && File.Exists(UniGitPath.Combine(gitManager.GetCurrentDotGitFolder(),"hooks", "pre-push"));
		}

		public void Dispose()
		{
			if(gitCallbacks != null) gitCallbacks.UpdateRepository -= OnUpdateRepository;
			if(gitManager != null) gitManager.RemoveSettingsAffector(this);
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
				return !gitSettings.DisableGitLFS;
			}
		}

		public GitLfsTrackedInfo[] TrackedInfo
		{
			get { return trackedInfo; }
		}
	}
}