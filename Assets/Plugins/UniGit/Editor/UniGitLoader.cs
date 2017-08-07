using System.IO;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		public static readonly GitLfsManager LfsManager;
		public static readonly GitManager GitManager;
		public static readonly GitHookManager HookManager;
		public static readonly GitCredentialsManager CredentialsManager;
		public static readonly GitExternalManager ExternalManager;
		private static readonly GitAutoFetcher autoFetcher;

		static UniGitLoader()
		{
			var injectionHelper = new InjectionHelper();
			var recompileChecker = ScriptableObject.CreateInstance<AssemblyReloadScriptableChecker>();
			recompileChecker.OnBeforeReloadAction = OnBeforeAssemblyReload;

			string repoPath = Application.dataPath.Replace("/Assets", "").Replace("/", "\\");
			string settingsPath = Path.Combine(repoPath, Path.Combine(".git",Path.Combine("UniGit", "Settings.json")));

			injectionHelper.Bind<string>().FromInstance(repoPath).WithId("repoPath");
			injectionHelper.Bind<string>().FromInstance(settingsPath).WithId("settingsPath");


			injectionHelper.Bind<GitCallbacks>().FromMethod(() =>
			{
				var c = new GitCallbacks();
				EditorApplication.update += c.IssueEditorUpdate;
				c.RefreshAssetDatabase += AssetDatabase.Refresh;
				c.SaveAssetDatabase += AssetDatabase.SaveAssets;
				return c;
			});
			injectionHelper.Bind<IGitPrefs>().To<UnityEditorGitPrefs>();
			injectionHelper.Bind<GitManager>();
			injectionHelper.Bind<GitSettingsJson>();
			injectionHelper.Bind<GitSettingsManager>();

			GitManager = injectionHelper.GetInstance<GitManager>();

			GitUnityMenu.Init(GitManager);
			GitResourceManager.Initilize();
			GitOverlay.Initlize(GitManager);

			if (!Repository.IsValid(repoPath))
			{
				return;
			}

			//credentials
			injectionHelper.Bind<ICredentialsAdapter>().To<WincredCredentialsAdapter>();
			injectionHelper.Bind<GitCredentialsManager>();
			//externals
			injectionHelper.Bind<IExternalAdapter>().To<GitExtensionsAdapter>();
			injectionHelper.Bind<IExternalAdapter>().To<TortoiseGitAdapter>();
			injectionHelper.Bind<GitExternalManager>();
			injectionHelper.Bind<GitLfsManager>();
			//hooks
			injectionHelper.Bind<GitPushHookBase>().To<GitLfsPrePushHook>();
			injectionHelper.Bind<GitHookManager>();

			var settingsManager = injectionHelper.GetInstance<GitSettingsManager>();
			settingsManager.LoadGitSettings();

			//delayed called must be used for serialized properties to be loaded
			EditorApplication.delayCall += () =>
			{
				settingsManager.LoadOldSettingsFile();
				GitManager.MarkDirty(true);
			};

			HookManager = injectionHelper.GetInstance<GitHookManager>();
			LfsManager = injectionHelper.GetInstance<GitLfsManager>();
			ExternalManager = injectionHelper.GetInstance<GitExternalManager>();
			CredentialsManager = injectionHelper.GetInstance<GitCredentialsManager>();
			autoFetcher = injectionHelper.CreateInstance<GitAutoFetcher>();

			GitProjectContextMenus.Init(GitManager, ExternalManager);
		}

		private static void OnBeforeAssemblyReload()
		{
			if(GitManager != null) GitManager.Dispose();
		}
	}
}