using System;
using LibGit2Sharp;
using UniGit.Status;

namespace UniGit
{
	public static class GitCallbacks
	{
		public static event Action<GitRepoStatus, string[]> UpdateRepository;

		public static void IssueUpdateRepository(GitRepoStatus repoStatus, string[] paths)
		{
			if (UpdateRepository != null)
				UpdateRepository.Invoke(repoStatus, paths);
		}

		public static event Action<Repository> OnRepositoryLoad;

		public static void IssueOnRepositoryLoad(Repository repository)
		{
			if(OnRepositoryLoad != null)
				OnRepositoryLoad.Invoke(repository);
		}
	}
}