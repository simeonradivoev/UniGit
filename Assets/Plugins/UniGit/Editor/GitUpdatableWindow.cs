using System;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow, IGitWatcher
	{
		//used an object because the EditorWindow saves Booleans even if private
		[NonSerialized] private object initialized;
		[NonSerialized] private object isRepositoryDirty;
		[NonSerialized] protected GitManager gitManager;
		[NonSerialized] protected GitReflectionHelper reflectionHelper;

		protected virtual void OnEnable()
		{
			GitWindows.AddWindow(this);
			if(gitManager != null)
				titleContent.image = gitManager.GetGitStatusIcon();
		}

        [UniGitInject]
		private void Construct(GitManager gitManager,GitReflectionHelper reflectionHelper)
		{
			if (gitManager == null)
			{
				Debug.LogError("Git manager cannot be null.");
				return;
			}
			if (this.gitManager != null && this.gitManager.Callbacks != null)
			{
				Unsubscribe(this.gitManager.Callbacks);
			}
			this.gitManager = gitManager;
			this.gitManager.AddWatcher(this);
			this.reflectionHelper = reflectionHelper;
			Subscribe(gitManager.Callbacks);
		}

		#region Editor Specific Updates

		//caled only in the editor as we can't force Editor recompile to reinject dependencies
		protected virtual void OnRepositoryCreate()
		{
			
		}

		#endregion

		protected virtual void Subscribe(GitCallbacks callbacks)
		{
			if (callbacks == null)
			{
				Debug.LogError("Trying to subscribe to null callbacks");
				return;
			}
			callbacks.EditorUpdate += OnEditorUpdateInternal;
			callbacks.UpdateRepository += OnGitManagerUpdateRepositoryInternal;
			callbacks.OnRepositoryLoad += OnRepositoryLoad;
			callbacks.UpdateRepositoryStart += UpdateTitleIcon;
			callbacks.RepositoryCreate += OnRepositoryCreate;
		}

		protected virtual void Unsubscribe(GitCallbacks callbacks)
		{
			if (callbacks == null) return;
			callbacks.EditorUpdate -= OnEditorUpdateInternal;
			callbacks.UpdateRepository -= OnGitManagerUpdateRepositoryInternal;
			callbacks.OnRepositoryLoad -= OnRepositoryLoad;
			callbacks.UpdateRepositoryStart -= UpdateTitleIcon;
			callbacks.RepositoryCreate -= OnRepositoryCreate;
		}

		protected virtual void OnFocus()
		{
			
		}

		protected virtual void OnLostFocus()
		{
			//the window is docked and has become hidden reset the initialization
			if (!HasFocus)
			{
				initialized = null;
			}
		}

		private void OnGitManagerUpdateRepositoryInternal(GitRepoStatus status,string[] paths)
		{
			UpdateTitleIcon();

			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (initialized == null || !gitManager.IsValidRepo) return;
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
			if (HasFocus && initialized == null && gitManager.Repository != null)
			{
				var gitManagerStatus = gitManager.GetCachedStatus();
				if (gitManagerStatus.Initilzied)
				{
					initialized = true;
					if (!gitManager.IsValidRepo) return;
					OnInitialize();
					OnGitManagerUpdateRepositoryInternal(gitManagerStatus, null);
					//simulate repository loading for first initialization
					OnRepositoryLoad(gitManager.Repository);
					Repaint();
				}
			}

			if (HasFocus)
			{
				OnEditorUpdate();
			}
		}

		protected void OnDisable()
		{
			GitWindows.RemoveWindow(this);
		}

		protected void OnDestroy()
		{
			if (gitManager != null)
			{
				if(gitManager.Callbacks != null) Unsubscribe(gitManager.Callbacks);
				gitManager.RemoveWatcher(this);
			}
		}

		#region Safe Controlls

		public void LoseFocus()
		{
			GUIUtility.keyboardControl = 0;
			EditorGUIUtility.editingTextField = false;
			Repaint();
		}

		#endregion

		public bool IsInitialized
		{
			get { return initialized != null; }
		}

		public bool HasFocus
		{
			get
			{
				return reflectionHelper.HasFocusFucntion.Invoke(this);
			}
		}

		public virtual bool IsWatching
		{
			get { return HasFocus; }
		}

		public bool IsValid
		{
			get { return this; }
		}

		protected abstract void OnGitUpdate(GitRepoStatus status,string[] paths);
		protected abstract void OnInitialize();
		protected abstract void OnRepositoryLoad(Repository repository);
		protected abstract void OnEditorUpdate();
	}
}