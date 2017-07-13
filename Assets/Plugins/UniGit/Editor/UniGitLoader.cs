using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		static UniGitLoader()
		{
			GitManager gitManager = new GitManager(Application.dataPath.Replace("/Assets", "").Replace("/", "\\"));
			GitManager.Instance = gitManager;

			if (!gitManager.IsValidRepo)
			{
				return;
			}

			GitUnityMenu.Init(gitManager);
			GitProjectContextMenus.Init(gitManager);
			GitResourceManager.Initilize();
			GitCredentialsManager.Load(gitManager);
			GitOverlay.Initlize(gitManager);
			GitLfsManager.Load(gitManager);
			GitHookManager.Load(gitManager);
			GitExternalManager.Load(gitManager);
		}
	}
}