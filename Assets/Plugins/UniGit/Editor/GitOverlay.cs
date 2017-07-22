using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEditorInternal;
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
		}

		internal static Icons icons;
		private static GUIStyle IconStyle;
		private static GitManager gitManager;

		internal static void Initlize(GitManager gitManager)
		{
			GitOverlay.gitManager = gitManager;
			EditorApplication.projectWindowItemOnGUI += CustomIcons;

			if (IconStyle == null)
			{
				IconStyle = new GUIStyle
				{
					imagePosition = ImagePosition.ImageOnly,
					alignment = TextAnchor.LowerLeft,
					padding = new RectOffset(2, 2, 2, 2)
				};
			}

			if (icons == null)
			{
				icons = new Icons()
				{
					validIcon = new GUIContent(GitResourceManager.GetTexture("success")),
					validIconSmall = new GUIContent(GitResourceManager.GetTexture("success_small")),
					modifiedIcon = new GUIContent(GitResourceManager.GetTexture("error")),
					modifiedIconSmall = new GUIContent(GitResourceManager.GetTexture("error_small")),
					addedIcon = new GUIContent(GitResourceManager.GetTexture("add")),
					addedIconSmall = new GUIContent(GitResourceManager.GetTexture("add_small")),
					untrackedIcon = new GUIContent(GitResourceManager.GetTexture("info")),
					untrackedIconSmall = new GUIContent(GitResourceManager.GetTexture("info_small")),
					ignoredIcon = new GUIContent(GitResourceManager.GetTexture("minus")),
					ignoredIconSmall = new GUIContent(GitResourceManager.GetTexture("minus_small")),
					conflictIcon = new GUIContent(GitResourceManager.GetTexture("warning")),
					conflictIconSmall = new GUIContent(GitResourceManager.GetTexture("warning_small")),
					deletedIcon = new GUIContent(GitResourceManager.GetTexture("deleted")),
					deletedIconSmall = new GUIContent(GitResourceManager.GetTexture("deleted_small")),
					renamedIcon = new GUIContent(GitResourceManager.GetTexture("renamed")),
					renamedIconSmall = new GUIContent(GitResourceManager.GetTexture("renamed_small")),
					loadingIconSmall = new GUIContent(GitResourceManager.GetTexture("loading")),
					objectIcon = new GUIContent(GitResourceManager.GetTexture("object")),
					objectIconSmall = new GUIContent(GitResourceManager.GetTexture("object_small")),
					metaIcon = new GUIContent(GitResourceManager.GetTexture("meta")),
					metaIconSmall = new GUIContent(GitResourceManager.GetTexture("meta_small")),
					fetch = new GUIContent(GitResourceManager.GetTexture("GitFetch")),
					merge = new GUIContent(GitResourceManager.GetTexture("GitMerge")),
					checkout = new GUIContent(GitResourceManager.GetTexture("GitCheckout")),
					loadingCircle = new GUIContent(GitResourceManager.GetTexture("loading_circle")),
					stashIcon = new GUIContent(GitResourceManager.GetTexture("stash")),
					unstashIcon = new GUIContent(GitResourceManager.GetTexture("unstash")),
				};
			}
		}

		public static GUIContent GetDiffTypeIcon(FileStatus type, bool small)
		{
			GUIContent content = null;

			if (type.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.ModifiedInIndex))
			{
				content = small ? icons.modifiedIconSmall : icons.modifiedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInIndex))
			{
				content = small ? icons.addedIconSmall : icons.addedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInWorkdir))
			{
				content = small ? icons.untrackedIconSmall : icons.untrackedIcon;
			}
			if (type.IsFlagSet(FileStatus.Ignored))
			{
				content = small ? icons.ignoredIconSmall : icons.ignoredIcon;
			}
			if (type.IsFlagSet(FileStatus.Conflicted))
			{
				content = small ? icons.conflictIconSmall : icons.conflictIcon;
			}
			if (type.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				content = small ? icons.renamedIconSmall : icons.renamedIcon;
			}
			if (type.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				content = small ? icons.deletedIconSmall : icons.deletedIcon;
			}
			return content != null ? SetupTooltip(content, type) : GUIContent.none;
		}

		private static GUIContent SetupTooltip(GUIContent content, FileStatus type)
		{
			content.tooltip = type.ToString();
			return content;
		}

		public static GUIContent GetDiffTypeIcon(ChangeKind type, bool small)
		{
			switch (type)
			{
				case ChangeKind.Unmodified:
					return small ? icons.validIconSmall : icons.validIcon;
				case ChangeKind.Added:
					return small ? icons.addedIconSmall : icons.addedIcon;
				case ChangeKind.Deleted:
					return small ? icons.deletedIconSmall : icons.deletedIcon;
				case ChangeKind.Modified:
					return small ? icons.modifiedIconSmall : icons.modifiedIcon;
				case ChangeKind.Ignored:
					return small ? icons.ignoredIconSmall : icons.ignoredIcon;
				case ChangeKind.Untracked:
					return small ? icons.untrackedIconSmall : icons.untrackedIcon;
				case ChangeKind.Conflicted:
					return small ? icons.conflictIconSmall : icons.conflictIcon;
				case ChangeKind.Renamed:
					return small ? icons.renamedIconSmall : icons.renamedIcon;
			}
			return null;
		}

		private static void CustomIcons(string guid, Rect rect)
		{
			if (gitManager.StatusTree == null) return;
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var status = gitManager.StatusTree.GetStatus(path);
			if (status != null)
			{
				Object assetObject = AssetDatabase.LoadMainAssetAtPath(path);
				if (assetObject != null && ProjectWindowUtil.IsFolder(assetObject.GetInstanceID()))
				{
					//exclude the Assets folder
					if (status.Depth == 0) return;
					//todo cache expandedProjectWindowItems into a HashSet for faster Contains
					if (!status.ForceStatus && InternalEditorUtility.expandedProjectWindowItems.Contains(assetObject.GetInstanceID())) return;
				}
				DrawFileIcon(rect, GetDiffTypeIcon(status.State, rect.height <= 16));
			}
		}

		private static void DrawFileIcon(Rect rect, GUIContent icon)
		{
			float width = Mathf.Min(rect.width, 32);
			float height = Mathf.Min(rect.height, 32);
			GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), icon, IconStyle);
		}
	}
}