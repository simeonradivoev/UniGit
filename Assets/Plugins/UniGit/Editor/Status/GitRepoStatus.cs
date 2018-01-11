using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public class GitRepoStatus : IEnumerable<GitStatusEntry>
	{
		[SerializeField] private List<GitStatusEntry> entries = new List<GitStatusEntry>();
		private object lockObj;

		public GitRepoStatus()
		{
			lockObj = new object();
		}

		public void Clear()
		{
			entries.Clear();
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

		public bool Get(string path,out GitStatusEntry entry)
		{
			foreach (var e in entries)
			{
				if (e.Path == path)
				{
					entry = e;
					return true;
				}
			}

			entry = new GitStatusEntry();
			return false;
		}

		public bool TryEnterLock()
		{
			return Monitor.TryEnter(lockObj);
		}

		public void Lock()
		{
			Monitor.Enter(lockObj);
		}

		public void Unlock()
		{
			Monitor.Exit(lockObj);
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