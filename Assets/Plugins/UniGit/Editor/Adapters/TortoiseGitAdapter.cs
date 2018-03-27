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
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "push", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Pull()
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "pull", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Commit(string message)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -logmsg:\"{2}\"", "commit", gitManager.GetCurrentRepoPath(),message);
			return true;
		}

		public sealed override bool Reset(Commit commit)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Merge()
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "merge", gitManager.GetCurrentRepoPath());
			return true;
		}

		public sealed override bool Fetch(string remote)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -remote:\"{1}\"", "fetch",remote);
			return true;
		}

		public sealed override bool Revert(IEnumerable<string> paths)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "revert",string.Join("*", paths.ToArray()));
			return true;
		}

		public sealed override bool Conflict(string path)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "conflicteditor", path);
			return true;
		}

		public sealed override bool Diff(string path)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "diff", path);
			return true;
		}

		public sealed override bool Diff(string path,string path2)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -path2:\"{2}\"", "diff", path,path2);
			return true;
		}

		public sealed override bool Diff(string path, Commit end)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -endrev:\"{2}\"", "diff", path, end.Sha);
			return true;
		}

		public sealed override bool Diff(string path, Commit start, Commit end)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -startrev:\"{2}\" -endrev:\"{3}\"", "diff", path, start.Sha,end.Sha);
			return true;
		}

		public sealed override bool Blame(string path)
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "blame", path);
			return true;
		}

		public sealed override bool Switch()
		{
			CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", gitManager.GetCurrentRepoPath());
			return true;
		}
	}
}