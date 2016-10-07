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
			GitManager.Repository.Stage(Selection.assetGUIDs.Select(g => AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => g.EndsWith(".meta") ? new[] { g } : new[] { g, g + ".meta" }));
			GitManager.Update();
		}

		[MenuItem("Assets/Git/Add", true, priority = 50), UsedImplicitly]
		private static bool AddSelectedValidate()
		{
			return Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).Any(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g)));
		}

		[MenuItem("Assets/Git/Remove", priority = 50), UsedImplicitly]
		private static void RemoveSelected()
		{
			GitManager.Repository.Unstage(Selection.assetGUIDs.Select(g => AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)));
			GitManager.Update();
		}

		[MenuItem("Assets/Git/Remove", true, priority = 50), UsedImplicitly]
		private static bool RemoveSelectedValidate()
		{
			return Selection.assetGUIDs.Select(g => string.IsNullOrEmpty(Path.GetExtension(AssetDatabase.GUIDToAssetPath(g))) ? AssetDatabase.GUIDToAssetPath(g) + ".meta" : AssetDatabase.GUIDToAssetPath(g)).SelectMany(g => GitManager.GetPathWithMeta(g)).Any(g => GitManager.CanUnstage(GitManager.Repository.RetrieveStatus(g)));
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
			GitManager.Repository.CheckoutPaths("HEAD", Selection.assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).SelectMany(e => GitManager.GetPathWithMeta(e)), new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force,OnCheckoutProgress = OnRevertProgress});
			EditorUtility.ClearProgressBar();
			GitManager.Update();
			AssetDatabase.Refresh();
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
				GitManager.Update();
				Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
				EditorWindow.GetWindow(type).ShowNotification(new GUIContent("Revert Complete!"));
			}
		}
	}
}