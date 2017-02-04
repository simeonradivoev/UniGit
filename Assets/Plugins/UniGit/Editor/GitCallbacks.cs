using System;
using LibGit2Sharp;
using UniGit.Status;
using UnityEditor;

namespace UniGit
{
	public static class GitCallbacks
	{
		static GitCallbacks()
		{
			EditorApplication.update += IssueEditorUpdate;
		}

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

		public static event Action EditorUpdate;
		public static void IssueEditorUpdate()
		{
			if(EditorUpdate != null)
				EditorUpdate.Invoke();
		}
	}
}