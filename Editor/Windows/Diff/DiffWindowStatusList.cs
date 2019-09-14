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
		private readonly object lockObj;

		public DiffWindowStatusList()
		{
			entries = new List<StatusListEntry>();
			lockObj = new object();
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
				string mainAssetPath = GitManager.AssetPathFromMeta(entry.LocalPath);
				if (!gitSettings.ShowEmptyFolders && gitManager.IsEmptyFolder(mainAssetPath)) return;

				int index = entries.FindIndex(e => e.LocalPath == mainAssetPath);
				if (index >= 0)
				{
					StatusListEntry ent = entries[index];
					ent.MetaChange |= MetaChangeEnum.Meta;
					ent.State |= entry.Status;
					entries[index] = ent;
					return;
				}

				statusEntry = new StatusListEntry(mainAssetPath, entry.Status, MetaChangeEnum.Meta, CalculateFlags(entry));
			}
			else
			{
				int index = entries.FindIndex(e => e.LocalPath == entry.LocalPath);
				if (index >= 0)
				{
					StatusListEntry ent = entries[index];
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
			for (int i = 0; i < entries.Count; i++)
			{
				int compare = sorter.Compare(entries[i], entry);
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
					for (int i = entries.Count - 1; i >= 0; i--)
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
					for (int i = entries.Count - 1; i >= 0; i--)
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

		public StatusListEntry this[int index]
		{
			get { return entries[index]; }
		}

		public void Clear()
		{
			entries.Clear();
		}

		public object LockObj
		{
			get { return lockObj; }
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<StatusListEntry> GetEnumerator()
		{
			return entries.GetEnumerator();
		}

		public int Count
		{
			get { return entries.Count; }
		}
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
			string projectPath = gitManager.ToProjectPath(localPath);
			return UniGitPathHelper.IsPathInAssetFolder(projectPath) ? AssetDatabase.AssetPathToGUID(projectPath) : projectPath;
		}

		public string LocalPath
		{
			get { return localPath; }
		}

		public string Name
		{
			get { return name; }
		}

		public MetaChangeEnum MetaChange
		{
			get { return metaChange; }
			internal set { metaChange = value; }
		}

		public FileStatus State
		{
			get { return state; }
			internal set { state = value; }
		}

		public StatusEntryFlags Flags
		{
			get { return flags; }
		}
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