using System;
using LibGit2Sharp;
using UniGit.Status;
using UnityEditor;

namespace UniGit
{
	public class GitCallbacks
	{
		public event Action<GitRepoStatus, string[]> UpdateRepository;
		public void IssueUpdateRepository(GitRepoStatus repoStatus, string[] paths)
		{
			if (UpdateRepository != null)
				UpdateRepository.Invoke(repoStatus, paths);
		}

		public event Action<Repository> OnRepositoryLoad;
		public void IssueOnRepositoryLoad(Repository repository)
		{
			if(OnRepositoryLoad != null)
				OnRepositoryLoad.Invoke(repository);
		}

		public event Action EditorUpdate;
		public void IssueEditorUpdate()
		{
			if(EditorUpdate != null)
				EditorUpdate.Invoke();
		}

		public event Action UpdateRepositoryStart;
		public void IssueUpdateRepositoryStart()
		{
			if(UpdateRepositoryStart != null)
				UpdateRepositoryStart.Invoke();
		}

		public event Action UpdateRepositoryFinish;
		public void IssueUpdateRepositoryFinish()
		{
			if(UpdateRepositoryFinish != null)
				UpdateRepositoryFinish.Invoke();
		}
	}
}