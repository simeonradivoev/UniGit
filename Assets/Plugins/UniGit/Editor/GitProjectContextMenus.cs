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
			string[] paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).ToArray();
			gitManager.AutoStage(paths);
		}

		[MenuItem("Assets/Git/Add", true, priority = 50), UsedImplicitly]
		private static bool AddSelectedValidate()
		{
			if (initializer == null || !initializer.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).Any(g => GitManager.CanStage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Remove", priority = 50), UsedImplicitly]
		private static void RemoveSelected()
		{
			string[] paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).ToArray();
			gitManager.AutoUnstage(paths);
		}

		[MenuItem("Assets/Git/Remove", true, priority = 50), UsedImplicitly]
		private static bool RemoveSelectedValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).Any(g => GitManager.CanUnstage(gitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Difference", priority = 65), UsedImplicitly]
		private static void SeeDifference()
		{
			gitManager.ShowDiff(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]), externalManager);
		}

		[MenuItem("Assets/Git/Difference", true, priority = 65)]
		private static bool SeeDifferenceValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			if (Selection.assetGUIDs.Length != 1) return false;
			if (gitManager.IsDirectory(Selection.assetGUIDs[0])) return false;
			var entry = gitManager.Repository.Index[AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])];
			if (entry != null)
			{
				Blob blob = gitManager.Repository.Lookup(entry.Id) as Blob;
				if (blob == null) return false;
				return !blob.IsBinary;
			}
			return false;
		}

		[MenuItem("Assets/Git/Difference with previous version", priority = 65), UsedImplicitly]
		private static void SeeDifferencePrev()
		{
			gitManager.ShowDiffPrev(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]), externalManager);
		}

		[MenuItem("Assets/Git/Difference with previous version", true, priority = 65), UsedImplicitly]
		private static bool SeeDifferencePrevValidate()
		{
			return SeeDifferenceValidate();
		}

		[MenuItem("Assets/Git/Revert", priority = 80), UsedImplicitly]
		private static void Revet()
		{
			var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).ToArray();
			if (externalManager.TakeRevert(paths))
			{
				gitCallbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirtyAuto(paths);
				return;
			}

			try
			{
				gitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions()
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
			gitManager.MarkDirtyAuto(paths);
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
			return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).SelectMany(gitManager.GetPathWithMeta).Where(File.Exists).Select(e => gitManager.Repository.RetrieveStatus(e)).Any(e => GitManager.CanStage(e) | GitManager.CanUnstage(e));
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
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			gitManager.ShowBlameWizard(path, externalManager);
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameObjectValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			return gitManager.CanBlame(path);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100), UsedImplicitly]
		private static void BlameMeta()
		{
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.GetTextMetaFilePathFromAssetPath).FirstOrDefault();
			gitManager.ShowBlameWizard(path, externalManager);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameMetaValidate()
		{
			if (gitManager == null || !initializer.IsValidRepo) return false;
			var path = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.GetTextMetaFilePathFromAssetPath).FirstOrDefault();
			return gitManager.CanBlame(path);
		}
	}
}