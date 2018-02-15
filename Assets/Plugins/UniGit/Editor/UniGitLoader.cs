using System;
using System.IO;
using System.Linq;
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
		public static GitManager GitManager;
		private static readonly InjectionHelper injectionHelper;
		public static GitCallbacks GitCallbacks;
		private static GitReflectionHelper ReflectionHelper;
		private static GitSettingsJson GitSettings;
		private static UniGitData uniGitData;

		static UniGitLoader()
		{
			HandlePaths();

			GitProfilerProxy.BeginSample("UniGit Initialization");
			try
			{
				GitWindows.OnWindowAddedEvent += OnWindowAdded;
				EditorApplication.update += OnEditorUpdate;

				injectionHelper = new InjectionHelper();

				GitWindows.Init();

				string repoPath = Application.dataPath.Replace(UniGitPath.UnityDeirectorySeparatorChar + "Assets", "").Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
				string settingsPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "Settings.json");
				string logPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "log.txt");
				uniGitData = CreateUniGitData(); //data must be created manually to not call unity methods from constructors

				injectionHelper.Bind<GitInitializer>().NonLazy();
				injectionHelper.Bind<string>().FromInstance(repoPath).WithId("repoPath");
				injectionHelper.Bind<string>().FromInstance(settingsPath).WithId("settingsPath");
				injectionHelper.Bind<string>().FromInstance(logPath).WithId("logPath");
				injectionHelper.Bind<UniGitData>().FromMethod(c => uniGitData); //must have a getter so that it can be injected 
				injectionHelper.Bind<GitCallbacks>().FromMethod(GetGitCallbacks);
				injectionHelper.Bind<IGitPrefs>().To<UnityEditorGitPrefs>();
				injectionHelper.Bind<GitManager>().NonLazy();
				injectionHelper.Bind<GitSettingsJson>();
				injectionHelper.Bind<GitSettingsManager>();
				injectionHelper.Bind<GitAsyncManager>();
				injectionHelper.Bind<GitFileWatcher>().NonLazy();
				injectionHelper.Bind<GitReflectionHelper>();
				injectionHelper.Bind<IGitResourceManager>().To<GitResourceManager>();
				injectionHelper.Bind<GitOverlay>();
				injectionHelper.Bind<GitAutoFetcher>().NonLazy();
				injectionHelper.Bind<GitLog>();
				injectionHelper.Bind<ILogger>().FromMethod(c => new Logger(c.injectionHelper.GetInstance<GitLog>()));
				injectionHelper.Bind<GitAnimation>();
				injectionHelper.Bind<ICredentialsAdapter>().To<WincredCredentialsAdapter>();
				injectionHelper.Bind<GitCredentialsManager>().NonLazy();
				injectionHelper.Bind<IExternalAdapter>().To<GitExtensionsAdapter>();
				injectionHelper.Bind<IExternalAdapter>().To<TortoiseGitAdapter>();
				injectionHelper.Bind<GitExternalManager>();
				injectionHelper.Bind<GitLfsManager>().NonLazy(); //must be non lazy as it add itself as a filter
				injectionHelper.Bind<GitPushHookBase>().To<GitLfsPrePushHook>();
				injectionHelper.Bind<GitHookManager>().NonLazy();
				injectionHelper.Bind<GitLfsHelper>();
				injectionHelper.Bind<FileLinesReader>();
				injectionHelper.Bind<GitProjectOverlay>().NonLazy();

				Rebuild(injectionHelper);
			}
			finally
			{
				GitProfilerProxy.EndSample();
			}
		}

		private static void Rebuild(InjectionHelper injectionHelper)
		{
			GitCallbacks = injectionHelper.GetInstance<GitCallbacks>();

			var settingsManager = injectionHelper.GetInstance<GitSettingsManager>();
			settingsManager.LoadGitSettings();

			//delayed called must be used for serialized properties to be loaded
			EditorApplication.delayCall += () =>
			{
				settingsManager.LoadOldSettingsFile();
			};

			GitManager = injectionHelper.GetInstance<GitManager>();
				
			ReflectionHelper = injectionHelper.GetInstance<GitReflectionHelper>();
			GitSettings = injectionHelper.GetInstance<GitSettingsJson>();
				
			GitCallbacks.OnLogEntry += OnLogEntry;
			GitCallbacks.OnBeforeAssemblyReload += OnBeforeAssemblyReload;

			injectionHelper.CreateNonLazy();

			injectionHelper.InjectStatic(typeof(GitProjectContextMenus));
			injectionHelper.InjectStatic(typeof(GitUnityMenu));
		}

		private static void OnWindowAdded(EditorWindow editorWindow)
		{
			injectionHelper.Inject(editorWindow);
			editorWindow.Repaint();
		}

		//emulate Unity's delayed call
		private static void OnEditorUpdate()
		{
			if(GitCallbacks != null) GitCallbacks.IssueDelayCall(true);
		}

		private static void OnBeforeAssemblyReload()
		{
			if(!(bool)ReflectionHelper.TestRunningField.GetValue(null))
				injectionHelper.Dispose();
		}

		private static void OnLogEntry(GitLog.LogEntry logEntry)
		{
			if (!GitSettings.UseUnityConsole)
				GetWindow<GitLogWindow>();
		}

		private static GitCallbacks GetGitCallbacks(InjectionHelper.ResolveCreateContext context)
		{
			var c = new GitCallbacks();
			EditorApplication.update += c.IssueEditorUpdate;
			c.RefreshAssetDatabase += AssetDatabase.Refresh;
			c.SaveAssetDatabase += AssetDatabase.SaveAssets;
			EditorApplication.playModeStateChanged += c.IssueOnPlayModeStateChange;
			EditorApplication.projectWindowItemOnGUI += c.IssueProjectWindowItemOnGUI;
			//asset postprocessing
			GitAssetPostprocessors.OnWillSaveAssetsEvent += c.IssueOnWillSaveAssets;
			GitAssetPostprocessors.OnPostprocessImportedAssetsEvent += c.IssueOnPostprocessImportedAssets;
			GitAssetPostprocessors.OnPostprocessDeletedAssetsEvent += c.IssueOnPostprocessDeletedAssets;
			GitAssetPostprocessors.OnPostprocessMovedAssetsEvent += c.IssueOnPostprocessMovedAssets;
			return c;
		}

		private static UniGitData CreateUniGitData()
		{
			var existentData = Resources.FindObjectsOfTypeAll<UniGitData>();
			foreach (var data in existentData)
			{
				if (data.Initialized) return data;
			}

			return existentData.Length > 0 ? existentData[0] : CreateNewGitData();
		}

		private static UniGitData CreateNewGitData()
		{
			var data = ScriptableObject.CreateInstance<UniGitData>();
			data.hideFlags = HideFlags.HideAndDontSave;
			data.name = "UniGitData";
			return data;
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
		        editorWindow.Show();
				return editorWindow;
	        }
	        var newWindow = ScriptableObject.CreateInstance<T>();
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
            return instance;
        }

		private static void HandlePaths()
		{
			AddPath(UniGitPath.Combine(Environment.CurrentDirectory,"Assets","Plugins","LibGit2"));
			AddPath(UniGitPath.Combine(Environment.CurrentDirectory,"Assets","Plugins","LibGit2","x86"));
			AddPath(UniGitPath.Combine(Environment.CurrentDirectory,"Assets","Plugins","LibGit2","x86_64"));
		}

		private static void AddPath(string path)
		{
			string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
			if(!currentPath.Contains(path))
			{
				Environment.SetEnvironmentVariable("PATH", currentPath + path + Path.PathSeparator, EnvironmentVariableTarget.Process);
			}
		}
	}
}