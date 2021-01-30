using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UniGit.Attributes;
using UniGit.Utils;

namespace UniGit.Adapters
{
	[ExternalAdapter("Tortoise Git", "TortoiseGitMerge.exe", "TortoiseGitIDiff.exe", "TortoiseGitProc.exe",Priority = 10)]
	public class TortoiseGitAdapter : BaseExternalAdapter
	{
		[UniGitInject]
		public TortoiseGitAdapter(GitManager gitManager,GitSettingsJson gitSettings) : base(gitManager,gitSettings)
		{
			
		}

		public sealed override bool Push()
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "push", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Pull()
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "pull", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Commit(string message)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -logmsg:\"{2}\"", "commit", gitManager.GetCurrentRepoPath(),message);
			return true;
		}

		public sealed override bool Reset(Commit commit)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Merge()
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "merge", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Fetch(string remote)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -remote:\"{1}\"", "fetch",remote);
			return true;
		}

		public sealed override bool Revert(IEnumerable<string> paths)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "revert",string.Join("*", paths.ToArray()));
			return true;
		}

		public sealed override bool Conflict(string path)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "conflicteditor", path);
			return true;
		}

		public sealed override bool Diff(string path)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "diff", path);
			return true;
		}

		public sealed override bool Diff(string path,string path2)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -path2:\"{2}\"", "diff", path,path2);
			return true;
		}

		public sealed override bool Diff(string path, Commit end)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -endrev:\"{2}\"", "diff", path, end.Sha);
			return true;
		}

		public sealed override bool Diff(string path, Commit start, Commit end)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -startrev:\"{2}\" -endrev:\"{3}\"", "diff", path, start.Sha,end.Sha);
			return true;
		}

		public sealed override bool Blame(string path)
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "blame", path);
			return true;
		}

		public sealed override bool Switch()
		{
			CallProcess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", gitManager.GetCurrentRepoPath());
			return true;
		}
	}
}