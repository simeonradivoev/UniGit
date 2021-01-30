using System;
using LibGit2Sharp;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public struct GitStatusEntry
	{
		[SerializeField] private string localPath;
		[SerializeField] private FileStatus status;

		public GitStatusEntry(string localPath, FileStatus status)
		{
			this.localPath = localPath;
			this.status = status;
		}

		public string LocalPath => localPath;

        public FileStatus Status => status;
    }
}