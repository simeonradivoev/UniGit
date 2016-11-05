using System;
using System.Collections.Generic;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow
	{
		//used an object because the EditorWindow saves Booleans even if private
		private object initilized;
		private object hasFocused;
		protected Queue<Action> actionQueue = new Queue<Action>();

		protected virtual void OnEnable()
		{
			titleContent.image = GitManager.GetGitStatusIcon();

			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
			GitManager.updateRepository -= OnGitManagerUpdateInternal;
			GitManager.updateRepository += OnGitManagerUpdateInternal;
			GitManager.onRepositoryLoad -= OnRepositoryLoad;
			GitManager.onRepositoryLoad += OnRepositoryLoad;
		}

		protected virtual void OnFocus()
		{
			hasFocused = true;
		}

		private void OnGitManagerUpdateInternal(RepositoryStatus status)
		{
			titleContent.image = GitManager.GetGitStatusIcon();

			//only update the window if it is initialized. That means opened and visible.
			//the editor window will initialize itself once it's focused
			if (initilized == null || !GitManager.IsValidRepo) return;
			OnGitUpdate(status);
			Repaint();
		}

		private void OnEditorUpdate()
		{
			//Only initialize if the editor Window is focused
			if (hasFocused != null && initilized == null && GitManager.Repository != null)
			{
				initilized = true;
				if (!GitManager.IsValidRepo) return;
				OnInitialize();
				OnGitManagerUpdateInternal(GitManager.Repository.RetrieveStatus());
				//simulate repository loading for first initialization
				OnRepositoryLoad(GitManager.Repository);
				OnGitManagerUpdateInternal(GitManager.Repository.RetrieveStatus());
				Repaint();
			}

			if (actionQueue.Count > 0)
			{
				Action action = actionQueue.Dequeue();
				if (action != null)
				{
					try
					{
						action.Invoke();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
						throw;
					}
				}
				
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

		protected abstract void OnGitUpdate(RepositoryStatus status);
		protected abstract void OnInitialize();
		protected abstract void OnRepositoryLoad(Repository repository);
	}
}