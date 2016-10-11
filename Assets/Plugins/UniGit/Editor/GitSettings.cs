using System;
using UnityEngine;
using System.Collections;

namespace UniGit
{
	public class GitSettings : ScriptableObject
	{
		[Tooltip("Auto stage changes for committing when an asset is modified")] public bool AutoStage = true;
		[Tooltip("Auto fetch repository changes when possible. This will tell you about changes to the remote repository without having to pull. This only works with the Credentials Manager.")] public bool AutoFetch = true;
		[Tooltip("The Maximum amount of commits show in the Git History Window. Use -1 for infinite commits.")] [Delayed] public int MaxCommits = 32;
		public ExternalsTypeEnum ExternalsType;
		public string ExternalProgram;
		public string CredentialsManager;

		[Flags]
		[SerializeField]
		public enum ExternalsTypeEnum
		{
			Pull = 1 << 0,
			Push = 1 << 1,
			Fetch = 1 << 2,
			Merge = 1 << 3,
			Commit = 1 << 4
		}
	}
}