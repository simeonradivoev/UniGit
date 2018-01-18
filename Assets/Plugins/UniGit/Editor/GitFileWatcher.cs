using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UniGit.Utils;

namespace UniGit
{
	public class GitFileWatcher : IDisposable
	{
		private List<FileSystemWatcher> fileWatchers;
		private readonly GitManager gitManager;
		private readonly GitSettingsJson gitSettings;
		private readonly GitCallbacks gitCallbacks;
		private Regex ignoreFoldersRegex;

		[UniGitInject]
		public GitFileWatcher(string repoPath,
			GitManager gitManager,
			GitCallbacks gitCallbacks,
			GitSettingsJson gitSettings,
			[UniGitInjectOptional] bool trackAssetsPath)
		{
			this.gitManager = gitManager;
			this.gitSettings = gitSettings;
			this.gitCallbacks = gitCallbacks;
			fileWatchers = new List<FileSystemWatcher>();

			string regexPattern = @".*.git$";
			if (!trackAssetsPath) regexPattern += "|.*Assets$";
			ignoreFoldersRegex = new Regex(regexPattern);

			var mainFileWatcher = new FileSystemWatcher(repoPath)
			{
				InternalBufferSize = 4,
				EnableRaisingEvents = gitSettings.TrackSystemFiles,
				IncludeSubdirectories = false,
				NotifyFilter = NotifyFilters.FileName
			};
			fileWatchers.Add(mainFileWatcher);
			Subscribe(mainFileWatcher);

			var repoDirectoryInfo = new DirectoryInfo(repoPath);
			foreach (var directory in repoDirectoryInfo.GetDirectories())
			{
				if (!gitManager.Repository.Ignore.IsPathIgnored(directory.FullName) && ShouldTrackDirectory(directory))
				{
					var fileWatcher = new FileSystemWatcher(directory.FullName)
					{
						InternalBufferSize = 4,
						EnableRaisingEvents = gitSettings.TrackSystemFiles,
						IncludeSubdirectories = true,
						NotifyFilter = NotifyFilters.FileName
					};

					fileWatchers.Add(fileWatcher);
					Subscribe(fileWatcher);
				}
			}

			gitCallbacks.OnSettingsChange += OnSettingsChange;
		}

		private void OnSettingsChange()
		{
			foreach (var fileWatcher in fileWatchers)
			{
				fileWatcher.EnableRaisingEvents = gitSettings.TrackSystemFiles;
			}
		}

		private bool ShouldTrackDirectory(DirectoryInfo directory)
		{
			return !directory.Attributes.HasFlag(FileAttributes.Hidden) && !ignoreFoldersRegex.IsMatch(directory.FullName);
		}

		private void Subscribe(FileSystemWatcher watcher)
		{
			watcher.Created += WatcherActivity;
			watcher.Deleted += WatcherActivity;
			watcher.Changed += WatcherActivity;
			watcher.Renamed += WatcherActivity;
		}

		private void Unsubscribe(FileSystemWatcher watcher)
		{
			watcher.Created -= WatcherActivity;
			watcher.Deleted -= WatcherActivity;
			watcher.Changed -= WatcherActivity;
			watcher.Renamed -= WatcherActivity;
		}

		private void WatcherActivity(object sender, FileSystemEventArgs e)
		{
			if (gitManager != null)
			{
				string relativePath = gitManager.GetRelativePath(e.FullPath);
				if (!gitManager.Repository.Ignore.IsPathIgnored(relativePath) && !gitManager.IsDirectory(relativePath))
				{
					if (e.ChangeType == WatcherChangeTypes.Renamed)
					{
						var relativeOldPath = ((RenamedEventArgs) e).OldFullPath;
						gitManager.MarkDirtyAuto(relativePath);
						gitManager.MarkDirtyAuto(relativeOldPath);
					}
					else
					{
						gitManager.MarkDirtyAuto(relativePath);
					}
				}
			}
		}

		public bool WaitForChange(out WaitForChangedResult result,WatcherChangeTypes types,int timeout)
		{
			foreach (var fileWatcher in fileWatchers)
			{
				var newResult = fileWatcher.WaitForChanged(types, timeout);
				if (newResult.ChangeType != 0 || newResult.TimedOut)
				{
					result = newResult;
					return false;
				}
			}

			result = new WaitForChangedResult();
			return true;
		}

		public void Dispose()
		{
			gitCallbacks.OnSettingsChange -= OnSettingsChange;

			foreach (var fileWatcher in fileWatchers)
			{
				Unsubscribe(fileWatcher);
				fileWatcher.Dispose();
			}
			fileWatchers.Clear();
		}
	}
}
