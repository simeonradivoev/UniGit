using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitProjectContextMenus
	{
		private static GitManager gitManager;
		private static GitExternalManager externalManager;
		private static GitCallbacks gitCallbacks;
		private static GitProjectOverlay gitProjectOverlay;
		private static GitReflectionHelper reflectionHelper;
		private static ILogger logger;
		private static GitInitializer initializer;

		[UniGitInject]
		internal static void Init(GitManager gitManager,
			GitExternalManager externalManager,
			GitCallbacks gitCallbacks,
			ILogger logger,
			GitProjectOverlay gitProjectOverlay,
			GitReflectionHelper reflectionHelper,
			GitInitializer initializer)
		{
			GitProjectContextMenus.gitManager = gitManager;
			GitProjectContextMenus.externalManager = externalManager;
			GitProjectContextMenus.gitCallbacks = gitCallbacks;
			GitProjectContextMenus.logger = logger;
			GitProjectContextMenus.reflectionHelper = reflectionHelper;
			GitProjectContextMenus.gitProjectOverlay = gitProjectOverlay;
			GitProjectContextMenus.initializer = initializer;
		}

		[MenuItem("Assets/Git/Add", priority = 50), UsedImplicitly]
		private static void AddSelected()
		{
			string[] localPaths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p))
				.SelectMany(gitManager.GetPathWithMeta).ToArray();
			gitManager.AutoStage(localPaths);
		}

		[MenuItem("Assets/Git/Add", true, priority = 50), UsedImplicitly]
		private static bool AddSelectedValidate()
		{
			if (initializer == null || !initializer.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p)).SelectMany(gitManager.GetPathWithMeta)
				.Any(g => GitManager.CanStage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Remove", priority = 50), UsedImplicitly]
		private static void RemoveSelected()
		{
			string[] localPaths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p)).SelectMany(gitManager.GetPathWithMeta).ToArray();
			gitManager.AutoUnstage(localPaths);
		}

		[MenuItem("Assets/Git/Remove", true, priority = 50), UsedImplicitly]
		private static bool RemoveSelectedValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p)).SelectMany(gitManager.GetPathWithMeta)
				.Any(g => GitManager.CanUnstage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Difference/Object", priority = 65), UsedImplicitly]
		private static void SeeDifferenceObject()
		{
			gitManager.ShowDiff(gitManager.ToLocalPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])), externalManager);
		}

		[MenuItem("Assets/Git/Difference/Object", true, priority = 65)]
		private static bool SeeDifferenceObjectValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			if (Selection.assetGUIDs.Length != 1) return false;
			string localPath = gitManager.ToLocalPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
			if (gitManager.IsDirectory(localPath)) return false;
			return true;
		}

		[MenuItem("Assets/Git/Difference/Meta", priority = 65), UsedImplicitly]
		private static void SeeDifferenceMeta()
		{
			gitManager.ShowDiff(gitManager.ToLocalPath(GitManager.MetaPathFromAsset(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]))), externalManager);
		}

		[MenuItem("Assets/Git/Difference/Meta", true, priority = 65)]
		private static bool SeeDifferenceMetaValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			if (Selection.assetGUIDs.Length != 1) return false;
			string localPath = gitManager.ToLocalPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
			if (gitManager.IsDirectory(localPath)) return false;
			return true;
		}

		[MenuItem("Assets/Git/Difference with previous version/Object", priority = 65), UsedImplicitly]
		private static void SeeDifferenceObjectPrev()
		{
			gitManager.ShowDiffPrev(gitManager.ToLocalPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])), externalManager);
		}

		[MenuItem("Assets/Git/Difference with previous version/Object", true, priority = 65), UsedImplicitly]
		private static bool SeeDifferenceObjectPrevValidate()
		{
			return SeeDifferenceObjectValidate();
		}

		[MenuItem("Assets/Git/Difference with previous version/Meta", priority = 65), UsedImplicitly]
		private static void SeeDifferenceMetaPrev()
		{
			gitManager.ShowDiffPrev(gitManager.ToLocalPath(GitManager.MetaPathFromAsset(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]))), externalManager);
		}

		[MenuItem("Assets/Git/Difference with previous version/Meta", true, priority = 65), UsedImplicitly]
		private static bool SeeDifferenceMetaPrevValidate()
		{
			return SeeDifferenceMetaValidate();
		}

		[MenuItem("Assets/Git/Revert", priority = 80), UsedImplicitly]
		private static void Revet()
		{
			var localPaths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p)).SelectMany(gitManager.GetPathWithMeta).ToArray();
			if (externalManager.TakeRevert(localPaths))
			{
				gitCallbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirtyAuto(localPaths);
				return;
			}

			try
			{
				gitManager.Repository.CheckoutPaths("HEAD", localPaths, new CheckoutOptions()
				{
					CheckoutModifiers = CheckoutModifiers.Force, 
					OnCheckoutProgress = OnRevertProgress,
					OnCheckoutNotify = gitManager.CheckoutNotifyHandler,
					CheckoutNotifyFlags = CheckoutNotifyFlags.Updated
				});
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			
			gitCallbacks.IssueAssetDatabaseRefresh();
			gitManager.MarkDirtyAuto(localPaths);
			var projectWindow = gitProjectOverlay.ProjectWindows.FirstOrDefault(reflectionHelper.HasFocusFucntion);
			if (projectWindow != null)
			{
				projectWindow.ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		[MenuItem("Assets/Git/Revert",true, priority = 80), UsedImplicitly]
		private static bool RevetValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).Where(File.Exists).Select(p => gitManager.ToLocalPath(p))
				.Select(e => gitManager.Repository.RetrieveStatus(e)).Any(e => GitManager.CanStage(e) | GitManager.CanUnstage(e));
		}

		private static void OnRevertProgress(string path, int currentSteps, int totalSteps)
		{
			float percent = (float)currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File", string.Format("Reverting file {0} {1}%", path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				logger.LogFormat(LogType.Log,"Revert of {0} successful.",path);
			}
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100), UsedImplicitly]
		private static void BlameObject()
		{
			var localPath = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(p => gitManager.ToLocalPath(p)).FirstOrDefault();
			gitManager.ShowBlameWizard(localPath, externalManager);
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameObjectValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			var projectPath = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			return gitManager.CanBlame(gitManager.ToLocalPath(projectPath));
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100), UsedImplicitly]
		private static void BlameMeta()
		{
			var projectPath = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(GitManager.MetaPathFromAsset).FirstOrDefault();
			gitManager.ShowBlameWizard(gitManager.ToLocalPath(projectPath), externalManager);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameMetaValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			var projectPath = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(GitManager.MetaPathFromAsset).FirstOrDefault();
			return gitManager.CanBlame(gitManager.ToLocalPath(projectPath));
		}

		[MenuItem("Assets/Git/Update Module", priority = 120), UsedImplicitly]
		public static void UpdateModule()
		{
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			var window = UniGitLoader.GetWindow<GitSubModuleOptionsWizard>();
			window.Init(path);
		}

		[MenuItem("Assets/Git/Update Module", priority = 120,validate = true), UsedImplicitly]
		public static bool UpdateModuleValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			return gitManager.IsSubModule(Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault());
		}
	}
}