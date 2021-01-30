using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#pragma warning disable 618	//GitCredentials obsolete

namespace UniGit.Security
{
	[Serializable]
	public class GitCredentialsJson : IEnumerable<GitCredential>
	{
		[SerializeField, HideInInspector]
		private List<GitCredential> entries;

        public GitCredentialsJson()
		{
			entries = new List<GitCredential>();
		}

		public GitCredential GetEntry(string url)
		{
            return entries?.FirstOrDefault(r => r.URL.Equals(url, StringComparison.InvariantCultureIgnoreCase));
		}

		internal void AddEntry(GitCredential entry)
		{
			entries.Add(entry);
		}

		internal void RemoveEntry(GitCredential entry)
		{
			entries.Remove(entry);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<GitCredential> GetEnumerator()
		{
			return entries.GetEnumerator();
		}

		internal void Copy(GitCredentials gitCredentials)
		{
			entries.AddRange(gitCredentials.Select(e => new GitCredential(e)));
		}

		internal void MarkDirty()
		{
			IsDirty = true;
		}

		internal void ResetDirty()
		{
			IsDirty = false;
		}

		public List<GitCredential> Entries => entries;

        internal bool IsDirty { get; private set; }
    }
}