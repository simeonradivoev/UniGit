using JetBrains.Annotations;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitUnityMenu
	{
		private static GitManager gitManager;

		internal static void Init(GitManager gitManager)
		{
			GitUnityMenu.gitManager = gitManager;
		}

        #region

	    [UsedImplicitly,MenuItem("Window/GIT History")]
        private static void OpenGitHistoryWindow()
	    {
	        UniGitLoader.GetWindow<GitHistoryWindow>();
	    }

		[UsedImplicitly,MenuItem("Window/GIT Diff")]
	    private static void OpenGitDiffWindow()
	    {
	        UniGitLoader.GetWindow<GitDiffWindow>();
	    }

		[UsedImplicitly,MenuItem("Window/GIT Settings")]
	    private static void OpenGitSettingsWindow()
	    {
	        UniGitLoader.GetWindow<GitSettingsWindow>();
	    }

		#endregion

		#region UniGit menus
		[UsedImplicitly,MenuItem("UniGit/About",false,0)]
		private static void OpenAboutWindow()
		{
			EditorWindow.GetWindow<GitAboutWindow>(true,"About UniGit");
		}

		[UsedImplicitly,MenuItem("UniGit/Initialize",false,0)]
		private static void Initilize()
		{
			if (!gitManager.IsValidRepo && EditorUtility.DisplayDialog("Initialize Repository", "Are you sure you want to initialize a Repository for your project", "Yes", "Cancel"))
			{
				gitManager.InitilizeRepositoryAndRecompile();
			}
		}

		[UsedImplicitly,MenuItem("UniGit/Initialize", true, 0)]
		private static bool InitilizeValidate()
		{
			return !gitManager.IsValidRepo;
		}

		[UsedImplicitly,MenuItem("UniGit/Report Issue", false, 0)]
		private static void ReportIssue()
		{
			GitLinks.GoTo(GitLinks.ReportIssue);
		}

		[UsedImplicitly,MenuItem("UniGit/Help",false ,0)]
		private static void Help()
		{
			GitLinks.GoTo(GitLinks.Wiki);
		}

		[UsedImplicitly,MenuItem("UniGit/Donate",false,20)]
		private static void Donate()
		{
			GitLinks.GoTo(GitLinks.Donate);
		}
        #endregion
    }
}