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

		[MenuItem("UniGit/About",false,0)]
		private static void OpenAboutWindow()
		{
			EditorWindow.GetWindow<GitAboutWindow>(true,"About UniGit");
		}

		[MenuItem("UniGit/Initialize",false,0)]
		private static void Initilize()
		{
			if (EditorUtility.DisplayDialog("Initialize Repository", "Are you sure you want to initialize a Repository for your project", "Yes", "Cancel"))
			{
				gitManager.InitilizeRepository(true);
			}
		}

		[MenuItem("UniGit/Initialize", true, 0)]
		private static bool InitilizeValidate()
		{
			return !gitManager.IsValidRepo;
		}

		[MenuItem("UniGit/Report Issue", false, 0)]
		private static void ReportIssue()
		{
			Application.OpenURL("https://github.com/simeonradivoev/UniGit/issues/new");
		}

		[MenuItem("UniGit/Help",false ,0)]
		private static void Help()
		{
			Application.OpenURL("https://github.com/simeonradivoev/UniGit/wiki");
		}

		[MenuItem("UniGit/Donate",false,20)]
		private static void Donate()
		{
			Application.OpenURL(GitAboutWindow.DonateUrl);
		}

		[MenuItem("UniGit/Dev/Recompile Scripts", false, 20)]
		private static void Recompile()
		{
			GitManager.Recompile();
		}
	}
}