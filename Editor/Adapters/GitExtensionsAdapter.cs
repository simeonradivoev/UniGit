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
		public GitExtensionsAdapter(GitManager gitManager,GitSettingsJson gitSettings) : base(gitManager,gitSettings)
		{
			
		}

		public sealed override bool Push()
		{
			return CallProcess("GitExtensions.exe", "push");
		}

		public sealed override bool Reset(Commit commit)
		{
			return CallProcess("GitExtensions.exe", "reset");
		}

		public sealed override bool Merge()
		{
			return CallProcess("GitExtensions.exe", "mergetool");
		}

		public sealed override bool Pull()
		{
			return CallProcess("GitExtensions.exe", "pull");
		}

		public sealed override bool Commit(string message)
		{
			return CallProcess("GitExtensions.exe", "commit");
		}

		public sealed override bool Fetch(string remote)
		{
			return CallProcess("GitExtensions.exe", "pull --fetch");
		}

		public sealed override bool Conflict(string path)
		{
			return CallProcess("GitExtensions.exe", "mergeconflicts");
		}

		public sealed override bool Diff(string path)
		{
			return CallProcess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Diff(string path, string path2)
		{
			return CallProcess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Diff(string path, Commit start, Commit end)
		{
			return CallProcess("GitExtensions.exe", "filehistory " + path);
		}

		public sealed override bool Diff(string path, Commit end)
		{
			return CallProcess("GitExtensions.exe", "viewdiff");
		}

		public sealed override bool Blame(string path)
		{
			return CallProcess("GitExtensions.exe", "blame " + path);
		}

		public sealed override bool Revert(IEnumerable<string> paths)
		{
			var path = paths.FirstOrDefault();
			return path != null && CallProcess("GitExtensions.exe", "revert " + path);
        }

		public sealed override bool Switch()
		{
			return CallProcess("GitExtensions.exe", "checkout");
		}
	}
}