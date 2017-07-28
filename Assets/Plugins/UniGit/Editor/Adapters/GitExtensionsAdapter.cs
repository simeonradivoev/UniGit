using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UniGit.Attributes;
using UniGit.Utils;

namespace UniGit.Adapters
{
	[ExternalAdapter("Git Extensions", "GitExtensions.exe",Priority = 5)]
	public class GitExtensionsAdapter : IExternalAdapter
	{
		private readonly GitExternalManager externalManager;

		[UniGitInject]
		public GitExtensionsAdapter(GitManager gitManager,GitExternalManager externalManager)
		{
			this.externalManager = externalManager;
		}

		public bool Push()
		{
			return externalManager.CallProccess("GitExtensions.exe", "push");
		}

		public bool Reset(Commit commit)
		{
			return externalManager.CallProccess("GitExtensions.exe", "reset");
		}

		public bool Merge()
		{
			return externalManager.CallProccess("GitExtensions.exe", "mergetool");
		}

		public bool Pull()
		{
			return externalManager.CallProccess("GitExtensions.exe", "pull");
		}

		public bool Commit(string message)
		{
			return externalManager.CallProccess("GitExtensions.exe", "commit");
		}

		public bool Fetch(string remote)
		{
			return externalManager.CallProccess("GitExtensions.exe", "pull --fetch");
		}

		public bool Conflict(string path)
		{
			return externalManager.CallProccess("GitExtensions.exe", "mergeconflicts");
		}

		public bool Diff(string path)
		{
			return externalManager.CallProccess("GitExtensions.exe", "viewdiff");
		}

		public bool Diff(string path, string path2)
		{
			return externalManager.CallProccess("GitExtensions.exe", "viewdiff");
		}

		public bool Diff(string path, Commit start, Commit end)
		{
			return externalManager.CallProccess("GitExtensions.exe", "filehistory " + path);
		}

		public bool Diff(string path, Commit end)
		{
			return externalManager.CallProccess("GitExtensions.exe", "viewdiff");
		}

		public bool Blame(string path)
		{
			return externalManager.CallProccess("GitExtensions.exe", "blame " + path);
		}

		public bool Revert(IEnumerable<string> paths)
		{
			string path = paths.FirstOrDefault();
			if (path != null)
			{
				return externalManager.CallProccess("GitExtensions.exe", "revert " + path);
			}
			return false;
		}

		public bool Switch()
		{
			return externalManager.CallProccess("GitExtensions.exe", "checkout");
		}
	}
}