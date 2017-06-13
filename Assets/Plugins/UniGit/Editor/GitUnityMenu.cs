using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitUnityMenu
	{
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
				GitManager.InitilizeRepository();
			}
		}

		[MenuItem("UniGit/Initialize", true, 0)]
		private static bool InitilizeValidate()
		{
			return !GitManager.IsValidRepo;
		}

		[MenuItem("UniGit/Donate",false,20)]
		private static void Donate()
		{
			Application.OpenURL(GitAboutWindow.DonateUrl);
		}
	}
}