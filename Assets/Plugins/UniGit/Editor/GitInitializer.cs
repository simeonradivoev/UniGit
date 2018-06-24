using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitInitializer
	{
		private readonly string repoPath;
		private readonly string gitPath;
		private readonly ILogger logger;
		private readonly GitCallbacks callbacks;

		[UniGitInject]
		public GitInitializer(string repoPath,
			ILogger logger,
			GitCallbacks callbacks)
		{
			this.repoPath = repoPath;
			this.logger = logger;
			this.callbacks = callbacks;
			gitPath = UniGitPath.Combine(repoPath, ".git");
		}

		public void InitializeRepository()
		{
			Repository.Init(repoPath);
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
			callbacks.IssueAssetDatabaseRefresh();
			callbacks.IssueSaveDatabaseRefresh();
			callbacks.IssueRepositoryCreate();
			//Update(true);
		}

		internal static void Recompile()
		{
			var importer = PluginImporter.GetAllImporters().FirstOrDefault(i => i.assetPath.EndsWith("UniGitResources.dll"));
			if (importer == null)
			{
				Debug.LogError("Could not find LibGit2Sharp.dll. You will have to close and open Unity to recompile scripts.");
				return;
			}
			importer.SetCompatibleWithEditor(true);
			importer.SaveAndReimport();
		}

		public  bool IsValidRepo
		{
			get { return Repository.IsValid(repoPath); }
		}

		public string GitSettingsFolderPath
		{
			get { return UniGitPath.Combine(gitPath, Path.Combine("UniGit", "Settings")); }
		}

		public string GetCommitMessageFilePath(string subModule)
		{
			if(!string.IsNullOrEmpty(subModule))
				return UniGitPath.Combine(gitPath, "UniGit", "Settings", string.Format("CommitMessage_{0}.txt",subModule));
			return UniGitPath.Combine(gitPath, "UniGit", "Settings", "CommitMessage.txt");
		}

		public string GitIgnoreFilePath
		{
			get { return UniGitPath.Combine(repoPath, ".gitignore"); }
		}
	}
}
