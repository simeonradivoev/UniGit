using System.IO;
using System.Linq;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		public static GitLfsManager LfsManager;
		public static readonly GitManager GitManager;
		public static GitHookManager HookManager;
		public static GitCredentialsManager CredentialsManager;
		public static GitExternalManager ExternalManager;
		public static GitLfsHelper LfsHelper;
		private static GitAutoFetcher autoFetcher;
		private static readonly InjectionHelper injectionHelper;

		static UniGitLoader()
		{
			Profiler.BeginSample("UniGit Initialization");
			try
			{
				injectionHelper = new InjectionHelper();
				GitWindows.Init();
				var recompileChecker = ScriptableObject.CreateInstance<AssemblyReloadScriptableChecker>();
				recompileChecker.OnBeforeReloadAction = OnBeforeAssemblyReload;

				string repoPath = Application.dataPath.Replace(UniGitPath.UnityDeirectorySeparatorChar + "Assets", "").Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
				string settingsPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "Settings.json");

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
				GitManager.Callbacks.RepositoryCreate += OnRepositoryCreate;

				GitUnityMenu.Init(GitManager);
				GitResourceManager.Initilize();
				GitOverlay.Initlize(GitManager);

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
				//helpers
				injectionHelper.Bind<GitLfsHelper>();
				injectionHelper.Bind<FileLinesReader>();

				EditorApplication.delayCall += OnDelayedInit;

				if (!Repository.IsValid(repoPath))
				{
					return;
				}

				Rebuild(injectionHelper);
			}
			finally
			{
				Profiler.EndSample();
			}
		}

		private static void Rebuild(InjectionHelper injectionHelper)
		{
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
			autoFetcher = injectionHelper.GetInstance<GitAutoFetcher>();
			LfsHelper = injectionHelper.GetInstance<GitLfsHelper>();

			GitProjectContextMenus.Init(GitManager, ExternalManager);
		}

		private static void OnDelayedInit()
		{
			Profiler.BeginSample("UniGit Delayed Window Injection");
			try
			{
				//inject all windows that are open
				//windows should add themselves on OnEnable
				foreach (var editorWindow in GitWindows.Windows)
				{
					injectionHelper.Inject(editorWindow);
				}
			}
			finally
			{
				Profiler.EndSample();
			}
		}

		private static void OnRepositoryCreate()
		{
			Rebuild(injectionHelper);
		}

		private static void OnBeforeAssemblyReload()
		{
			if(GitManager != null) GitManager.Dispose();
		}

	    public static T FindWindow<T>() where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
	            return editorWindow;
	        }
	        return null;
	    }

	    public static T GetWindow<T>() where T : EditorWindow
	    {
	        return GetWindow<T>(false);
	    }

	    public static T GetWindow<T>(bool utility) where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
	            return editorWindow;
	        }
	        var newWindow = ScriptableObject.CreateInstance<T>();
            injectionHelper.Inject(newWindow);
            if(utility)
                newWindow.ShowUtility();
            else
	            newWindow.Show();

            return newWindow;
	    }

	    public static T DisplayWizard<T>(string title, string createButtonName) where T : ScriptableWizard
	    {
	        return DisplayWizard<T>(title, createButtonName, "");
	    }

	    public static T DisplayWizard<T>(string title,string createButtonName,string otherButtonName) where T : ScriptableWizard
	    {
	        var instance = ScriptableWizard.DisplayWizard<T>(title, createButtonName, otherButtonName);
            injectionHelper.Inject(instance);
            return instance;
        }
	}
}