using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;

namespace UniGit
{
	internal class GitDiffWindowSorter : IComparer<GitDiffWindow.StatusListEntry>
	{
		private readonly GitDiffWindow window;
		private readonly GitManager gitManager;

		public GitDiffWindowSorter(GitDiffWindow window,GitManager gitManager)
		{
			this.window = window;
			this.gitManager = gitManager;
		}

		public int Compare(GitDiffWindow.StatusListEntry x, GitDiffWindow.StatusListEntry y)
		{
			int stateCompare = window.IsGrouping() ? GetPriority(x.State).CompareTo(GetPriority(y.State)) : 0;
			if (stateCompare == 0)
			{
				var settings = window.GitDiffSettings;

				if (settings.sortDir == GitDiffWindow.SortDir.Descending)
				{
					var oldLeft = x;
					x = y;
					y = oldLeft;
				}

				if (settings.unstagedChangesPriority)
				{
					bool canStageX = GitManager.CanStage(x.State);
					bool canUnstageX = GitManager.CanUnstage(x.State);
					bool canStageY = GitManager.CanStage(y.State);
					bool canUnstageY = GitManager.CanUnstage(y.State);

					//prioritize upsaged changes that are pending
					if ((canStageX && canUnstageX) && !(canStageY && canUnstageY))
					{
						return -1;
					}
					if (!(canStageX && canUnstageX) && (canStageY && canUnstageY))
					{
						return 1;
					}
				}

				switch (settings.sortType)
				{
					case GitDiffWindow.SortType.Name:
						stateCompare = string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
						break;
					case GitDiffWindow.SortType.Path:
						stateCompare = string.Compare(x.LocalPath, y.LocalPath, StringComparison.InvariantCultureIgnoreCase);
						break;
					case GitDiffWindow.SortType.ModificationDate:
						DateTime modifedTimeLeft = GetClosest(gitManager.GetPathWithMeta(x.LocalPath).Select(p => File.GetLastWriteTime(UniGitPath.Combine(gitManager.GetCurrentRepoPath(), p))));
						DateTime modifedRightTime = GetClosest(gitManager.GetPathWithMeta(x.LocalPath).Select(p => File.GetLastWriteTime(UniGitPath.Combine(gitManager.GetCurrentRepoPath(),p))));
						stateCompare = DateTime.Compare(modifedRightTime,modifedTimeLeft);
						break;
					case GitDiffWindow.SortType.CreationDate:
						DateTime createdTimeLeft = GetClosest(gitManager.GetPathWithMeta(x.LocalPath).Select(p => File.GetCreationTime(UniGitPath.Combine(gitManager.GetCurrentRepoPath(),p))));
						DateTime createdRightTime = GetClosest(gitManager.GetPathWithMeta(y.LocalPath).Select(p => File.GetCreationTime(UniGitPath.Combine(gitManager.GetCurrentRepoPath(),p))));
						stateCompare = DateTime.Compare(createdRightTime,createdTimeLeft);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			if (stateCompare == 0)
			{
				stateCompare = String.Compare(x.LocalPath, y.LocalPath, StringComparison.Ordinal);
			}
			return stateCompare;
		}

		private DateTime GetClosest(IEnumerable<DateTime> dates)
		{
			DateTime now = DateTime.MaxValue;
			DateTime closest = DateTime.Now;
			long min = long.MaxValue;

			foreach (DateTime date in dates)
				if (Math.Abs(date.Ticks - now.Ticks) < min)
				{
					min = date.Ticks - now.Ticks;
					closest = date;
				}

			return closest;
		}

		private static int GetPriority(FileStatus status)
		{
			if (status.IsFlagSet(FileStatus.Conflicted))
			{
				return -1;
			}
			if (status.IsFlagSet(FileStatus.NewInIndex | FileStatus.NewInWorkdir))
			{
				return 1;
			}
			if (status.IsFlagSet(FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir))
			{
				return 2;
			}
			if (status.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				return 3;
			}
			if (status.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				return 3;
			}
			return 4;
		}
	}
}
