using System;
using JetBrains.Annotations;
using UniGit.Attributes;
using UnityEngine;

namespace UniGit.Adapters
{
	[ExternalAdapter("Tortoise Git", "TortoiseGitMerge.exe", "TortoiseGitIDiff.exe", "TortoiseGitProc.exe")]
	public class TortoiseGitAdapter : IExternalAdapter
	{
		public void Push()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "push", GitManager.RepoPath);
		}

		public void Pull()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "pull", GitManager.RepoPath);
		}

		public void Commit(string message)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\" -logmsg:\"{2}\"", "commit", GitManager.RepoPath,message);
		}

		public void Reset()
		{
			
		}

		public void Merge()
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -path:\"{1}\"", "merge", GitManager.RepoPath);
		}

		public void Fetch(string remote)
		{
			GitExternalManager.CallProccess("TortoiseGitProc.exe", "-command:\"{0}\" -remote:\"{1}\"", "fetch",remote);
		}

		public void Conflict(string left, string right, string ansestor, string merge,Type assetType)
		{
			if (assetType == typeof(Texture) || assetType.IsSubclassOf(typeof(Texture)))
			{
				GitExternalManager.CallProccess("TortoiseGitIDiff.exe", "-right:\"{1}\" -left:\"{2}\" -merged:\"{3}\"", ansestor, right, left, merge);
			}
			else
			{
				GitExternalManager.CallProccess("TortoiseGitMerge.exe", "-base:\"{0}\" -mine:\"{1}\" -theirs:\"{2}\" -merged:\"{3}\"", ansestor, right, left, merge);
			}
		}

		public void Diff(string leftTitle, string leftPath, string rightTitle, string rightPath, Type assetType)
		{
			if (assetType == typeof(Texture) || assetType.IsSubclassOf(typeof(Texture)))
			{
				GitExternalManager.CallProccess("TortoiseGitIDiff.exe", "-left:\"{0}\" -lefttitle:\"{1}\" -right:\"{2}\" -righttitle:\"{3}\"", leftPath, leftTitle, rightPath, rightTitle);
			}
			else
			{
				GitExternalManager.CallProccess("TortoiseGitMerge.exe", "-base:\"{0}\" -basename:\"{1}\" -mine:\"{2}\" -minename:\"{3}\" -base", leftPath, leftTitle, rightPath, rightTitle);
			}
		}
	}
}