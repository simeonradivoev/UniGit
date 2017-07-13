using System;
using LibGit2Sharp;
using UniGit.Status;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow, IGitWindow
	{
		//used an object because the EditorWindow saves Booleans even if private
		[NonSerialized] private object initilized;
		[NonSerialized] private object hasFocused;
		[NonSerialized] protected GitManager gitManager;
		[NonSerialized] protected GitSettingsJson gitSettings;

		protected virtual void OnEnable()
		{
			Construct(GitManager.Instance);

			GitCallbacks.EditorUpdate -= OnEditorUpdateInternal;
			GitCallbacks.EditorUpdate += OnEditorUpdateInternal;

			GitCallbacks.UpdateRepository -= OnGitManagerUpdateInternal;
			GitCallbacks.UpdateRepository += OnGitManagerUpdateInternal;
			GitCallbacks.OnRepositoryLoad -= OnRepositoryLoad;
			GitCallbacks.OnRepositoryLoad += OnRepositoryLoad;

			GitCallbacks.UpdateRepositoryStart -= UpdateTitleIcon;
			GitCallbacks.UpdateRepositoryStart += UpdateTitleIcon;

			GitCallbacks.UpdateRepositoryFinish -= UpdateTitleIcon;
			GitCallbacks.UpdateRepositoryFinish += UpdateTitleIcon;
		}

		public virtual void Construct(GitManager gitManager)
		{
			this.gitManager = gitManager;
			gitSettings = gitManager.Settings;
			titleContent.image = gitManager.GetGitStatusIcon();
		}

		protected virtual void OnFocus()
		{
			hasFocused = true;
		}

		private void OnGitManagerUpdateInternal(GitRepoStatus status,string[] paths)
		{
			UpdateTitleIcon();

			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (initilized == null || !gitManager.IsValidRepo) return;
			OnGitUpdate(status, paths);
		}

		private void UpdateTitleIcon()
		{
			titleContent.image = gitManager.GetGitStatusIcon();
			Repaint();
		}

		private void OnEditorUpdateInternal()
		{
			//Only initialize if the editor Window is focused
			if (hasFocused != null && initilized == null && gitManager.Repository != null)
			{
				if (gitManager.LastStatus != null)
				{
					initilized = true;
					if (!gitManager.IsValidRepo) return;
					OnInitialize();
					OnGitManagerUpdateInternal(gitManager.LastStatus,null);
					//simulate repository loading for first initialization
					OnRepositoryLoad(gitManager.Repository);
					Repaint();
				}
			}

			if (hasFocused != null)
			{
				OnEditorUpdate();
			}
		}

		protected void OnDestroy()
		{
			GitCallbacks.EditorUpdate -= OnEditorUpdateInternal;
			GitCallbacks.UpdateRepository -= OnGitManagerUpdateInternal;
			GitCallbacks.OnRepositoryLoad -= OnRepositoryLoad;
			GitCallbacks.UpdateRepositoryStart -= UpdateTitleIcon;
			GitCallbacks.UpdateRepositoryFinish -= UpdateTitleIcon;
		}

		#region Safe Controlls

		public void LoseFocus()
		{
			GUIUtility.keyboardControl = 0;
			EditorGUIUtility.editingTextField = false;
			Repaint();
		}

		#endregion

		protected abstract void OnGitUpdate(GitRepoStatus status,string[] paths);
		protected abstract void OnInitialize();
		protected abstract void OnRepositoryLoad(Repository repository);
		protected abstract void OnEditorUpdate();
	}
}