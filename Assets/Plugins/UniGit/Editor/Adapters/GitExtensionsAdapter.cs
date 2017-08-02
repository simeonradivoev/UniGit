using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UniGit.Attributes;
using UniGit.Utils;

namespace UniGit.Adapters
{
	[ExternalAdapter("Git Extensions", "GitExtensions.exe",Priority = 5)]
	public class GitExtensionsAdapter : BaseExternalAdapter
	{
		[UniGitInject]
		public GitExtensionsAdapter(GitManager gitManager) : base(gitManager)
		{
			
		}

		public sealed override bool Push()
		{
			return CallProccess("GitExtensions.exe", "push");
		}

		public sealed override bool Reset(Commit commit)
		{
			return CallProccess("GitExtensions.exe", "reset");
		}

		public sealed override bool Merge()
		{
			return CallProccess("GitExtensions.exe", "mergetool");
		}

		public sealed override bool Pull()
		{
			return CallProccess("GitExtensions.exe", "pull");
		}

		public sealed override bool Commit(string message)
		{
			return CallProccess("GitExtensions.exe", "commit");
		}

		public sealed override bool Fetch(string remote)
		{
			return CallProccess("GitExtensions.exe", "pull --fetch");
		}

		public sealed override bool Conflict(string path)
		{
			return CallProccess("GitExtensions.exe", "mergeconflicts");
		}

		public sealed override bool Diff(string path)
		{
			return CallProccess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Diff(string path, string path2)
		{
			return CallProccess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Diff(string path, Commit start, Commit end)
		{
			return CallProccess("GitExtensions.exe", "filehistory " + path);
		}

		public sealed override bool Diff(string path, Commit end)
		{
			return CallProccess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Blame(string path)
		{
			return CallProccess("GitExtensions.exe", "blame " + path);
		}

		public sealed override bool Revert(IEnumerable<string> paths)
		{
			string path = paths.FirstOrDefault();
			if (path != null)
			{
				return CallProccess("GitExtensions.exe", "revert " + path);
			}
			return false;
		}

		public sealed override bool Switch()
		{
			return CallProccess("GitExtensions.exe", "checkout");
		}
	}
}