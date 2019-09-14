using System.IO;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEngine;

namespace UniGit
{
	public class GitInitializer
	{
		private readonly UniGitPaths paths;
		private readonly ILogger logger;
		private readonly GitCallbacks callbacks;

		[UniGitInject]
		public GitInitializer(
			UniGitPaths paths,
			ILogger logger,
			GitCallbacks callbacks)
		{
			this.paths = paths;
			this.logger = logger;
			this.callbacks = callbacks;
		}

		public void InitializeRepository()
		{
			Repository.Init(paths.RepoPath);
			Directory.CreateDirectory(GitSettingsFolderPath);
			string newGitIgnoreFile = GitIgnoreFilePath;
			if (!File.Exists(newGitIgnoreFile))
			{
				File.WriteAllText(newGitIgnoreFile, GitIgnoreTemplate.Template);
			}
			else
			{
				logger.Log(LogType.Log,"Git Ignore file already present");
			}

			logger.Log(LogType.Log,"Repository Initialized");
			//Initialize();
		}

		internal void InitializeRepositoryAndRecompile()
		{
			InitializeRepository();
			RecompileSoft();
			//Update(true);
		}

		internal void RecompileSoft()
		{
			callbacks.IssueAssetDatabaseRefresh();
			callbacks.IssueSaveDatabaseRefresh();
			callbacks.IssueRepositoryCreate();
        }

		public  bool IsValidRepo => !string.IsNullOrEmpty(paths.RepoPath) && Repository.IsValid(paths.RepoPath);

		public string GitSettingsFolderPath => UniGitPathHelper.Combine(paths.GitPath, Path.Combine("UniGit", "Settings"));

		public string GetCommitMessageFilePath(string subModule)
		{
			if(!string.IsNullOrEmpty(subModule))
				return UniGitPathHelper.Combine(paths.GitPath, "UniGit", "Settings", $"CommitMessage_{UniGitPathHelper.GetFriendlyNameFromPath(subModule)}.txt");
			return UniGitPathHelper.Combine(paths.GitPath, "UniGit", "Settings", "CommitMessage.txt");
		}

		public string GitIgnoreFilePath => UniGitPathHelper.Combine(paths.RepoPath, ".gitignore");
	}
}
