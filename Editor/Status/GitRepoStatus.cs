using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public class GitRepoStatus : IEnumerable<GitStatusEntry>
	{
		[SerializeField] private List<GitStatusEntry> entries = new List<GitStatusEntry>();
		[SerializeField] private List<GitStatusSubModuleEntry> subModuleEntries = new List<GitStatusSubModuleEntry>();
		[SerializeField] private List<GitStatusRemoteEntry> remoteEntries = new List<GitStatusRemoteEntry>();

        public GitRepoStatus()
		{
			LockObj = new object();
		}

		public void Clear()
		{
			entries.Clear();
			subModuleEntries.Clear();
			remoteEntries.Clear();
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
			entries.RemoveAll(e => e.LocalPath == status.LocalPath);
			entries.Add(status);
		}

		public void Add(GitStatusSubModuleEntry status)
		{
			subModuleEntries.Add(status);
		}

		public void Add(GitStatusRemoteEntry remoteEntry)
		{
			remoteEntries.Add(remoteEntry);
		}

		public void Update(string localFilePath,FileStatus status)
		{
			entries.RemoveAll(e => e.LocalPath == localFilePath);
			if (status != FileStatus.Nonexistent)
			{
				entries.Add(new GitStatusEntry(localFilePath, status));
			}
		}

		public void Update(string path,SubmoduleStatus status)
		{
			var entry = subModuleEntries.FirstOrDefault(e => e.Path == path);
			if (entry != null)
			{
				entry.Status = status;
			}
		}

		public bool Get(string localPath,out GitStatusEntry entry)
		{
			foreach (var e in entries)
            {
                if (e.LocalPath != localPath) continue;
                entry = e;
                return true;
            }

			entry = new GitStatusEntry();
			return false;
		}

		public object LockObj { get; }

        public IEnumerable<GitStatusSubModuleEntry> SubModuleEntries => subModuleEntries;

        public IEnumerable<GitStatusRemoteEntry> RemoteEntries => remoteEntries;

        IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

        IEnumerator<GitStatusEntry> IEnumerable<GitStatusEntry>.GetEnumerator()
		{
			return GetEnumerator();
		}

        public List<GitStatusEntry>.Enumerator GetEnumerator()
        {
            return entries.GetEnumerator();
        }
	}
}