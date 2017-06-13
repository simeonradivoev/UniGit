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
		internal static void IssueUpdateRepository(GitRepoStatus repoStatus, string[] paths)
		{
			if (UpdateRepository != null)
				UpdateRepository.Invoke(repoStatus, paths);
		}

		public static event Action<Repository> OnRepositoryLoad;
		internal static void IssueOnRepositoryLoad(Repository repository)
		{
			if(OnRepositoryLoad != null)
				OnRepositoryLoad.Invoke(repository);
		}

		internal static event Action EditorUpdate;
		public static void IssueEditorUpdate()
		{
			if(EditorUpdate != null)
				EditorUpdate.Invoke();
		}

		public static event Action UpdateRepositoryStart;
		internal static void IssueUpdateRepositoryStart()
		{
			if(UpdateRepositoryStart != null)
				UpdateRepositoryStart.Invoke();
		}

		public static event Action UpdateRepositoryFinish;
		internal static void IssueUpdateRepositoryFinish()
		{
			if(UpdateRepositoryFinish != null)
				UpdateRepositoryFinish.Invoke();
		}
	}
}