using System.Linq;
using UniGit.Utils;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		static UniGitLoader()
		{
			var recompileChecker = ScriptableObject.CreateInstance<AssemblyReloadScriptableChecker>();
			recompileChecker.OnBeforeReloadAction = OnBeforeAssemblyReload;

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

		private static void OnBeforeAssemblyReload()
		{
			if(GitManager.Instance != null) GitManager.Instance.Dispose();
		}
	}
}