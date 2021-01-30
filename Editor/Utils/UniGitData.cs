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
			repositoryStatus ??= new GitRepoStatus();
			dirtyFilesQueue ??= new List<string>();
			logEntries ??= new List<GitLog.LogEntry>();
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			//the data needs to be initialized first, because starting the editor for the first time calls OnDisable
			if (initialized)
			{
				gitCallbacks?.IssueBeforeAssemblyReload();
			}
		}

		public List<GitLog.LogEntry> LogEntries => logEntries;

        public List<string> DirtyFilesQueue => dirtyFilesQueue;

        public GitRepoStatus RepositoryStatus => repositoryStatus;

        public bool Initialized
		{
			get => initialized;
            set => initialized = value;
        }

		public bool LogInitialized
		{
			get => logInitialized;
            set => logInitialized = value;
        }
	}
}