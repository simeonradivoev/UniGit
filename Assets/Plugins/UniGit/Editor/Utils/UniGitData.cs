using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UniGit.Status;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	[Serializable]
	//must derive from EditorWindow even if not an Editor, in order to save data when entering and leaving play mode
	public class UniGitData : EditorWindow
	{
		[SerializeField] private GitRepoStatus repositoryStatus;
		[SerializeField] private List<string> dirtyFilesQueue;
		[SerializeField] private List<GitLog.LogEntry> logEntries;
		[SerializeField] private bool logInitialized;
		[SerializeField] private bool initialized;
		private Action onBeforeReloadAction;

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
			if (initialized && onBeforeReloadAction != null)
			{
				onBeforeReloadAction.Invoke();
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

		public Action OnBeforeReloadAction
		{
			get { return onBeforeReloadAction; }
			set { onBeforeReloadAction = value; }
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