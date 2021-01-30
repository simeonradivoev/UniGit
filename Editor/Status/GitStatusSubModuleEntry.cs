using System;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public class GitStatusSubModuleEntry
	{
		[SerializeField] private string path;
		[SerializeField] private string url;
		[SerializeField] private SubmoduleStatus status;
		[SerializeField] private string workDirId;

		public GitStatusSubModuleEntry(Submodule submodule)
		{
			path = UniGitPathHelper.FixUnityPath(submodule.Path);
			url = submodule.Url;
			workDirId = submodule.WorkDirCommitId?.Sha;
			status = submodule.RetrieveStatus();
		}

		public GitStatusSubModuleEntry(string path)
		{
			this.path = path;
		}

		public string Path
		{
			get { return path; }
		}

		public string Url
		{
			get { return url; }
		}

		public string WorkDirId
		{
			get { return workDirId; }
		}

		public SubmoduleStatus Status
		{
			get { return status; }
			set { status = value; }
		}
	}
}
