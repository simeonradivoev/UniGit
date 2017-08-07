using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;

namespace UniGit
{
	public static class GitAssetPostprocessors
	{
		public class GitAssetModificationPostprocessor : UnityEditor.AssetModificationProcessor
		{
			[UsedImplicitly]
			private static string[] OnWillSaveAssets(string[] paths)
			{
				var gitManager = UniGitLoader.GitManager;

				if (gitManager != null && gitManager.Settings != null)
				{
					if (gitManager.Prefs.GetBool("UniGit_DisablePostprocess")) return paths;
					if (gitManager.Repository != null && paths != null && paths.Length > 0)
					{
						bool autoStage = gitManager.Settings != null && gitManager.Settings.AutoStage;
						string[] pathsFinal = paths.SelectMany(GitManager.GetPathWithMeta).ToArray();
						if (pathsFinal.Length > 0)
						{
							if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
							{
								if (autoStage)
								{
									gitManager.AsyncStage(pathsFinal);
								}
								else
								{
									gitManager.MarkDirty(pathsFinal);
								}
							}
							else
							{
								if (autoStage) Commands.Stage(gitManager.Repository,pathsFinal);
								gitManager.MarkDirty(pathsFinal);
							}
						}
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
				var gitManager = UniGitLoader.GitManager;

				if (gitManager.Prefs.GetBool("UniGit_DisablePostprocess")) return;
				if (gitManager.Repository != null)
				{
					bool autoStage = gitManager.Settings != null && gitManager.Settings.AutoStage;

					if (gitManager.Settings != null)
					{
						if (importedAssets != null && importedAssets.Length > 0)
						{
							string[] importedAssetsToStage = importedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(GitManager.GetPathWithMeta).ToArray();
							if (importedAssetsToStage.Length > 0)
							{
								if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
								{
									if (autoStage)
									{
										gitManager.AsyncStage(importedAssetsToStage);
									}
									else
									{
										gitManager.MarkDirty(importedAssetsToStage);
									}
								}
								else
								{
									if (autoStage) Commands.Stage(gitManager.Repository, importedAssetsToStage);
									gitManager.MarkDirty(importedAssetsToStage);
								}
							}
						}

						if (movedAssets != null && movedAssets.Length > 0)
						{
							string[] movedAssetsFinal = movedAssets.Where(a => !GitManager.IsEmptyFolder(a)).SelectMany(GitManager.GetPathWithMeta).ToArray();
							if (movedAssetsFinal.Length > 0)
							{
								if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
								{
									if (autoStage)
									{
										gitManager.AsyncStage(movedAssetsFinal);
									}
									else
									{
										gitManager.MarkDirty(movedAssetsFinal);
									}
								}
								else
								{
									if (autoStage) Commands.Stage(gitManager.Repository, movedAssetsFinal);
									gitManager.MarkDirty(movedAssetsFinal);
								}
							}
						}
					}

					//automatic deletion of previously moved asset is necessary even if AutoStage is off
					if (movedFromAssetPaths != null && movedFromAssetPaths.Length > 0)
					{
						string[] movedFromAssetPathsFinal = movedFromAssetPaths.SelectMany(GitManager.GetPathWithMeta).ToArray();
						if (movedFromAssetPathsFinal.Length > 0)
						{
							if (gitManager.Settings != null && gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
							{
								gitManager.AsyncUnstage(movedFromAssetPathsFinal);
							}
							else
							{
								Commands.Unstage(gitManager.Repository,movedFromAssetPathsFinal);
								gitManager.MarkDirty(movedFromAssetPathsFinal);
							}
						}
					}

					//automatic deletion is necessary even if AutoStage is off
					if (deletedAssets != null && deletedAssets.Length > 0)
					{
						string[] deletedAssetsFinal = deletedAssets.SelectMany(GitManager.GetPathWithMeta).ToArray();
						if (deletedAssetsFinal.Length > 0)
						{
							if (gitManager.Settings != null && gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
							{
								gitManager.AsyncUnstage(deletedAssetsFinal);
							}
							else
							{
								Commands.Unstage(gitManager.Repository,deletedAssetsFinal);
								gitManager.MarkDirty(deletedAssetsFinal);
							}
						}
					}
				}
			}
		}
	}
}