using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UniGit.Attributes;

namespace UniGit.Adapters
{
	[ExternalAdapter("Git Extensions", "GitExtensions.exe",Priority = 5)]
	public class GitExtensionsAdapter : IExternalAdapter
	{
		public bool Push()
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "push");
		}

		public bool Reset(Commit commit)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "reset");
		}

		public bool Merge()
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "mergetool");
		}

		public bool Pull()
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "pull");
		}

		public bool Commit(string message)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "commit");
		}

		public bool Fetch(string remote)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "pull --fetch");
		}

		public bool Conflict(string path)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "mergeconflicts");
		}

		public bool Diff(string path)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "viewdiff");
		}

		public bool Diff(string path, string path2)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "viewdiff");
		}

		public bool Diff(string path, Commit start, Commit end)
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "filehistory " + path);
		}

		public bool Revert(IEnumerable<string> paths)
		{
			string path = paths.FirstOrDefault();
			if (path != null)
			{
				return GitExternalManager.CallProccess("GitExtensions.exe", "revert " + path);
			}
			return false;
		}

		public bool Switch()
		{
			return GitExternalManager.CallProccess("GitExtensions.exe", "checkout");
		}
	}
}