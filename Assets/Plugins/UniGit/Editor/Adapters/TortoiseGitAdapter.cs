using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Attributes;
using UnityEngine;

namespace UniGit.Adapters
{
	[ExternalAdapter("Tortoise Git", "TortoiseGitMerge.exe", "TortoiseGitIDiff.exe", "TortoiseGitProc.exe",Priority = 10)]
	public class TortoiseGitAdapter : IExternalAdapter
	{
		public bool Push()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "push", GitManager.RepoPath);
			return true;
		}

		public bool Pull()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "pull", GitManager.RepoPath);
			return true;
		}

		public bool Commit(string message)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -logmsg:\"{2}\"", "commit", GitManager.RepoPath,message);
			return true;
		}

		public bool Reset(Commit commit)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", GitManager.RepoPath);
			return true;
		}

		public bool Merge()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "merge", GitManager.RepoPath);
			return true;
		}

		public bool Fetch(string remote)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -remote:\"{1}\"", "fetch",remote);
			return true;
		}

		public bool Revert(IEnumerable<string> paths)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "revert",string.Join("*", paths.ToArray()));
			return true;
		}

		public bool Conflict(string path)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "conflicteditor", path);
			return true;
		}

		public bool Diff(string path)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "diff", path);
			return true;
		}

		public bool Diff(string path,string path2)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -path2:\"{2}\"", "diff", path,path2);
			return true;
		}

		public bool Diff(string path, Commit start, Commit end)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -startrev:\"{2}\" -endrev:\"{3}\"", "diff", path, start.Sha,end.Sha);
			return true;
		}

		public bool Blame(string path)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "blame", path);
			return true;
		}

		public bool Switch()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "switch", GitManager.RepoPath);
			return true;
		}
	}
}