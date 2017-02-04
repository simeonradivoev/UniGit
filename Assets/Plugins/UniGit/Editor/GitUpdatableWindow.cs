using System;
using System.Collections.Generic;
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

			EditorApplication.update -= OnEditorUpdateInternal;
			EditorApplication.update += OnEditorUpdateInternal;

			GitCallbacks.UpdateRepository -= OnGitManagerUpdateInternal;
			GitCallbacks.UpdateRepository += OnGitManagerUpdateInternal;
			GitCallbacks.OnRepositoryLoad -= OnRepositoryLoad;
			GitCallbacks.OnRepositoryLoad += OnRepositoryLoad;
		}

		protected virtual void OnFocus()
		{
			hasFocused = true;
		}

		private void OnGitManagerUpdateInternal(GitRepoStatus status,string[] paths)
		{
			titleContent.image = GitManager.GetGitStatusIcon();

			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (initilized == null || !GitManager.IsValidRepo) return;
			OnGitUpdate(status, paths);
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