using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace UniGit.Status
{
	public class GitRepoStatus : IEnumerable<GitStatusEntry>
	{
		private readonly List<GitStatusEntry> entries = new List<GitStatusEntry>();

		public GitRepoStatus(RepositoryStatus status)
		{
			entries.AddRange(status.Select(e => new GitStatusEntry(e.FilePath,e.State)));
		}

		public void Combine(RepositoryStatus other)
		{
			foreach (var otherEntry in other)
			{
				Update(otherEntry.FilePath, otherEntry.State);
			}
		}

		public void Update(GitStatusEntry status)
		{
			entries.RemoveAll(e => e.Path == status.Path);
			entries.Add(status);
		}

		public void Update(string filePath,FileStatus status)
		{
			entries.RemoveAll(e => e.Path == filePath);
			if (status != FileStatus.Nonexistent)
			{
				entries.Add(new GitStatusEntry(filePath, status));
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<GitStatusEntry> GetEnumerator()
		{
			return entries.GetEnumerator();
		}
	}
}