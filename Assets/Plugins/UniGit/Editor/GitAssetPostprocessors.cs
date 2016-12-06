using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitAssetPostprocessors
	{
		public class GitAssetModificationPostprocessor : UnityEditor.AssetModificationProcessor
		{
			[UsedImplicitly]
			private static string[] OnWillSaveAssets(string[] paths)
			{
				if (EditorPrefs.GetBool("UniGit_DisablePostprocess")) return paths;
				if (GitManager.Settings != null && GitManager.Settings.AutoStage)
				{
					string[] pathsFinal = paths.SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
					if (pathsFinal.Length > 0)
					{
						GitManager.Repository.Stage(pathsFinal);
						GitManager.MarkDirty(pathsFinal);
					}
				}
				return paths;
			}
		}

		public class GitBrowserAssetPostprocessor : AssetPostprocessor
		{
			[UsedImplicitly]
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				if (EditorPrefs.GetBool("UniGit_DisablePostprocess")) return;
				if (GitManager.Repository != null)
				{
					if (GitManager.Settings != null && GitManager.Settings.AutoStage)
					{
						if (importedAssets != null && importedAssets.Length > 0)
						{
							string[] importedAssetsToStage = importedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
							if (importedAssetsToStage.Length > 0)
							{
								GitManager.Repository.Stage(importedAssetsToStage);
								GitManager.MarkDirty(importedAssetsToStage);
							}
						}

						if (movedAssets != null && movedAssets.Length > 0)
						{
							string[] movedAssetsFinal = movedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
							if (movedAssetsFinal.Length > 0)
							{
								GitManager.Repository.Stage(movedAssetsFinal);
								GitManager.MarkDirty(movedAssetsFinal);
							}
						}
					}

					//automatic deletion is necessary even if AutoStage is off
					if (deletedAssets != null && deletedAssets.Length > 0)
					{
						string[] deletedAssetsFinal = deletedAssets.SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanUnstage(GitManager.Repository.RetrieveStatus(g))).ToArray();
						if (deletedAssetsFinal.Length > 0)
						{
							GitManager.Repository.Unstage(deletedAssetsFinal);
							GitManager.MarkDirty(deletedAssetsFinal);
						}
					}
				}
			}
		}
	}
}