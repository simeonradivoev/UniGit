using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Windows.Diff
{
	[Serializable]
	public class DiffWindowStatusList : IEnumerable<StatusListEntry>
	{
		[SerializeField] private List<StatusListEntry> entries;
		private readonly GitSettingsJson gitSettings;
		private readonly GitManager gitManager;
		private readonly GitLfsHelper lfsHelper;

        public DiffWindowStatusList()
		{
			entries = new List<StatusListEntry>();
			LockObj = new object();
		}

		public DiffWindowStatusList(GitSettingsJson gitSettings, GitManager gitManager,GitLfsHelper lfsHelper) : this()
		{
			this.gitSettings = gitSettings;
			this.gitManager = gitManager;
			this.lfsHelper = lfsHelper;
		}

		internal void Copy(DiffWindowStatusList other)
		{
			entries.AddRange(other.entries);
		}

		internal void Add(GitStatusEntry entry, IComparer<StatusListEntry> sorter)
		{
			StatusListEntry statusEntry;

			if (UniGitPathHelper.IsMetaPath(entry.LocalPath))
			{
				var mainAssetPath = GitManager.AssetPathFromMeta(entry.LocalPath);
				if (!gitSettings.ShowEmptyFolders && gitManager.IsEmptyFolder(mainAssetPath)) return;

				var index = entries.FindIndex(e => e.LocalPath == mainAssetPath);
				if (index >= 0)
				{
					var ent = entries[index];
					ent.MetaChange |= MetaChangeEnum.Meta;
					ent.State |= entry.Status;
					entries[index] = ent;
					return;
				}

				statusEntry = new StatusListEntry(mainAssetPath, entry.Status, MetaChangeEnum.Meta, CalculateFlags(entry));
			}
			else
			{
				var index = entries.FindIndex(e => e.LocalPath == entry.LocalPath);
				if (index >= 0)
				{
					var ent = entries[index];
					ent.State |= entry.Status;
					entries[index] = ent;
					return;
				}

				statusEntry = new StatusListEntry(entry.LocalPath, entry.Status, MetaChangeEnum.Object, CalculateFlags(entry));
			}

			if (sorter != null) AddSorted(statusEntry, sorter);
			else entries.Add(statusEntry);
		}

		private StatusEntryFlags CalculateFlags(GitStatusEntry entry)
		{
			StatusEntryFlags flags = 0;
			if (lfsHelper.IsLfsPath(entry.LocalPath))
				flags |= StatusEntryFlags.IsLfs;
			if (gitManager.IsSubModule(entry.LocalPath))
				flags |= StatusEntryFlags.IsSubModule;
			return flags;
		}

		private void AddSorted(StatusListEntry entry, IComparer<StatusListEntry> sorter)
		{
			for (var i = 0; i < entries.Count; i++)
			{
				var compare = sorter.Compare(entries[i], entry);
				if (compare > 0)
				{
					entries.Insert(i, entry);
					return;
				}
			}

			entries.Add(entry);
		}

		public void Sort(IComparer<StatusListEntry> sorter)
		{
			entries.Sort(sorter);
		}

		public void RemoveRange(string[] paths)
		{
			foreach (var path in paths)
			{
				if (UniGitPathHelper.IsMetaPath(path))
				{
					var assetPath = GitManager.AssetPathFromMeta(path);
					for (var i = entries.Count - 1; i >= 0; i--)
					{
						var entry = entries[i];
						if (entry.LocalPath == assetPath)
						{
							if (entry.MetaChange.HasFlag(MetaChangeEnum.Object))
							{
								entry.MetaChange = entry.MetaChange.ClearFlags(MetaChangeEnum.Meta);
								entries[i] = entry;
							}
							else
								entries.RemoveAt(i);
						}
					}
				}
				else
				{
					for (var i = entries.Count - 1; i >= 0; i--)
					{
						var entry = entries[i];
						if (entry.LocalPath == path)
						{
							if (entry.MetaChange.HasFlag(MetaChangeEnum.Meta))
							{
								entry.MetaChange = entry.MetaChange.ClearFlags(MetaChangeEnum.Object);
								entries[i] = entry;
							}
							else
								entries.RemoveAt(i);
						}
					}
				}
			}
		}

		public StatusListEntry this[int index] => entries[index];

        public void Clear()
		{
			entries.Clear();
		}

		public object LockObj { get; }

        IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<StatusListEntry> GetEnumerator()
		{
			return entries.GetEnumerator();
		}

		public int Count => entries.Count;
    }

	[Serializable]
	public struct StatusListEntry
	{
		[SerializeField]
		private string localPath;
		[SerializeField]
		private string name;
		[SerializeField]
		private MetaChangeEnum metaChange;
		[SerializeField]
		private FileStatus state;
		[SerializeField]
		private StatusEntryFlags flags;

		public StatusListEntry(string localPath, FileStatus state, MetaChangeEnum metaChange, StatusEntryFlags flags)
		{
			this.localPath = localPath;
			this.name = Path.GetFileName(localPath);
			this.state = state;
			this.metaChange = metaChange;
			this.flags = flags;
		}

		public string GetGuid(GitManager gitManager)
		{
			var projectPath = gitManager.ToProjectPath(localPath);
			return UniGitPathHelper.IsPathInAssetFolder(projectPath) ? AssetDatabase.AssetPathToGUID(projectPath) : projectPath;
		}

		public string LocalPath => localPath;

        public string Name => name;

        public MetaChangeEnum MetaChange
		{
			get => metaChange;
            internal set => metaChange = value;
        }

		public FileStatus State
		{
			get => state;
            internal set => state = value;
        }

		public StatusEntryFlags Flags => flags;
    }

	[Serializable]
	[Flags]
	public enum MetaChangeEnum
	{
		Object = 1 << 0,
		Meta = 1 << 1
	}

	[Serializable]
	[Flags]
	public enum StatusEntryFlags
	{
		IsLfs = 1 << 0,
		IsSubModule = 1 << 1
	}
}