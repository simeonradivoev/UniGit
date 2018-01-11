using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UniGit.Status;
using UnityEngine;

namespace UniGit.Utils
{
	[Serializable]
	public class UniGitData : ScriptableObject
	{
		[SerializeField] private GitRepoStatus repositoryStatus;
		[SerializeField] private List<string> dirtyFilesQueue;
		private bool initialized;
		private Action onBeforeReloadAction;

		private void OnEnable()
		{
			if(repositoryStatus == null) repositoryStatus = new GitRepoStatus();
			if(dirtyFilesQueue == null) dirtyFilesQueue = new List<string>();
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			//the data needs to be initialized first, because starting the editor for the first time calls OnDisable
			if(initialized && onBeforeReloadAction != null)  onBeforeReloadAction.Invoke();
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
	}
}