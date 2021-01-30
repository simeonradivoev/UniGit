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
				Version = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(), "git-lfs", "version", null);
				Installed = true;
			}
			catch (Exception)
			{
				Installed = false;
				return;
			}

			UpdateInitilized();
            if (!Initialized) return;
            RegisterFilter();
            Update();
        }

		private void OnUpdateRepository(GitRepoStatus status, string[] paths)
		{
			UpdateInitilized();
		}

		public void Update()
		{
			RegisterFilter();

			if (File.Exists(UniGitPathHelper.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes")))
            {
                using TextReader file = File.OpenText(UniGitPathHelper.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes"));
                TrackedInfo = file.ReadToEnd().Split(UniGitPathHelper.NewLineChar).Select(GitLfsTrackedInfo.Parse).Where(l => l != null).ToArray();
            }

			UpdateInitilized();
		}

		private void UpdateInitilized()
		{
			Initialized = CheckInitialized();
		}

		public void SaveTracking()
		{
			using (var file = File.CreateText(UniGitPathHelper.Combine(gitManager.GetCurrentRepoPath(), ".gitattributes")))
			{
				foreach (var info in TrackedInfo)
				{
					file.WriteLine(info.ToString());
				}
			}

			Update();
		}

		private void RegisterFilter()
        {
            if (GlobalSettings.GetRegisteredFilters().Any(f => f.Name == "lfs")) return;
            var filteredFiles = new List<FilterAttributeEntry> {new FilterAttributeEntry("lfs")};
            var filter = new GitLfsFilter("lfs", filteredFiles,this, gitManager,logger);
            GlobalSettings.RegisterFilter(filter);
        }

		public bool Initialize()
		{
			var output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs", "install", null);
			
			if (!Directory.Exists(UniGitPathHelper.Combine(gitManager.GetCurrentDotGitFolder(),"lfs")))
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
				var output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs",$"track \"*{extension}\"", null);
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
				var output = GitHelper.RunExeOutput(gitManager.GetCurrentRepoPath(),"git-lfs",$"track \"*{extension}\"", null);
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
			if (Installed && Initialized)
			{
				setting.ClearFlags(GitSettingsJson.ThreadingType.Stage);
				setting.ClearFlags(GitSettingsJson.ThreadingType.Unstage);
			}
		}

		#endregion

		public bool CheckInitialized()
		{
			return Directory.Exists(UniGitPathHelper.Combine(gitManager.GetCurrentDotGitFolder(), "lfs")) && File.Exists(UniGitPathHelper.Combine(gitManager.GetCurrentDotGitFolder(),"hooks", "pre-push"));
		}

		public void Dispose()
		{
			if(gitCallbacks != null) gitCallbacks.UpdateRepository -= OnUpdateRepository;
            gitManager?.RemoveSettingsAffector(this);
        }

		public bool Initialized { get; private set; }

        public bool Installed { get; }

        public string Version { get; }

        public bool IsEnabled => !gitSettings.DisableGitLFS;

        public GitLfsTrackedInfo[] TrackedInfo { get; private set; } = new GitLfsTrackedInfo[0];
    }
}