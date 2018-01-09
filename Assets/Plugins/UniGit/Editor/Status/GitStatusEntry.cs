using System;
using LibGit2Sharp;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public struct GitStatusEntry
	{
		[SerializeField] private string path;
		[SerializeField] private FileStatus status;

		public GitStatusEntry(string path, FileStatus status)
		{
			this.path = path;
			this.status = status;
		}

		public string Path
		{
			get { return path; }
		}

		public FileStatus Status
		{
			get { return status; }
		}
	}
}