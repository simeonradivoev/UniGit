using System;
using JetBrains.Annotations;
using UnityEditor;

namespace UniGit
{
	public static class GitAssetPostprocessors
	{
		public delegate void OnWillSaveAssetsDelegate(string[] paths, ref string[] outputs);

		public static event OnWillSaveAssetsDelegate OnWillSaveAssetsEvent;
		public static event Action<string[]> OnPostprocessImportedAssetsEvent;
		public static event Action<string[]> OnPostprocessDeletedAssetsEvent;
		public static event Action<string[],string[]> OnPostprocessMovedAssetsEvent;

		public class GitAssetModificationPostprocessor : UnityEditor.AssetModificationProcessor
		{
			[UsedImplicitly]
			private static string[] OnWillSaveAssets(string[] paths)
			{
				var outputs = new string[paths.Length];
				Array.Copy(paths, outputs, paths.Length);
                OnWillSaveAssetsEvent?.Invoke(paths,ref outputs);
                return outputs;
			}
		}

		public class GitBrowserAssetPostprocessor : AssetPostprocessor
		{
			[UsedImplicitly]
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				if(importedAssets.Length > 0) OnPostprocessImportedAssetsEvent?.Invoke(importedAssets);
				if(deletedAssets.Length > 0) OnPostprocessDeletedAssetsEvent?.Invoke(deletedAssets);
				if(movedAssets.Length > 0) OnPostprocessMovedAssetsEvent?.Invoke(movedAssets, movedFromAssetPaths);
			}
		}
	}
}