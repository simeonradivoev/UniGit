using System.Collections.Generic;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEngine;

namespace UniGit
{
	public class GitOverlay
	{
		public class Icons
		{
			public GUIContent validIcon;
			public GUIContent validIconSmall;
			public GUIContent modifiedIcon;
			public GUIContent modifiedIconSmall;
			public GUIContent addedIcon;
			public GUIContent addedIconSmall;
			public GUIContent untrackedIcon;
			public GUIContent untrackedIconSmall;
			public GUIContent ignoredIcon;
			public GUIContent ignoredIconSmall;
			public GUIContent conflictIcon;
			public GUIContent conflictIconSmall;
			public GUIContent deletedIcon;
			public GUIContent deletedIconSmall;
			public GUIContent renamedIcon;
			public GUIContent renamedIconSmall;
			public GUIContent loadingIconSmall;
			public GUIContent objectIcon;
			public GUIContent objectIconSmall;
			public GUIContent metaIcon;
			public GUIContent metaIconSmall;
			public GUIContent fetch;
			public GUIContent merge;
			public GUIContent checkout;
			public GUIContent loadingCircle;
			public GUIContent stashIcon;
			public GUIContent unstashIcon;
			public GUIContent lfsObjectIcon;
			public GUIContent lfsObjectIconSmall;
			public GUIContent donateSmall;
			public GUIContent starSmall;
		}

		private Icons m_icons;

		[UniGitInject]
		public GitOverlay(GitResourceManager resourceManager)
		{
			m_icons = new Icons()
			{
				validIcon = new GUIContent(resourceManager.GetTexture("success")),
				validIconSmall = new GUIContent(resourceManager.GetTexture("success_small")),
				modifiedIcon = new GUIContent(resourceManager.GetTexture("error")),
				modifiedIconSmall = new GUIContent(resourceManager.GetTexture("error_small")),
				addedIcon = new GUIContent(resourceManager.GetTexture("add")),
				addedIconSmall = new GUIContent(resourceManager.GetTexture("add_small")),
				untrackedIcon = new GUIContent(resourceManager.GetTexture("info")),
				untrackedIconSmall = new GUIContent(resourceManager.GetTexture("info_small")),
				ignoredIcon = new GUIContent(resourceManager.GetTexture("minus")),
				ignoredIconSmall = new GUIContent(resourceManager.GetTexture("minus_small")),
				conflictIcon = new GUIContent(resourceManager.GetTexture("warning")),
				conflictIconSmall = new GUIContent(resourceManager.GetTexture("warning_small")),
				deletedIcon = new GUIContent(resourceManager.GetTexture("deleted")),
				deletedIconSmall = new GUIContent(resourceManager.GetTexture("deleted_small")),
				renamedIcon = new GUIContent(resourceManager.GetTexture("renamed")),
				renamedIconSmall = new GUIContent(resourceManager.GetTexture("renamed_small")),
				loadingIconSmall = new GUIContent(resourceManager.GetTexture("loading")),
				objectIcon = new GUIContent(resourceManager.GetTexture("object")),
				objectIconSmall = new GUIContent(resourceManager.GetTexture("object_small")),
				metaIcon = new GUIContent(resourceManager.GetTexture("meta")),
				metaIconSmall = new GUIContent(resourceManager.GetTexture("meta_small")),
				fetch = new GUIContent(resourceManager.GetTexture("GitFetch")),
				merge = new GUIContent(resourceManager.GetTexture("GitMerge")),
				checkout = new GUIContent(resourceManager.GetTexture("GitCheckout")),
				loadingCircle = new GUIContent(resourceManager.GetTexture("loading_circle")),
				stashIcon = new GUIContent(resourceManager.GetTexture("stash")),
				unstashIcon = new GUIContent(resourceManager.GetTexture("unstash")),
				lfsObjectIcon = new GUIContent(resourceManager.GetTexture("lfs_object")),
				lfsObjectIconSmall = new GUIContent(resourceManager.GetTexture("lfs_object_small")),
				donateSmall = new GUIContent(resourceManager.GetTexture("donate"), "Donate"),
				starSmall = new GUIContent(resourceManager.GetTexture("star")),
			};
		}

