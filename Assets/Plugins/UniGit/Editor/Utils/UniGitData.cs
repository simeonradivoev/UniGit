using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UniGit.Status;
using UnityEngine;

namespace UniGit.Utils
{
	[Serializable]
	//must derive from EditorWindow even if not an Editor, in order to save data when entering and leaving play mode
	public class UniGitData : ScriptableObject
	{
		[SerializeField] private GitRepoStatus repositoryStatus;
		[SerializeField] private List<string> dirtyFilesQueue;
		[SerializeField] private List<GitLog.LogEntry> logEntries;
		[SerializeField] private bool logInitialized;
		[SerializeField] private bool initialized;
		private GitCallbacks gitCallbacks;

		[UniGitInject]
		private void Construct(GitCallbacks gitCallbacks)
		{
			this.gitCallbacks = gitCallbacks;
		}

		private void OnEnable()
		{
			if(repositoryStatus == null) repositoryStatus = new GitRepoStatus();
			if(dirtyFilesQueue == null) dirtyFilesQueue = new List<string>();
			if(logEntries == null) logEntries = new List<GitLog.LogEntry>();
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			//the data needs to be initialized first, because starting the editor for the first time calls OnDisable
			if (initialized && gitCallbacks != null)
			{
				gitCallbacks.IssueBeforeAssemblyReload();
			}
		}

		public List<GitLog.LogEntry> LogEntries
		{
			get { return logEntries; }
		}

		public List<string> DirtyFilesQueue
		{
			get { return dirtyFilesQueue; }
		}

		public GitRepoStatus RepositoryStatus
		{
			get { return repositoryStatus; }
		}

		public bool Initialized
		{
			get { return initialized; }
			set { initialized = value; }
		}

		public bool LogInitialized
		{
			get { return logInitialized; }
			set { logInitialized = value; }
		}
	}
}