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

		public string Path => path;

        public string Url => url;

        public string WorkDirId => workDirId;

        public SubmoduleStatus Status
		{
			get => status;
            set => status = value;
        }
	}
}
