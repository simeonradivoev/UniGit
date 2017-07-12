using UniGit.Status;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public abstract class GitSettingsTab
	{
		protected GitSettingsWindow settingsWindow;
		protected SerializedObject serializedObject;
		private bool hasFocused;
		private bool initilized;
		protected GitManager gitManager;

		internal GitSettingsTab(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}

		internal void OnEnable()
		{
			GitCallbacks.EditorUpdate -= OnEditorUpdateInternal;
			GitCallbacks.EditorUpdate += OnEditorUpdateInternal;
			GitCallbacks.UpdateRepository -= OnGitManagerUpdateInternal;
			GitCallbacks.UpdateRepository += OnGitManagerUpdateInternal;
		}

		internal void Setup(GitSettingsWindow settingsWindow, SerializedObject serializedObject)
		{
			this.settingsWindow = settingsWindow;
			this.serializedObject = serializedObject;
		}

		internal abstract void OnGUI(Rect rect, Event current);

		protected virtual void OnInitialize()
		{
			
		}

		public void OnFocus()
		{
			hasFocused = true;
		}

		public virtual void OnGitUpdate(GitRepoStatus status, string[] paths)
		{
			
		}

		private void OnGitManagerUpdateInternal(GitRepoStatus status, string[] paths)
		{
			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (!initilized || !gitManager.IsValidRepo) return;
			OnGitUpdate(status, paths);
		}

		private void OnEditorUpdateInternal()
		{
			//Only initialize if the editor Window is focused
			if (hasFocused && !initilized && gitManager.Repository != null)
			{
				if (gitManager.LastStatus != null)
				{
					initilized = true;
					if (!gitManager.IsValidRepo) return;
					OnInitialize();
					OnGitManagerUpdateInternal(gitManager.LastStatus, null);
				}
			}
		}

		internal void OnDestroy()
		{
			GitCallbacks.EditorUpdate -= OnEditorUpdateInternal;
			GitCallbacks.UpdateRepository -= OnGitManagerUpdateInternal;
		}
	}
}