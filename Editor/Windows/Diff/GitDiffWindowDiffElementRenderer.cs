using System;
using System.IO;
using System.Linq;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit.Windows.Diff
{
	public class GitDiffWindowDiffElementRenderer : IDiffWindowRenderer, IDisposable
	{
		internal const string SmallElementsKey = "UniGitDiffElementsSmall";
		private readonly GitManager gitManager;

		private readonly GitOverlay gitOverlay;
		private readonly GitSettingsJson gitSettings;
		private readonly GitCallbacks gitCallbacks;
		private bool smallElements;

		private class Styles
		{
			public GUIStyle diffElementSelected;
			public GUIStyle diffElementSmall;
			public GUIStyle diffElementBig;
			public GUIStyle assetIcon;
			public GUIStyle diffElementName;
			public GUIStyle diffElementPath;
			public GUIStyle toggle;
			public Texture folderIcon;
			public Texture defaultAssetIcon;
		}

		private Styles styles;

		[UniGitInject]
		public GitDiffWindowDiffElementRenderer(GitManager gitManager, GitOverlay gitOverlay, GitSettingsJson gitSettings,GitCallbacks gitCallbacks,
			IGitPrefs prefs)
		{
			this.gitManager = gitManager;
			this.gitOverlay = gitOverlay;
			this.gitSettings = gitSettings;
			this.gitCallbacks = gitCallbacks;
			OnPrefsChange(prefs);
			gitCallbacks.OnPrefsChange += OnPrefsChange;
		}

		private void OnPrefsChange(IGitPrefs prefs)
		{
			smallElements = prefs.GetBool(SmallElementsKey,false);
		}

		public void LoadStyles()
		{
			styles = new Styles()
			{
				assetIcon = new GUIStyle("NotificationBackground") {contentOffset = Vector2.zero, alignment = TextAnchor.MiddleCenter, imagePosition = ImagePosition.ImageOnly, padding = new RectOffset(4, 4, 4, 4), border = new RectOffset(12, 12, 12, 12)},
				diffElementSelected = "OL SelectedRow",
				diffElementSmall = new GUIStyle("ProjectBrowserHeaderBgTop") {fixedHeight = 48,margin = new RectOffset(),padding = new RectOffset(4,4,4,4),border = new RectOffset(8,8,8,8)},
				diffElementBig = new GUIStyle("ProjectBrowserHeaderBgTop") {fixedHeight = 60,margin = new RectOffset(),padding = new RectOffset(6,6,6,6),border = new RectOffset(8,8,8,8)},
				diffElementName = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, onNormal = new GUIStyleState() { textColor = Color.white * 0.95f, background = Texture2D.blackTexture } },
				diffElementPath = new GUIStyle(EditorStyles.label) { onNormal = new GUIStyleState() { textColor = Color.white * 0.9f, background = Texture2D.blackTexture }, wordWrap = true, fixedHeight = 0, alignment = TextAnchor.MiddleLeft },
				toggle = new GUIStyle("MenuToggleItem") { fixedHeight = 32,normal = { background = (Texture2D)GitGUI.IconContentTex("toggle@2x") }, onNormal = { background = (Texture2D)GitGUI.IconContentTex("toggle on@2x") }, active = { background = (Texture2D)GitGUI.IconContentTex("toggle act@2x") }, onActive = { background = (Texture2D)GitGUI.IconContentTex("toggle on act@2x") }, fixedWidth = 0, border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset() },
				folderIcon = EditorGUIUtility.IconContent("Folder Icon").image,
				defaultAssetIcon = EditorGUIUtility.IconContent("DefaultAsset Icon").image
			};
		}

		internal void DoFileDiff(Rect rect,StatusListEntry info,bool enabled,bool selected,GitDiffWindow window)
		{
			var elementPadding = GetElementStyle().padding;
			var iconSize = GetElementStyle().fixedHeight - elementPadding.vertical;
			var toggleSize = styles.toggle.fixedHeight;

			var current = Event.current;
			var projectPath = gitManager.ToProjectPath(info.LocalPath);
			var fileName = info.Name;

			GitGUI.StartEnable(enabled);
			var stageToggleRect = new Rect(rect.x + rect.width - toggleSize * 2, rect.y + (rect.height - toggleSize) * 0.5f, toggleSize, toggleSize);
			var canUnstage = GitManager.CanUnstage(info.State);
			var canStage = GitManager.CanStage(info.State);
			var maxPathSize = rect.width - stageToggleRect.width - toggleSize - 21;

			if (current.type == EventType.Repaint)
			{
				(selected ? styles.diffElementSelected : GetElementStyle()).Draw(rect,false,false,false,false);
			}

			if (canStage && canUnstage)
			{
				maxPathSize -= stageToggleRect.width - 4;
				var stageWarnningRect = new Rect(stageToggleRect.x - stageToggleRect.width - 4, stageToggleRect.y, stageToggleRect.width, stageToggleRect.height);
				EditorGUIUtility.AddCursorRect(stageWarnningRect, MouseCursor.Link);
				if (GUI.Button(stageWarnningRect, GitGUI.IconContent("console.warnicon", "", "Unstaged changed pending. Stage to update index."), GUIStyle.none))
				{
					var localPaths = gitManager.GetPathWithMeta(info.LocalPath).ToArray();
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
			}

			if (current.type == EventType.Repaint)
			{
				Object asset = null;
				if(UniGitPathHelper.IsPathInAssetFolder(projectPath))
					asset = AssetDatabase.LoadAssetAtPath(UniGitPathHelper.IsMetaPath(projectPath) ? GitManager.AssetPathFromMeta(projectPath) : projectPath, typeof(Object));

				var extension = Path.GetExtension(projectPath);
				var tmpContent = GUIContent.none;
				if (string.IsNullOrEmpty(extension))
				{
					tmpContent = GitGUI.GetTempContent(styles.folderIcon, "Folder");
				}

				if (tmpContent.image == null)
				{
					if (asset != null)
					{
						tmpContent = GitGUI.GetTempContent(string.Empty,AssetDatabase.GetCachedIcon(projectPath), asset.GetType().Name);
					}
					else
					{
						tmpContent = GitGUI.GetTempContent(styles.defaultAssetIcon, "Unknown Type");
					}
				}

				var x = rect.x + elementPadding.left;
				GUI.Box(new Rect(x, rect.y + elementPadding.top, iconSize,iconSize), tmpContent, styles.assetIcon);
				x += iconSize + 8;

				styles.diffElementName.Draw(new Rect(x, rect.y + elementPadding.top + 2, rect.width - elementPadding.right - iconSize - rect.height, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(fileName), false, selected, selected, false);

				x = rect.x + elementPadding.left + iconSize + 8;
				foreach (var diffTypeIcon in gitOverlay.GetDiffTypeIcons(info.State,false))
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), diffTypeIcon, GUIStyle.none);
					x += 25;
				}
				
				if (info.MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.objectIconSmall.image, "main asset file changed"), GUIStyle.none);
					x += 25;
				}
				if (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.metaIconSmall.image, ".meta file changed"), GUIStyle.none);
					x += 25;
				}
				if (info.Flags.IsFlagSet(StatusEntryFlags.IsLfs))
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.lfsObjectIconSmall.image, "Lfs Object"), GUIStyle.none);
					x += 25;
				}
				if (info.Flags.IsFlagSet(StatusEntryFlags.IsSubModule))
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.submoduleTagIconSmall.image, "Sub Module"), GUIStyle.none);
					x += 25;
				}

				var pathSize = styles.diffElementPath.CalcSize(GitGUI.GetTempContent(projectPath));
				pathSize.x = Mathf.Min(pathSize.x, maxPathSize - x);

				var pathRect = new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight, pathSize.x, EditorGUIUtility.singleLineHeight*2);

				styles.diffElementPath.Draw(pathRect, GitGUI.GetTempContent(projectPath),false, selected, selected, false);
				x += pathRect.width + 4;

				if (!enabled)
				{
					GUI.Box(new Rect(x, rect.y + elementPadding.top + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempSpinAnimatedTexture(),GUIStyle.none);
					//spinning animation needs constant repaint
					if(gitSettings.AnimationType.HasFlag(GitSettingsJson.AnimationTypeEnum.Loading)) window.Repaint();
				}
			}

			if (canUnstage || canStage)
			{
				EditorGUI.BeginChangeCheck();
				EditorGUIUtility.AddCursorRect(stageToggleRect,MouseCursor.Link);
				EditorGUI.Toggle(stageToggleRect,canUnstage, styles.toggle);
				if (EditorGUI.EndChangeCheck())
				{
					var updateFlag = false;
					if (GitManager.CanStage(info.State))
					{
						var paths = gitManager.GetPathWithMeta(info.LocalPath).ToArray();
						if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
						{
							gitManager.AsyncStage(paths).onComplete += (o)=>{ window.Repaint(); };
						}
						else
						{
						    GitCommands.Stage(gitManager.Repository,paths);
							gitManager.MarkDirtyAuto(paths);
						}
						updateFlag = true;
					}
					else if (GitManager.CanUnstage(info.State))
					{
						var paths = gitManager.GetPathWithMeta(info.LocalPath).ToArray();
						if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
						{
							gitManager.AsyncUnstage(paths).onComplete += (o) => { window.Repaint(); };
						}
						else
						{
						    GitCommands.Unstage(gitManager.Repository,paths);
							gitManager.MarkDirtyAuto(paths);

						}
						updateFlag = true;
					}

					if (updateFlag)
					{
						window.Repaint();
						current.Use();
					}
				}
			}
			GitGUI.EndEnable();
		}

		public void Dispose()
		{
			gitCallbacks.OnPrefsChange += OnPrefsChange;
		}

		private GUIStyle GetElementStyle()
		{
			return smallElements ? styles.diffElementSmall : styles.diffElementBig;
		}

		internal float ElementHeight => GetElementStyle().fixedHeight;
    }
}
