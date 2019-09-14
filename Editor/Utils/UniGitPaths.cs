namespace UniGit.Utils
{
	public class UniGitPaths
	{
		public string RepoPath { get; private set; }

        public string RepoProjectRelativePath { get; private set; }

		public string SettingsFilePath { get; private set; }

		public string SettingsFolderPath { get; private set; }

		public string LogsFilePath { get; private set; }

		public string LogsFolderPath { get; private set; }

		public string CredentialsFilePath { get; private set; }

        public string ProjectPath { get; private set; }

		/// <summary>
        /// The .git folder for the repository.
        /// </summary>
		public string GitPath { get; private set; }

		public UniGitPaths(string repoPath,string projectPath)
        {
            this.ProjectPath = projectPath;
            SetRepoPath(repoPath);
		}

		public void SetRepoPath(string repoPath)
		{
			this.RepoPath = repoPath;

            if (!UniGitPathHelper.PathsEqual(repoPath, ProjectPath))
            {
                RepoProjectRelativePath = UniGitPathHelper.SubtractDirectory(repoPath, ProjectPath);
            }

            SettingsFolderPath = UniGitPathHelper.Combine(repoPath, ".git", "UniGit");
            SettingsFilePath = UniGitPathHelper.Combine(SettingsFolderPath, "Settings.json");
            LogsFolderPath = SettingsFolderPath;
            LogsFilePath = UniGitPathHelper.Combine(LogsFolderPath, "log.txt");
            CredentialsFilePath = UniGitPathHelper.Combine(SettingsFolderPath, "Credentials.json");
            GitPath = UniGitPathHelper.Combine(RepoPath, ".git");
		}
	}
}