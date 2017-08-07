using System;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow, IGitWindow, ISerializationCallbackReceiver
	{
		//used an object because the EditorWindow saves Booleans even if private
		[NonSerialized] private object initilized;
		[NonSerialized] private object hasFocused;
		[NonSerialized] protected GitManager gitManager;
		[NonSerialized] protected GitSettingsJson gitSettings;
		[NonSerialized] protected readonly InjectionHelper injectionHelper = new InjectionHelper();

		protected virtual void OnEnable()
		{
			injectionHelper.Bind(GetType()).FromInstance(this);
			if(gitManager != null)
				titleContent.image = gitManager.GetGitStatusIcon();
		}

		public virtual void Construct(GitManager gitManager)
		{
			if (gitManager == null)
			{
				Debug.LogError("Git manager cannot be null.");
				return;
			}
			injectionHelper.Bind<GitManager>().FromInstance(gitManager);
			if (this.gitManager != null && this.gitManager.Callbacks != null)
			{
				Unsubscribe(this.gitManager.Callbacks);
			}
			this.gitManager = gitManager;
			gitSettings = gitManager.Settings;

			Subscribe(gitManager.Callbacks);
		}

		public virtual void OnBeforeSerialize()
		{
			
		}

		#region Editor Specific Updates

		public virtual void OnAfterDeserialize()
		{
			Construct(UniGitLoader.GitManager);
		}

		//caled only in the editor as we can't force Editor recompile to reinject dependencies
		protected virtual void OnRepositoryCreate()
		{
			Construct(UniGitLoader.GitManager);
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
			callbacks.UpdateRepository += OnGitManagerUpdateInternal;
			callbacks.OnRepositoryLoad += OnRepositoryLoad;
			callbacks.UpdateRepositoryStart += UpdateTitleIcon;
			callbacks.UpdateRepositoryFinish += UpdateTitleIcon;
			callbacks.RepositoryCreate += OnRepositoryCreate;
		}

		protected virtual void Unsubscribe(GitCallbacks callbacks)
		{
			if (callbacks == null) return;
			callbacks.EditorUpdate -= OnEditorUpdateInternal;
			callbacks.UpdateRepository -= OnGitManagerUpdateInternal;
			callbacks.OnRepositoryLoad -= OnRepositoryLoad;
			callbacks.UpdateRepositoryStart -= UpdateTitleIcon;
			callbacks.UpdateRepositoryFinish -= UpdateTitleIcon;
			callbacks.RepositoryCreate -= OnRepositoryCreate;
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
			if(gitManager != null && gitManager.Callbacks != null)
				Unsubscribe(gitManager.Callbacks);
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