		public GUIContent GetDiffTypeIcon(FileStatus type, bool small)
		{
			GUIContent content = null;

			if (type.IsFlagSet(FileStatus.Ignored))
			{
				content = small ? m_icons.ignoredIconSmall : m_icons.ignoredIcon;
			}
			else if (type.IsFlagSet(FileStatus.NewInIndex))
			{
				content = small ? m_icons.addedIconSmall : m_icons.addedIcon;
			}
			else if (type.IsFlagSet(FileStatus.NewInWorkdir))
			{
				content = small ? m_icons.untrackedIconSmall : m_icons.untrackedIcon;
			}
			else if (type.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.ModifiedInIndex))
			{
				content = small ? m_icons.modifiedIconSmall : m_icons.modifiedIcon;
			}
			else if (type.IsFlagSet(FileStatus.Conflicted))
			{
				content = small ? m_icons.conflictIconSmall : m_icons.conflictIcon;
			}
			else if (type.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				content = small ? m_icons.renamedIconSmall : m_icons.renamedIcon;
			}
			else if (type.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				content = small ? m_icons.deletedIconSmall : m_icons.deletedIcon;
			}
			return content != null ? SetupTooltip(content, type) : GUIContent.none;
		}

		public IEnumerable<GUIContent> GetDiffTypeIcons(FileStatus type, bool small)
		{
			if (type.IsFlagSet(FileStatus.Ignored))
			{
				yield return SetupTooltip(small ? m_icons.ignoredIconSmall : m_icons.ignoredIcon,type);
			}
			if (type.IsFlagSet(FileStatus.NewInIndex))
			{
				yield return SetupTooltip(small ? m_icons.addedIconSmall : m_icons.addedIcon,type);
			}
			if (type.IsFlagSet(FileStatus.NewInWorkdir))
			{
				yield return SetupTooltip(small ? m_icons.untrackedIconSmall : m_icons.untrackedIcon,type);
			}
			if (type.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.ModifiedInIndex))
			{
				yield return SetupTooltip(small ? m_icons.modifiedIconSmall : m_icons.modifiedIcon,type);
			}
			if (type.IsFlagSet(FileStatus.Conflicted))
			{
				yield return SetupTooltip(small ? m_icons.conflictIconSmall : m_icons.conflictIcon,type);
			}
			if (type.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				yield return SetupTooltip(small ? m_icons.renamedIconSmall : m_icons.renamedIcon,type);
			}
			if (type.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				yield return SetupTooltip(small ? m_icons.deletedIconSmall : m_icons.deletedIcon,type);
			}
		}

		private GUIContent SetupTooltip(GUIContent content, FileStatus type)
		{
			content.tooltip = type.ToString();
			return content;
		}

		public GUIContent GetDiffTypeIcon(ChangeKind type, bool small)
		{
			switch (type)
			{
				case ChangeKind.Unmodified:
					return small ? m_icons.validIconSmall : m_icons.validIcon;
				case ChangeKind.Added:
					return small ? m_icons.addedIconSmall : m_icons.addedIcon;
				case ChangeKind.Deleted:
					return small ? m_icons.deletedIconSmall : m_icons.deletedIcon;
				case ChangeKind.Modified:
					return small ? m_icons.modifiedIconSmall : m_icons.modifiedIcon;
				case ChangeKind.Ignored:
					return small ? m_icons.ignoredIconSmall : m_icons.ignoredIcon;
				case ChangeKind.Untracked:
					return small ? m_icons.untrackedIconSmall : m_icons.untrackedIcon;
				case ChangeKind.Conflicted:
					return small ? m_icons.conflictIconSmall : m_icons.conflictIcon;
				case ChangeKind.Renamed:
					return small ? m_icons.renamedIconSmall : m_icons.renamedIcon;
			}
			return null;
		}

		public Icons icons
		{
			get { return m_icons; }
		}
	}
}