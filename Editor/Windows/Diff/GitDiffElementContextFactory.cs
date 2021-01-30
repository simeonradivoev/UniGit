using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UniGit.Windows.Diff;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitDiffElementContextFactory
	{
		private readonly GitManager gitManager;
		private readonly GitConflictsHandler conflictsHandler;
		private readonly GitOverlay gitOverlay;
		private readonly GitExternalManager externalManager;
		private readonly GitCallbacks gitCallbacks;

		[UniGitInject]
		public GitDiffElementContextFactory(GitManager gitManager, GitConflictsHandler conflictsHandler, GitOverlay gitOverlay, 
			GitExternalManager externalManager, GitCallbacks gitCallbacks)
		{
			this.gitManager = gitManager;
			this.conflictsHandler = conflictsHandler;
			this.gitOverlay = gitOverlay;
			this.externalManager = externalManager;
			this.gitCallbacks = gitCallbacks;
		}

		internal void Build(IGenericMenu editMenu,GitDiffWindow window)
		{
			var entries = window.GetStatusList().Where(window.IsSelected).ToArray();
			var selectedFlags = entries.Select(e => e.State).CombineFlags();

			var addContent = new GUIContent("Stage", GitGUI.Textures.CollabPush);
			if (GitManager.CanStage(selectedFlags))
			{
				editMenu.AddItem(addContent, false, () => { AddSelectedCallback(window);});
			}
			else
			{
				editMenu.AddDisabledItem(addContent);
			}
			var removeContent = new GUIContent("Unstage", GitGUI.Textures.CollabPull);
			if (GitManager.CanUnstage(selectedFlags))
			{
				editMenu.AddItem(removeContent, false, () => { RemoveSelectedCallback(window);});
			}
			else
			{
				editMenu.AddDisabledItem(removeContent);
			}
			
			editMenu.AddSeparator("");
			var diffIcon = GitGUI.Textures.ZoomTool;
			if (entries.Length >= 1)
			{
				var localPath = entries[0].LocalPath;
				if (selectedFlags.IsFlagSet(FileStatus.Conflicted))
				{
					if (conflictsHandler.CanResolveConflictsWithTool(localPath))
					{
						editMenu.AddItem(new GUIContent("Resolve Conflicts","Resolve merge conflicts"), false, ResolveConflictsCallback, localPath);
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Resolve Conflicts"));
					}
					editMenu.AddItem(new GUIContent("Resolve (Using Ours)"), false, ResolveConflictsOursCallback, localPath);
					editMenu.AddItem(new GUIContent("Resolve (Using Theirs)"), false, ResolveConflictsTheirsCallback, localPath);
				}
				else if(!selectedFlags.IsFlagSet(FileStatus.Ignored))
				{
					if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
					{
						editMenu.AddItem(new GUIContent("Difference/Asset", diffIcon), false, () => { SeeDifferenceObject(entries[0]); });
						editMenu.AddItem(new GUIContent("Difference/Meta", diffIcon), false, () => { SeeDifferenceMeta(entries[0]); });
					}
					else
					{
						editMenu.AddItem(new GUIContent("Difference", diffIcon), false, () => { SeeDifferenceAuto(entries[0]); });
					}

					if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
					{
						editMenu.AddItem(new GUIContent("Difference with previous version/Asset", diffIcon), false, () => { SeeDifferencePrevObject(entries[0]); });
						editMenu.AddItem(new GUIContent("Difference with previous version/Meta", diffIcon), false, () => { SeeDifferencePrevMeta(entries[0]); });
					}
					else
					{
						editMenu.AddItem(new GUIContent("Difference with previous version", diffIcon), false, () => { SeeDifferencePrevAuto(entries[0]); });
					}
				}
				else
				{
					editMenu.AddDisabledItem(new GUIContent("Difference", diffIcon));
					editMenu.AddDisabledItem(new GUIContent("Difference with previous version", diffIcon));
				}
				editMenu.AddSeparator("");
			}

			if(selectedFlags.IsFlagSet(FileStatus.Ignored))
				editMenu.AddDisabledItem(new GUIContent("Revert", GitGUI.Textures.AnimationWindow));
			else if(entries.Length >= 1)
			{
				if(entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					editMenu.AddItem(new GUIContent("Revert/Asset", GitGUI.Textures.AnimationWindow), false, () => RevertSelectedObjects(window));
					editMenu.AddItem(new GUIContent("Revert/Meta", GitGUI.Textures.AnimationWindow), false, () => RevertSelectedMeta(window));
				}
				else
				{
					editMenu.AddItem(new GUIContent("Revert", GitGUI.Textures.AnimationWindow), false, () => RevertSelectedCallback(window));
				}
			}	

			if (entries.Length >= 1)
			{
				editMenu.AddSeparator("");
				if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					if (gitManager.CanBlame(entries[0].State))
					{
						editMenu.AddItem(new GUIContent("Blame/Object", GitGUI.Textures.GameView), false, ()=> {BlameObject(entries[0]);});
						editMenu.AddItem(new GUIContent("Blame/Meta", GitGUI.Textures.GameView), false, ()=> {BlameMeta(entries[0]);});
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Blame", GitGUI.Textures.GameView));
					}
				}
				else if(entries.Length > 0)
				{
					if (gitManager.CanBlame(entries[0].State))
					{
						editMenu.AddItem(new GUIContent("Blame", GitGUI.Textures.GameView), false, ()=> {BlameAuto(entries[0]);});
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Blame", GitGUI.Textures.GameView));
					}
				}
			}
			editMenu.AddSeparator("");
			if (entries.Length >= 1)
			{
				editMenu.AddItem(new GUIContent("Show In Explorer", GitGUI.Textures.FolderIcon), false, () => { EditorUtility.RevealInFinder(gitManager.ToProjectPath(entries[0].LocalPath)); });
			}
			editMenu.AddItem(new GUIContent("Open", GitGUI.Textures.OrbitTool), false, () =>
			{
				AssetDatabase.OpenAsset(entries.Select(e => AssetDatabase.LoadAssetAtPath<Object>(gitManager.ToProjectPath(e.LocalPath))).Where(a => a != null).ToArray());
			});
			editMenu.AddItem(new GUIContent("Delete",gitOverlay.icons.trashIconSmall.image), false, () =>
			{
				foreach (var entry in entries)
				{
					window.DeleteAsset(entry.LocalPath);
				}
			});
			editMenu.AddItem(new GUIContent("Reload", GitGUI.Textures.RotateTool), false, window.ReloadCallback);
		}

		internal void Build(FileStatus fileStatus, GenericMenu menu,GitDiffWindow window)
		{
			menu.AddItem(new GUIContent("Select All"), false, () => SelectFilteredCallback(fileStatus,window));
			if (GitManager.CanStage(fileStatus))
			{
				menu.AddItem(new GUIContent("Add All"), false, () =>
				{
					var paths = window.GetStatusList().Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => gitManager.GetPathWithMeta(s.LocalPath)).ToArray();
					if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
					{
						gitManager.AsyncStage(paths).onComplete += (o) => { window.Repaint(); };
					}
					else
					{
						GitCommands.Stage(gitManager.Repository,paths);
						gitManager.MarkDirtyAuto(paths);
					}
					window.Repaint();
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Add All"));
			}

			if (GitManager.CanUnstage(fileStatus))
			{
				menu.AddItem(new GUIContent("Remove All"), false, () =>
				{
					var paths = window.GetStatusList().Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => gitManager.GetPathWithMeta(s.LocalPath)).ToArray();
					if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
					{
						gitManager.AsyncUnstage(paths).onComplete += (o) => { window.Repaint(); };
					}
					else
					{
						GitCommands.Unstage(gitManager.Repository,paths);
						gitManager.MarkDirtyAuto(paths);
					}
					window.Repaint();
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Remove All"));
			}
		}

		private void SelectFilteredCallback(FileStatus filter,GitDiffWindow window)
		{
			foreach (var entry in window.GetStatusList())
			{
				if(entry.State == filter)
					window.AddSelected(entry);
				else
					window.RemoveSelected(entry);
			}
		}

		private void RevertSelectedCallback(GitDiffWindow window)
		{
			var localPaths = window.GetStatusList().Where(window.IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.LocalPath)).ToArray();
			Revert(localPaths);
		}

		private void RevertSelectedMeta(GitDiffWindow window)
		{
			var metaLocalPaths = window.GetStatusList().Where(window.IsSelected).Select(e => GitManager.MetaPathFromAsset(e.LocalPath)).ToArray();
			Revert(metaLocalPaths);
		}

		private void RevertSelectedObjects(GitDiffWindow window)
		{
			var localPaths = window.GetStatusList().Where(window.IsSelected).Select(e => e.LocalPath).SkipWhile(gitManager.IsDirectory).ToArray();
			Revert(localPaths);
		}

		private void AddSelectedCallback(GitDiffWindow window)
		{
			var localPaths = window.GetStatusList().Where(window.IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.LocalPath)).ToArray();
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
			{
				gitManager.AsyncStage(localPaths).onComplete += (o) => { window.Repaint(); };
			}
			else
			{
				GitCommands.Stage(gitManager.Repository,localPaths);
				gitManager.MarkDirtyAuto(localPaths);
			}
			window.Repaint();
		}

		private void Revert(string[] localPaths)
		{
			if (externalManager.TakeRevert(localPaths.Select(p => gitManager.ToProjectPath(p))))
			{
				gitCallbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirtyAuto(localPaths);
				return;
			}

			try
			{
				gitManager.Repository.CheckoutPaths("HEAD", localPaths, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force, OnCheckoutProgress = OnRevertProgress });
				gitManager.MarkDirtyAuto(localPaths);
				gitCallbacks.IssueAssetDatabaseRefresh();
				GitGUI.ShowNotificationOnWindow<GitDiffWindow>(new GUIContent("Revert Complete!"),false);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private void BlameAuto(StatusListEntry entry)
        {
            gitManager.ShowBlameWizard(
                entry.MetaChange.IsFlagSet(MetaChangeEnum.Object)
                    ? entry.LocalPath
                    : GitManager.MetaPathFromAsset(entry.LocalPath), externalManager);
        }

		private void BlameMeta(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(GitManager.MetaPathFromAsset(entry.LocalPath), externalManager);
		}

		private void BlameObject(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(entry.LocalPath, externalManager);
		}

		private void RemoveSelectedCallback(GitDiffWindow window)
		{
			var localPaths = window.GetStatusList().Where(window.IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.LocalPath)).ToArray();
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
			{
				gitManager.AsyncUnstage(localPaths).onComplete += (o) => { window.Repaint(); };
			}
			else
			{
				GitCommands.Unstage(gitManager.Repository,localPaths);
				gitManager.MarkDirtyAuto(localPaths);
			}
			window.Repaint();
		}

		private void OnRevertProgress(string path,int currentSteps,int totalSteps)
		{
			var percent = (float) currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File",$"Reverting file {path} {(percent * 100):####}%", percent);
			if (currentSteps >= totalSteps)
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private void ResolveConflictsTheirsCallback(object localPath)
		{
			conflictsHandler.ResolveConflicts((string)localPath,MergeFileFavor.Theirs);
		}

		private void ResolveConflictsOursCallback(object localPath)
		{
			conflictsHandler.ResolveConflicts((string)localPath, MergeFileFavor.Ours);
		}

		private void ResolveConflictsCallback(object localPath)
		{
			conflictsHandler.ResolveConflicts((string)localPath, MergeFileFavor.Normal);
		}

		private void SeeDifferenceAuto(StatusListEntry entry)
		{
			if (entry.MetaChange.IsFlagSet(MetaChangeEnum.Object))
			{
				SeeDifferenceObject(entry);
			}
			else
			{
				SeeDifferenceMeta(entry);
			}
		}

		private void SeeDifferenceObject(StatusListEntry entry)
		{
			gitManager.ShowDiff(entry.LocalPath,externalManager);
		}

		private void SeeDifferenceMeta(StatusListEntry entry)
		{
			gitManager.ShowDiff(GitManager.MetaPathFromAsset(entry.LocalPath), externalManager);
		}

		private void SeeDifferencePrevAuto(StatusListEntry entry)
		{
			if (entry.MetaChange.IsFlagSet(MetaChangeEnum.Object))
			{
				SeeDifferencePrevObject(entry);
			}
			else
			{
				SeeDifferencePrevMeta(entry);
			}
		}

		private void SeeDifferencePrevObject(StatusListEntry entry)
		{
			gitManager.ShowDiffPrev(entry.LocalPath, externalManager);
		}

		private void SeeDifferencePrevMeta(StatusListEntry entry)
		{
			gitManager.ShowDiffPrev(GitManager.MetaPathFromAsset(entry.LocalPath), externalManager);
		}
	}
}
