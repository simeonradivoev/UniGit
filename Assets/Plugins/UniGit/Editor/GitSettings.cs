using UnityEngine;
using System.Collections;

namespace UniGit
{
	public class GitSettings : ScriptableObject
	{
		[Tooltip("Auto stage changes for committing when an asset is modified")]
		[SerializeField]
		public bool AutoStage = true;
		[Tooltip("Auto fetch repository changes when possible. This will tell you about changes to the remote repository without having to pull. This only works with the Credentials Manager.")]
		[SerializeField]
		public bool AutoFetch = true;
		[Tooltip("The Maximum amount of commits show in the Git History Window. Use -1 for infinite commits.")]
		[Delayed]
		[SerializeField]
		public int MaxCommits = 32;
	}
}