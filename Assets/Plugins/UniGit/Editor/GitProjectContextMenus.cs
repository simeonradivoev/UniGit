using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;
using Utils.Extensions;

namespace UniGit
{
	public static class GitProjectContextMenus
	{
		[MenuItem("Assets/Git/Add", priority = 50), UsedImplicitly]
		private static void AddSelected()
		{
			string[] paths = Selection.assetGUIDs.Select(g => AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
			GitManager.Repository.Stage(paths);
			GitManager.MarkDirty(paths);
		}

		[MenuItem("Assets/Git/Add", true, priority = 50), UsedImplicitly]
		private static bool AddSelectedValidate()
		{
			string[] paths = Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
			return paths.Any(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Remove", priority = 50), UsedImplicitly]
		private static void RemoveSelected()
		{
			string[] paths = Selection.assetGUIDs.Select(g => AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
			GitManager.Repository.Unstage(paths);
			GitManager.MarkDirty(paths);
		}

		[MenuItem("Assets/Git/Remove", true, priority = 50), UsedImplicitly]
		private static bool RemoveSelectedValidate()
		{
			string[] paths = Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).ToArray();
			return paths.Any(g => GitManager.CanUnstage(GitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Difference", priority = 65), UsedImplicitly]
		private static void SeeDifference()
		{
			GitManager.ShowDiff(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
		}

		[MenuItem("Assets/Git/Difference", true, priority = 65)]
		private static bool SeeDifferenceValidate()
		{
			if (Selection.assetGUIDs.Length != 1) return false;
			var entry = GitManager.Repository.Index[AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])];
			if (entry != null)
			{
				Blob blob = GitManager.Repository.Lookup(entry.Id) as Blob;
				if (blob == null) return false;
				return !blob.IsBinary;
			}
			return false;
		}

		[MenuItem("Assets/Git/Difference with previous version", priority = 65), UsedImplicitly]
		private static void SeeDifferencePrev()
		{
			GitManager.ShowDiffPrev(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
		}

		[MenuItem("Assets/Git/Difference with previous version", true, priority = 65), UsedImplicitly]
		private static bool SeeDifferencePrevValidate()
		{
			return SeeDifferenceValidate();
		}

		[MenuItem("Assets/Git/Revert", priority = 80), UsedImplicitly]
		private static void Revet()
		{
			var paths = Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).SelectMany(e => GitManager.GetPathWithMeta(e)).ToArray();
			if (GitExternalManager.TakeRevert(paths))
			{
				AssetDatabase.Refresh();
				GitManager.MarkDirty(paths);
				return;
			}

			GitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force,OnCheckoutProgress = OnRevertProgress});
			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh();
			GitManager.MarkDirty(paths);
		}

		[MenuItem("Assets/Git/Revert",true, priority = 80), UsedImplicitly]
		private static bool RevetValidate()
		{
			return Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).SelectMany(e => GitManager.GetPathWithMeta(e)).Where(e => File.Exists(e)).Select(e => GitManager.Repository.RetrieveStatus(e)).Any(e => GitManager.CanStage(e) | GitManager.CanUnstage(e));
		}

		private static void OnRevertProgress(string path, int currentSteps, int totalSteps)
		{
			float percent = (float)currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File", string.Format("Reverting file {0} {1}%", path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				GitManager.MarkDirty();
				Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
				EditorWindow.GetWindow(type).ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100), UsedImplicitly]
		private static void BlameObject()
		{
			var path = Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).FirstOrDefault();
			GitManager.ShowBlameWizard(path);
		}

		[MenuItem("Assets/Git/Blame/Object", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameObjectValidate()
		{
			var path = Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).FirstOrDefault();
			return GitManager.CanBlame(path);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100), UsedImplicitly]
		private static void BlameMeta()
		{
			var path = Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).Select(e => AssetDatabase.GetTextMetaFilePathFromAssetPath(e)).FirstOrDefault();
			GitManager.ShowBlameWizard(path);
		}

		[MenuItem("Assets/Git/Blame/Meta", priority = 100,validate = true), UsedImplicitly]
		private static bool BlameMetaValidate()
		{
			var path = Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).Select(e => AssetDatabase.GetTextMetaFilePathFromAssetPath(e)).FirstOrDefault();
			return GitManager.CanBlame(path);
		}
	}
}