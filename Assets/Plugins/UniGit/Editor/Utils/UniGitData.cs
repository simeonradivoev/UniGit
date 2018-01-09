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
		private Action onBeforeReloadAction;

		private void OnEnable()
		{
			if(repositoryStatus == null) repositoryStatus = new GitRepoStatus();
			if(dirtyFilesQueue == null) dirtyFilesQueue = new List<string>();
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			if(onBeforeReloadAction != null) onBeforeReloadAction.Invoke();
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
	}
}