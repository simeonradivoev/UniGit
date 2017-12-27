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
				string[] outputs = new string[paths.Length];
				Array.Copy(paths, outputs, paths.Length);
				if (OnWillSaveAssetsEvent != null) OnWillSaveAssetsEvent.Invoke(paths,ref outputs);
				return outputs;
			}
		}

		public class GitBrowserAssetPostprocessor : AssetPostprocessor
		{
			[UsedImplicitly]
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				if(importedAssets.Length > 0 && OnPostprocessImportedAssetsEvent != null) OnPostprocessImportedAssetsEvent.Invoke(importedAssets);
				if(deletedAssets.Length > 0 && OnPostprocessDeletedAssetsEvent != null) OnPostprocessDeletedAssetsEvent.Invoke(deletedAssets);
				if(movedAssets.Length > 0 && OnPostprocessMovedAssetsEvent != null) OnPostprocessMovedAssetsEvent.Invoke(movedAssets, movedFromAssetPaths);
			}
		}
	}
}