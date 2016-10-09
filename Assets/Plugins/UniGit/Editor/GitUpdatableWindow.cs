using System;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public abstract class GitUpdatableWindow : EditorWindow
	{
		//used an object because the EditorWindow saves Booleans even if private
		private object initilized;

		protected virtual void OnEnable()
		{
			titleContent.image = GitManager.GetGitStatusIcon();

			GitManager.updateRepository -= OnGitManagerUpdateInternal;
			GitManager.updateRepository += OnGitManagerUpdateInternal;

			if (initilized == null) return;
			//this will be called when entering Play mode
			OnGitManagerUpdateInternal(GitManager.Repository.RetrieveStatus());
		}

		protected virtual void OnFocus()
		{
			//Only initialize if the editor Window is focused
			if (initilized != null) return;
			initilized = true;
			if (!GitManager.IsValidRepo) return;
			OnInitialize();
			OnGitManagerUpdateInternal(GitManager.Repository.RetrieveStatus());
			Repaint();
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

		protected abstract void OnGitUpdate(RepositoryStatus status);
		protected abstract void OnInitialize();
	}
}