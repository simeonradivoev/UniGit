using LibGit2Sharp;
using UniGit.Status;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow
	{
		//used an object because the EditorWindow saves Booleans even if private
		private object initilized;
		private object hasFocused;

		protected virtual void OnEnable()
		{
			titleContent.image = GitManager.GetGitStatusIcon();

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

		protected virtual void OnFocus()
		{
			hasFocused = true;
		}

		private void OnGitManagerUpdateInternal(GitRepoStatus status,string[] paths)
		{
			UpdateTitleIcon();

			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (initilized == null || !GitManager.IsValidRepo) return;
			OnGitUpdate(status, paths);
		}

		private void UpdateTitleIcon()
		{
			titleContent.image = GitManager.GetGitStatusIcon();
			Repaint();
		}

		private void OnEditorUpdateInternal()
		{
			//Only initialize if the editor Window is focused
			if (hasFocused != null && initilized == null && GitManager.Repository != null)
			{
				if (GitManager.LastStatus != null)
				{
					initilized = true;
					if (!GitManager.IsValidRepo) return;
					OnInitialize();
					OnGitManagerUpdateInternal(GitManager.LastStatus,null);
					//simulate repository loading for first initialization
					OnRepositoryLoad(GitManager.Repository);
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