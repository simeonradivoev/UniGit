using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using Utils.Extensions;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace UniGit
{
	public class GitDiffWindow : GitUpdatableWindow
	{
		[MenuItem("Window/GIT Diff Window")]
		public static void CreateEditor()
		{
			GetWindow(true);
		}

		public static GitDiffWindow GetWindow(bool focus)
		{
			return GetWindow<GitDiffWindow>("Git Diff", focus);
		}

		public Rect CommitRect { get { return new Rect(0,0,position.width, commitMaximized ? 48 + CalculateCommitTextHeight() : 46);} }
		public Rect DiffToolbarRect { get { return new Rect(0, CommitRect.height, position.width, 18); } }
		public Rect DiffRect { get { return new Rect(0,CommitRect.height + DiffToolbarRect.height, position.width,position.height - CommitRect.height - DiffToolbarRect.height);} }

		private const string CommitMessageKey = "UniGitCommitMessage";
		private const string CommitMessageUndoGroup = "Commit Message Change";

		[SerializeField] private Vector2 diffScroll;
		[SerializeField] private bool commitMaximized = true;
		[SerializeField] private string filter = "";
		[SerializeField] private Vector2 commitScroll;

		private SerializedObject editoSerializedObject;
		private Rect commitsRect;
		private Styles styles;
		[SerializeField] private Settings settings;
		private int lastSelectedIndex;
		private StatusList statusList;
		private object statusListLock = new object();
		//cached data path for threading purposes
		private static string cachedDataPath;
		private char commitMessageLastChar;

		[Serializable]
		public class Settings
		{
			public FileStatus showFileStatusTypeFilter = (FileStatus)(-1);
			public FileStatus MinimizedFileStatus = (FileStatus)(-1);
			public SortType sortType = SortType.ModificationDate;
			public SortDir sortDir = SortDir.Ascending;
			public bool emptyCommit;
			public bool amendCommit;
			public bool prettify;
			public bool readFromFile;
			public string commitMessage;
			public string commitMessageFromFile;
			public DateTime lastMessageUpdate;
		}

		private class Styles
		{
			public GUIStyle commitTextArea;
			public GUIStyle assetIcon;
			public GUIStyle diffScrollHeader;
			public GUIStyle diffElementName;
			public GUIStyle diffElementPath;
			public GUIStyle diffElement;
			public GUIStyle toggle;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			editoSerializedObject = new SerializedObject(this);
			if (settings == null) settings = new Settings();
			ReadCommitMessage();
			ReadCommitMessageFromFile();
			if (Undo.GetCurrentGroupName() == CommitMessageUndoGroup)
			{
				Undo.RegisterFullObjectHierarchyUndo(this, "Commit Message Changed");
			}
			cachedDataPath = Application.dataPath;
		}

		protected override void OnGitUpdate(GitRepoStatus status,string[] paths)
		{
			ThreadPool.QueueUserWorkItem(CreateStatusListThreaded, status);
		}

		private void UpdateStatusList()
		{
			if(GitManager.Repository == null) return;
			ThreadPool.QueueUserWorkItem(CreateStatusListThreaded, GitManager.LastStatus);
		}

		protected override void OnInitialize()
		{
		
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			
		}

		protected override void OnEditorUpdate()
		{
			
		}

		private void CreateStatusListThreaded(object param)
		{
			Monitor.Enter(statusListLock);
			try
			{
				GitRepoStatus status = (GitRepoStatus)param ?? new GitRepoStatus(GitManager.Repository.RetrieveStatus());
				statusList = new StatusList(status, settings.showFileStatusTypeFilter, settings.sortType, settings.sortDir);
				GitManager.ActionQueue.Enqueue(Repaint);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				Monitor.Exit(statusListLock);
			}
		}

		[UsedImplicitly]
		private void OnUnfocus()
		{
			if(!GitManager.IsValidRepo) return;
			if(statusList != null) statusList.SelectAll(false);
		}

		[UsedImplicitly]
		protected override void OnFocus()
		{
			base.OnFocus();
			GUI.FocusControl(null);

			if (settings.readFromFile)
			{
				if (File.Exists(CommitMessageFilePath))
				{
					var lastWriteTime = File.GetLastWriteTime(CommitMessageFilePath);
					if (lastWriteTime.CompareTo(settings.lastMessageUpdate) != 0)
					{
						settings.lastMessageUpdate = lastWriteTime;
						ReadCommitMessageFromFile();
						EditorGUI.FocusTextInControl("");
					}
				}
			}
		}

		private void CreateStyles()
		{
			if (styles == null)
			{
				GitProfilerProxy.BeginSample("Git Diff Window Style Creation",this);
				styles = new Styles();
				styles.commitTextArea = new GUIStyle("sv_iconselector_labelselection") {margin = new RectOffset(4, 4, 4, 4), normal = {textColor = Color.black}, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4)};
				styles.assetIcon = new GUIStyle("NotificationBackground") {contentOffset = Vector2.zero, alignment = TextAnchor.MiddleCenter,imagePosition = ImagePosition.ImageOnly,padding = new RectOffset(4,4,4,4),border = new RectOffset(12,12,12,12)};
				styles.diffScrollHeader = new GUIStyle("CurveEditorBackground") {contentOffset = new Vector2(48,0),alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold, normal = {textColor = Color.white * 0.9f},padding = new RectOffset(12,12,12,12),imagePosition = ImagePosition.ImageLeft};
				styles.diffElementName = new GUIStyle(EditorStyles.boldLabel) {fontSize = 12,onNormal = new GUIStyleState() {textColor = Color.white * 0.95f,background = Texture2D.blackTexture} };
				styles.diffElementPath = new GUIStyle(EditorStyles.label) {onNormal = new GUIStyleState() { textColor = Color.white * 0.9f, background = Texture2D.blackTexture } };
				styles.diffElement = new GUIStyle("ProjectBrowserHeaderBgTop") {fixedHeight = 0,border = new RectOffset(8,8,8,8)};
				styles.toggle = new GUIStyle("IN Toggle") {normal = {background = (Texture2D)EditorGUIUtility.IconContent("toggle@2x").image },onNormal = {background = (Texture2D)EditorGUIUtility.IconContent("toggle on@2x").image },active = {background = (Texture2D)EditorGUIUtility.IconContent("toggle act@2x").image}, onActive = { background = (Texture2D)EditorGUIUtility.IconContent("toggle on act@2x").image }, fixedHeight = 0,fixedWidth = 0,border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset()};
				GitProfilerProxy.EndSample();
			}
		}

		[UsedImplicitly]
		private void OnGUI()
		{
			CreateStyles();

			if (!GitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI();
				return;
			}

			if (GitManager.Repository == null) return;
			RepositoryInformation repoInfo = GitManager.Repository.Info;
			GUILayout.BeginArea(CommitRect);
			DoCommit(repoInfo);
			GUILayout.EndArea();

			if (statusList == null) return;
			DoDiffScroll(Event.current);

			editoSerializedObject.ApplyModifiedProperties();

			if (Event.current.type == EventType.MouseDown)
			{
				GUIUtility.keyboardControl = 0;
				GUI.FocusControl(null);
			}
		}

		private void DoCommit(RepositoryInformation repoInfo)
		{
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			if (repoInfo.CurrentOperation == CurrentOperation.Merge)
				GUILayout.Label(GitGUI.GetTempContent("Merge"), "AssetLabel");
			commitMaximized = GUILayout.Toggle(commitMaximized, GitGUI.GetTempContent(settings.readFromFile ? "File Commit Message: (Read Only)" : "Commit Message: "), "IN Foldout",GUILayout.Width(settings.readFromFile ? 210 : 116));
			if (!commitMaximized)
			{
				if (!settings.readFromFile)
				{
					EditorGUI.BeginChangeCheck();
					GUI.SetNextControlName("Commit Message Field");
					settings.commitMessage = EditorGUILayout.TextArea(settings.commitMessage, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					if (EditorGUI.EndChangeCheck())
					{
						SaveCommitMessage();
					}
				}
				else
				{
					GUILayout.Label(new GUIContent(settings.commitMessageFromFile), GUI.skin.textArea, GUILayout.ExpandHeight(true));
				}
			}
			EditorGUILayout.EndHorizontal();
			if (commitMaximized)
			{
				commitScroll = EditorGUILayout.BeginScrollView(commitScroll, GUILayout.Height(CalculateCommitTextHeight()));
				if (!settings.readFromFile)
				{
					EditorGUI.BeginChangeCheck();
					GUI.SetNextControlName("Commit Message Field");
					string newCommitMessage = EditorGUILayout.TextArea(settings.commitMessage, GUILayout.ExpandHeight(true));
					if (EditorGUI.EndChangeCheck())
					{
						if ((Event.current.character == ' ' || Event.current.character == '\0') && !(commitMessageLastChar == ' ' || commitMessageLastChar == '\0'))
						{
							if (Undo.GetCurrentGroupName() == CommitMessageUndoGroup)
							{
								Undo.IncrementCurrentGroup();
							}
						}
						commitMessageLastChar = Event.current.character;
						Undo.RecordObject(this, CommitMessageUndoGroup);
						settings.commitMessage = newCommitMessage;
						SaveCommitMessage();
					}
				}
				else
				{
					GUILayout.Label(new GUIContent(settings.commitMessageFromFile), GUI.skin.textArea,GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndScrollView();
			}

			EditorGUILayout.BeginHorizontal();
			
			if (GUILayout.Button(GitGUI.GetTempContent("Commit"), "DropDownButton"))
			{
				GenericMenu commitMenu = new GenericMenu();
				commitMenu.AddItem(new GUIContent("Commit"),false, CommitCallback);
				if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Commit))
				{
					commitMenu.AddItem(new GUIContent("Commit And Push"), false, CommitAndPushCallback);
				}
				else
				{
					commitMenu.AddDisabledItem(new GUIContent("Commit And Push"));
				}
				commitMenu.AddSeparator("");
				commitMenu.AddItem(new GUIContent("Commit Message/Clear"),false, ClearCommitMessage);
				commitMenu.AddItem(new GUIContent("Commit Message/Read from file"),settings.readFromFile, ToggleReadFromFile);
				if (File.Exists(CommitMessageFilePath))
				{
					commitMenu.AddItem(new GUIContent("Commit Message/Open File"), false, OpenCommitMessageFile);
				}
				else
				{
					commitMenu.AddDisabledItem(new GUIContent("Commit Message/Open File"));
				}
				commitMenu.AddItem(new GUIContent("Commit Message/Reload"),false,ReadCommitMessage);
				commitMenu.ShowAsContext();
			}
			GitGUI.StartEnable(!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Commit));
			settings.emptyCommit = GUILayout.Toggle(settings.emptyCommit, GitGUI.GetTempContent("Empty Commit", "Commit the message only without changes"));
			EditorGUI.BeginChangeCheck();
			settings.amendCommit = GUILayout.Toggle(settings.amendCommit, GitGUI.GetTempContent("Amend Commit", "Amend previous commit."));
			if (EditorGUI.EndChangeCheck())
			{
				if (settings.amendCommit)
				{
					if (string.IsNullOrEmpty(settings.commitMessage))
					{
						settings.commitMessage = GitManager.Repository.Head.Tip.Message;
						SaveCommitMessage();
					}

					if (string.IsNullOrEmpty(settings.commitMessageFromFile))
					{
						settings.commitMessageFromFile = GitManager.Repository.Head.Tip.Message;
						SaveCommitMessageToFile();
					}
				}
			}
			settings.prettify = GUILayout.Toggle(settings.prettify, GitGUI.GetTempContent("Prettify", "Prettify the commit message"));
			GitGUI.EndEnable();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(EditorGUIUtility.IconContent("_Help"),"IconButton"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit/wiki/Features-and-Usage#file-difference-and-committing");
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		private float CalculateCommitTextHeight()
		{
			string commitMessage = GetActiveCommitMessage(false);
			return Mathf.Clamp(GUI.skin.textArea.CalcHeight(GitGUI.GetTempContent(commitMessage), position.width) + EditorGUIUtility.singleLineHeight, 50, GitManager.Settings.MaxCommitTextAreaSize);
		}

		private bool Commit()
		{
			Signature signature = GitManager.Signature;
			try
			{
				string commitMessage = GetActiveCommitMessage(true);
				if (!GitExternalManager.TakeCommit(commitMessage))
				{
					GitProfilerProxy.BeginSample("Git Commit");
					GitManager.Repository.Commit(commitMessage, signature, signature, new CommitOptions() { AllowEmptyCommit = settings.emptyCommit, AmendPreviousCommit = settings.amendCommit, PrettifyMessage = settings.prettify });
					GitProfilerProxy.EndSample();
					GitHistoryWindow.GetWindow(true);
				}
				GitManager.MarkDirty();
				return true;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				GUI.FocusControl("");
				ClearCommitMessage();
				//reset amend commit so the user will have to enable it again to load the last commit message
				settings.amendCommit = false;
			}
			return false;
		}

		private string GetActiveCommitMessage(bool forceUpdate)
		{
			if (settings.readFromFile)
			{
				if (forceUpdate)
				{
					ReadCommitMessageFromFile();
				}
				return settings.commitMessageFromFile;
				
			}
			else
			{
				if (forceUpdate)
				{
					ReadCommitMessage();
				}
				return settings.commitMessage;
			}
		}

		private void ToggleReadFromFile()
		{
			if (settings.readFromFile)
			{
				settings.readFromFile = false;
				ReadCommitMessage();
			}
			else
			{
				settings.readFromFile = true;
				ReadCommitMessageFromFile();
			}
		}

		private void OpenCommitMessageFile()
		{
			if (File.Exists(CommitMessageFilePath))
			{
				System.Diagnostics.Process.Start(CommitMessageFilePath);
			}
		}

		private void ClearCommitMessage()
		{
			settings.commitMessage = string.Empty;
			settings.commitMessageFromFile = string.Empty;
			SaveCommitMessageToFile();
			SaveCommitMessage();
		}

		private void CommitCallback()
		{
			if (EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes?", "Commit","Cancel"))
			{
				Commit();
			}
		}

		private void CommitAndPushCallback()
		{
			if (GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Commit) || EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes and then push them?", "Commit and Push","Cancel"))
			{
				if (Commit())
				{
					ScriptableWizard.DisplayWizard<GitPushWizard>("Push", "Push");
				}
			}
		}

		private const float elementTopBottomMargin = 8;
		private const float elementSideMargin = 8;
		private const float iconSize = 48;
		private const float elementHeight = iconSize + elementTopBottomMargin * 2;
		private Rect diffScrollContentRect;

		private void DoDiffScroll(Event current)
		{
			float totalTypesCount = statusList.Select(i => GetMergedStatus(i.State)).Distinct().Count();
			float elementsTotalHeight = (statusList.Count(i => IsVisible(i)) + totalTypesCount)  * elementHeight;

			GUILayout.BeginArea(DiffToolbarRect, GUIContent.none, "Toolbar");
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(GitGUI.GetTempContent("Edit"), "TE ToolbarDropDown",GUILayout.MinWidth(64)))
			{
				GenericMenuWrapper editMenu = new GenericMenuWrapper(new GenericMenu());
				DoDiffElementContex(editMenu);
				editMenu.GenericMenu.ShowAsContext();
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Filter"), "TE ToolbarDropDown", GUILayout.MinWidth(64)))
			{
				GenericMenu genericMenu = new GenericMenu();
				FileStatus[] fileStatuses = (FileStatus[]) Enum.GetValues(typeof (FileStatus));
				genericMenu.AddItem(new GUIContent("Show All"), settings.showFileStatusTypeFilter == (FileStatus)(-1), () =>
				{
					settings.showFileStatusTypeFilter = (FileStatus)(-1);
					UpdateStatusList();
				});
				genericMenu.AddItem(new GUIContent("Show None"), settings.showFileStatusTypeFilter == 0, () =>
				{
					settings.showFileStatusTypeFilter = 0;
					UpdateStatusList();
				});
				for (int i = 0; i < fileStatuses.Length; i++)
				{
					FileStatus flag = fileStatuses[i];
					genericMenu.AddItem(new GUIContent(flag.ToString()), settings.showFileStatusTypeFilter != (FileStatus)(-1) && settings.showFileStatusTypeFilter.IsFlagSet(flag),()=>
					{
						settings.showFileStatusTypeFilter = settings.showFileStatusTypeFilter.SetFlags(flag, !settings.showFileStatusTypeFilter.IsFlagSet(flag));
						UpdateStatusList();
					});
				}
				genericMenu.ShowAsContext();
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Sort"), "TE ToolbarDropDown", GUILayout.MinWidth(64)))
			{
				GenericMenu genericMenu = new GenericMenu();
				foreach (SortType type in Enum.GetValues(typeof(SortType)))
				{
					genericMenu.AddItem(new GUIContent(type.GetDescription()), type == settings.sortType, () =>
					{
						settings.sortType = type;
						UpdateStatusList();
					});
				}
				genericMenu.AddSeparator("");
				foreach (SortDir dir in Enum.GetValues(typeof(SortDir)))
				{
					genericMenu.AddItem(new GUIContent(dir.GetDescription()), dir == settings.sortDir, () =>
					{
						settings.sortDir = dir;
						UpdateStatusList();
					});
				}
				genericMenu.ShowAsContext();
			}
			GUILayout.FlexibleSpace();
			filter = EditorGUILayout.TextField(GUIContent.none, filter, "ToolbarSeachTextField");
			if (string.IsNullOrEmpty(filter))
			{
				GUILayout.Box(GUIContent.none, "ToolbarSeachCancelButtonEmpty");
			}
			else
			{
				if (GUILayout.Button(GUIContent.none, "ToolbarSeachCancelButton"))
				{
					filter = "";
					GUI.FocusControl("");
				}
			}
			EditorGUILayout.EndHorizontal();
			GUILayout.EndArea();

			diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16,512), elementsTotalHeight);
			diffScroll = GUI.BeginScrollView(DiffRect, diffScroll, diffScrollContentRect);

			int index = 0;
			FileStatus? lastFileStatus = null;
			float infoX = 0;

			foreach (var info in statusList)
			{
				FileStatus mergedStatus = GetMergedStatus(info.State);
				bool isExpanded = IsVisible(info);
				bool isUpdating = GitManager.IsFileUpdating(info.Path);
				Rect elementRect;

				if (!lastFileStatus.HasValue || lastFileStatus != mergedStatus)
				{
					elementRect = new Rect(0, infoX, diffScrollContentRect.width + 16, elementHeight);
					lastFileStatus = mergedStatus;
					FileStatus newState = lastFileStatus.Value;
					if (current.type == EventType.Repaint)
					{
						styles.diffScrollHeader.Draw(elementRect, GitGUI.GetTempContent(mergedStatus.ToString()), false,false,false,false);
						GUIStyle.none.Draw(new Rect(elementRect.x + 12, elementRect.y + 14, elementRect.width - 12, elementRect.height - 24), GitGUI.GetTempContent(GitOverlay.GetDiffTypeIcon(info.State, false).image),false,false,false,false);
						((GUIStyle) "ProjectBrowserSubAssetExpandBtn").Draw(new Rect(elementRect.x + elementRect.width + 32, elementRect.y, 24, 24), GUIContent.none, false, isExpanded, isExpanded, false);
					}

					if (elementRect.Contains(current.mousePosition))
					{
						if (current.type == EventType.ContextClick)
						{
							GenericMenu selectAllMenu = new GenericMenu();
							DoDiffStatusContex(newState, selectAllMenu);
							selectAllMenu.ShowAsContext();
							current.Use();
						}
						else if(current.type == EventType.MouseDown && current.button == 0)
						{
							settings.MinimizedFileStatus = settings.MinimizedFileStatus.SetFlags(mergedStatus, !isExpanded);
							if (!isExpanded)
							{
								statusList.SelectAll(e => e.State == newState, false);
							}
							Repaint();
							current.Use();
						}
						
					}
					infoX += elementRect.height;
				}

				if (!isExpanded) continue;
				elementRect = new Rect(0, infoX, diffScrollContentRect.width + 16, elementHeight);
				DoFileDiff(elementRect,info, isUpdating);
				DoFileDiffSelection(elementRect,info,index, isUpdating);
				infoX += elementRect.height;
				index++;
			}
			GUI.EndScrollView();

			if (Event.current.type == EventType.mouseDrag && DiffRect.Contains(Event.current.mousePosition))
			{
				diffScroll.y -= Event.current.delta.y;
				Repaint();
			}
		}

		private void DoFileDiff(Rect rect,StatusListEntry info,bool isUpdating)
		{
			Event current = Event.current;
			
			if (rect.y > DiffRect.height + diffScroll.y || rect.y + rect.height < diffScroll.y)
			{
				return;
			}

			GitGUI.StartEnable(!isUpdating);
			Rect stageToggleRect = new Rect(rect.x + rect.width - 64, rect.y + 16, 32, 32);
			bool canUnstage = GitManager.CanUnstage(info.State);
			bool canStage = GitManager.CanStage(info.State);

			if (current.type == EventType.Repaint)
			{
				string filePath = info.Path;
				string fileName = info.Name;

				Object asset = null;
				if (filePath.EndsWith(".meta"))
				{
					asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPathFromTextMetaFilePath(filePath), typeof (Object));
				}
				else
				{
					asset = AssetDatabase.LoadAssetAtPath(filePath, typeof (Object));
				}

				GUI.SetNextControlName(info.Path);
				GUI.Box(rect, GUIContent.none, info.Selected ? "TL LogicBar 1" : styles.diffElement);

				string extension = Path.GetExtension(filePath);
				GUIContent tmpContent = GUIContent.none;
				if (string.IsNullOrEmpty(extension))
				{
					tmpContent = GitGUI.GetTempContent(EditorGUIUtility.IconContent("Folder Icon").image, string.Empty, "Folder");
				}

				if (tmpContent.image == null)
				{
					if (asset != null)
					{
						tmpContent = GitGUI.GetTempContent(AssetPreview.GetMiniThumbnail(asset), string.Empty, asset.GetType().Name);
					}
					else
					{
						tmpContent = GitGUI.GetTempContent(EditorGUIUtility.IconContent("DefaultAsset Icon").image, string.Empty, "Unknown Type");
					}
				}

				float x = rect.x + elementSideMargin;
				GUI.Box(new Rect(x, rect.y + elementTopBottomMargin, iconSize, iconSize), tmpContent, styles.assetIcon);
				x += iconSize + 8;

				styles.diffElementName.Draw(new Rect(x, rect.y + elementTopBottomMargin + 2, rect.width - elementSideMargin - iconSize - rect.height, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(fileName), false, info.Selected, info.Selected, false);

				x = rect.x + elementSideMargin + iconSize + 8;
				GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitOverlay.GetDiffTypeIcon(info.State, false), GUIStyle.none);
				x += 25;
				if (info.MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(GitOverlay.icons.objectIconSmall.image,string.Empty, "main asset file changed"), GUIStyle.none);
					x += 25;
				}
				if (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(GitOverlay.icons.metaIconSmall.image,string.Empty, ".meta file changed"), GUIStyle.none);
					x += 25;
				}


				float pathMinWidth,pathMaxWidth;
				styles.diffElementPath.CalcMinMaxWidth(GitGUI.GetTempContent(filePath), out pathMinWidth, out pathMaxWidth);
				Rect pathRect = new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 7, pathMinWidth, EditorGUIUtility.singleLineHeight);
				styles.diffElementPath.Draw(pathRect, GitGUI.GetTempContent(filePath),false, info.Selected, info.Selected, false);
				x += pathRect.width + 4;

				if (isUpdating)
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(EditorGUIUtility.FindTexture("WaitSpin00")),GUIStyle.none);
				}
			}

			if (canUnstage || canStage)
			{
				EditorGUI.BeginChangeCheck();
				EditorGUIUtility.AddCursorRect(stageToggleRect,MouseCursor.Link);
				EditorGUI.Toggle(stageToggleRect,canUnstage, styles.toggle);
				if (EditorGUI.EndChangeCheck())
				{
					bool updateFlag = false;
					string[] paths = null;
					if (GitManager.CanStage(info.State))
					{
						paths = GitManager.GetPathWithMeta(info.Path).ToArray();
						GitManager.Repository.Stage(paths);
						updateFlag = true;
					}
					else if (GitManager.CanUnstage(info.State))
					{
						paths = GitManager.GetPathWithMeta(info.Path).ToArray();
						GitManager.Repository.Unstage(paths);
						updateFlag = true;
					}

					if (updateFlag)
					{
						Repaint();
						current.Use();
						if (paths != null && paths.Length > 0) GitManager.MarkDirty(paths);
					}
				}
			}
			GitGUI.EndEnable();
		}

		private void DoFileDiffSelection(Rect elementRect,StatusListEntry info, int index,bool isUpdating)
		{
			Event current = Event.current;

			if (elementRect.Contains(current.mousePosition) && !isUpdating)
			{
				if (current.type == EventType.ContextClick)
				{
					if (GitManager.Settings.UseSimpleContextMenus)
					{
						GenericMenuWrapper genericMenuWrapper = new GenericMenuWrapper(new GenericMenu());
						DoDiffElementContex(genericMenuWrapper);
						genericMenuWrapper.GenericMenu.ShowAsContext();
					}
					else
					{
						ContextGenericMenuPopup popup = new ContextGenericMenuPopup();
						DoDiffElementContex(popup);
						PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), popup);
					}
					current.Use();
				}
				else if (current.type == EventType.MouseDown)
				{
					if (current.button == 0)
					{
						if (current.modifiers == EventModifiers.Control)
						{
							lastSelectedIndex = index;
							info.Selected = !info.Selected;
							GUI.FocusControl(info.Path);
						}
						else if (current.shift)
						{
							if (!current.control) statusList.SelectAll(false);

							int tmpIndex = 0;
							foreach (var selectInfo in statusList)
							{
								FileStatus mergedStatus = GetMergedStatus(selectInfo.State);
								bool isExpanded = settings.MinimizedFileStatus.IsFlagSet(mergedStatus);
								if (!isExpanded) continue;
								if (tmpIndex >= Mathf.Min(lastSelectedIndex, index) && tmpIndex <= Mathf.Max(lastSelectedIndex, index))
								{
									selectInfo.Selected = true;
								}
								tmpIndex++;
							}
							if (current.control) lastSelectedIndex = index;
							GUI.FocusControl(info.Path);
						}
						else
						{
							if (current.clickCount == 2)
							{
								Selection.activeObject = AssetDatabase.LoadAssetAtPath(info.Path, typeof (Object));
							}
							else
							{
								lastSelectedIndex = index;
								statusList.SelectAll(false);
								info.Selected = !info.Selected;
								GUI.FocusControl(info.Path);
							}
						}
						current.Use();
						Repaint();
					}
					else if (current.button == 1)
					{
						if (!info.Selected)
						{
							statusList.SelectAll(false);
							info.Selected = true;
							current.Use();
							Repaint();
						}
					}
				}
			}
		}

		private bool IsVisible(StatusListEntry entry)
		{
			return settings.MinimizedFileStatus.IsFlagSet(GetMergedStatus(entry.State)) && (entry.Name == null || string.IsNullOrEmpty(filter) || entry.Name.Contains(filter));
		}

		private void ReadCommitMessageFromFile()
		{
			if (File.Exists(CommitMessageFilePath))
			{
				using (var commiFileStream = File.Open(CommitMessageFilePath, FileMode.Open, FileAccess.Read))
				using (var reader = new StreamReader(commiFileStream))
				{
					settings.commitMessageFromFile = reader.ReadToEnd();
				}
			}
			else
			{
				Debug.LogWarning("Commit message file missing. Creating new file.");
				SaveCommitMessageToFile();
			}
		}

		private void ReadCommitMessage()
		{
			//load commit from previous versions and remove the key
			if (EditorPrefs.HasKey(CommitMessageKey))
			{
				settings.commitMessage = EditorPrefs.GetString(CommitMessageKey);
			}
			else
			{
				settings.commitMessage = "";
			}

			GUI.FocusControl("");
		}

		private void SaveCommitMessageToFile()
		{
			try
			{
				if (!Directory.Exists(Path.Combine(GitManager.GitFolderPath, "UniGit/Settings")))
				{
					Directory.CreateDirectory(Path.Combine(GitManager.GitFolderPath, "UniGit/Settings"));
				}

				File.WriteAllText(CommitMessageFilePath, settings.commitMessageFromFile);
			}
			catch (Exception e)
			{
#if UNITY_EDITOR
				Debug.LogError("Could not save commit message to file. Saving to EditorPrefs");
				Debug.LogException(e);
#endif
			}
		}

		private void SaveCommitMessage()
		{
			EditorPrefs.SetString(CommitMessageKey, settings.commitMessage);
		}

		public void SetCommitMessage(string commitMessage)
		{
			if (settings.readFromFile)
			{
				settings.commitMessageFromFile = commitMessage;
				SaveCommitMessageToFile();
			}
			settings.commitMessage = commitMessage;
			SaveCommitMessage();
		}

		private string CommitMessageFilePath
		{
			get { return Path.Combine(GitManager.GitFolderPath, "UniGit/Settings/CommitMessage.txt"); }
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			if (!settings.readFromFile)
			{
				SaveCommitMessage();
			}
		}

		#region Menu Callbacks

		private void DoDiffStatusContex(FileStatus fileStatus, GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Select All"), false, SelectFilteredCallback, fileStatus);
			if (GitManager.CanStage(fileStatus))
			{
				menu.AddItem(new GUIContent("Add All"), false, () =>
				{
					string[] paths = statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => GitManager.GetPathWithMeta(s.Path)).ToArray();
					GitManager.Repository.Stage(paths);
					GitManager.MarkDirty(paths);
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
					string[] paths = statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => GitManager.GetPathWithMeta(s.Path)).ToArray();
					GitManager.Repository.Unstage(paths);
					GitManager.MarkDirty(paths);
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Remove All"));
			}
		}

		private void DoDiffElementContex(IGenericMenu editMenu)
		{
			StatusListEntry[] entries = statusList.Where(e => e.Selected).ToArray();
			FileStatus selectedFlags = entries.Select(e => e.State).CombineFlags();

			GUIContent addContent = new GUIContent("Stage", EditorGUIUtility.FindTexture("CollabPush"),"Stage File and meta to index.");
			if (GitManager.CanStage(selectedFlags))
			{
				editMenu.AddItem(addContent, false, AddSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(addContent);
			}
			GUIContent removeContent = new GUIContent("Unstage", EditorGUIUtility.FindTexture("CollabPull"),"Remove file and meta from index");
			if (GitManager.CanUnstage(selectedFlags))
			{
				editMenu.AddItem(removeContent, false, RemoveSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(removeContent);
			}
			
			editMenu.AddSeparator("");
			Texture2D diffIcon = EditorGUIUtility.FindTexture("ViewToolZoom");
			if (entries.Length == 1)
			{
				string path = entries[0].Path;
				if (selectedFlags.IsFlagSet(FileStatus.Conflicted))
				{
					if (GitConflictsHandler.CanResolveConflictsWithTool(path))
					{
						editMenu.AddItem(new GUIContent("Resolve Conflicts","Resolve merge conflicts"), false, ResolveConflictsCallback, path);
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Resolve Conflicts"));
					}
					editMenu.AddItem(new GUIContent("Resolve (Using Ours)","Resolve using our own version of the file or meta"), false, ResolveConflictsOursCallback, entries[0].Path);
					editMenu.AddItem(new GUIContent("Resolve (Using Theirs)","Resolve by using their version of the file or meta"), false, ResolveConflictsTheirsCallback, entries[0].Path);
				}
				else
				{
					if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
					{
						editMenu.AddItem(new GUIContent("Difference/Asset", diffIcon, "Show changes between the working tree and the index or a tree"), false, () => { SeeDifferenceObject(entries[0]); });
						editMenu.AddItem(new GUIContent("Difference/Meta", diffIcon, "Show changes between the working tree and the index or a tree"), false, () => { SeeDifferenceMeta(entries[0]); });
					}
					else
					{
						editMenu.AddItem(new GUIContent("Difference", diffIcon, "Show changes between the working tree and the index or a tree"), false, () => { SeeDifferenceAuto(entries[0]); });
					}

					if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
					{
						editMenu.AddItem(new GUIContent("Difference with previous version/Asset", diffIcon, "Show changes between the working tree and the previous commit"), false, () => { SeeDifferencePrevObject(entries[0]); });
						editMenu.AddItem(new GUIContent("Difference with previous version/Meta", diffIcon, "Show changes between the working tree and the previous commit"), false, () => { SeeDifferencePrevMeta(entries[0]); });
					}
					else
					{
						editMenu.AddItem(new GUIContent("Difference with previous version", diffIcon, "Show changes between the working tree and the previous commit"), false, () => { SeeDifferencePrevAuto(entries[0]); });
					}
					
				}
			}
			editMenu.AddSeparator("");
			editMenu.AddItem(new GUIContent("Revert", EditorGUIUtility.FindTexture("UnityEditor.AnimationWindow"),"Revert any changes made to the file or meta"), false, RevertSelectedCallback);
			editMenu.AddSeparator("");
			if (GitManager.CanBlame(entries[0].State))
			{
				editMenu.AddItem(new GUIContent("Blame", EditorGUIUtility.FindTexture("UnityEditor.GameView"), "Annotates each line in the given file with information from the revision which last modified the line. "), false, BlameSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(new GUIContent("Blame", EditorGUIUtility.FindTexture("UnityEditor.GameView")));
			}
			editMenu.AddSeparator("");
			if (entries.Length == 1)
			{
				editMenu.AddItem(new GUIContent("Show In Explorer", EditorGUIUtility.FindTexture("Folder Icon"),"Show file in the native Explorer"), false, () => { EditorUtility.RevealInFinder(entries[0].Path); });
			}
			editMenu.AddItem(new GUIContent("Reload", EditorGUIUtility.FindTexture("RotateTool"),"Force a reload of the repository"), false, ReloadCallback);
		}

		private void SelectFilteredCallback(object filter)
		{
			FileStatus state = (FileStatus)filter;
			foreach (var entry in statusList)
			{
				entry.Selected = entry.State == state;
			}
		}

		private void ReloadCallback()
		{
			GitManager.MarkDirty(true);
		}

		private void ResolveConflictsTheirsCallback(object path)
		{
			GitConflictsHandler.ResolveConflicts((string)path,MergeFileFavor.Theirs);
		}

		private void ResolveConflictsOursCallback(object path)
		{
			GitConflictsHandler.ResolveConflicts((string)path, MergeFileFavor.Ours);
		}

		private void ResolveConflictsCallback(object path)
		{
			GitConflictsHandler.ResolveConflicts((string)path, MergeFileFavor.Normal);
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
			GitManager.ShowDiff(entry.Path);
		}

		private void SeeDifferenceMeta(StatusListEntry entry)
		{
			GitManager.ShowDiff(GitManager.MetaPathFromAsset(entry.Path));
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
			GitManager.ShowDiffPrev(entry.Path);
		}

		private void SeeDifferencePrevMeta(StatusListEntry entry)
		{
			GitManager.ShowDiffPrev(GitManager.MetaPathFromAsset(entry.Path));
		}

		private void RevertSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();

			if (GitExternalManager.TakeRevert(paths))
			{
				AssetDatabase.Refresh();
				GitManager.MarkDirty(paths);
				return;
			}

			GitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions() {CheckoutModifiers = CheckoutModifiers.Force,OnCheckoutProgress = OnRevertProgress });
			EditorUtility.ClearProgressBar();
		}

		private void BlameSelectedCallback()
		{
			string firstPath = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).FirstOrDefault();
			GitManager.ShowBlameWizard(firstPath);
		}

		private void OnRevertProgress(string path,int currentSteps,int totalSteps)
		{
			float percent = (float) currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File",string.Format("Reverting file {0} {1}%",path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				EditorUtility.ClearProgressBar();
				GitManager.MarkDirty();
				GetWindow<GitDiffWindow>().ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		private void RemoveSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();
			GitManager.Repository.Unstage(paths);
			GitManager.MarkDirty(paths);
		}

		private void AddSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();
			GitManager.Repository.Stage(paths);
			GitManager.MarkDirty(paths);
		}
		#endregion

		#region Sorting

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

		private static FileStatus GetMergedStatus(FileStatus status)
		{
			if (status.IsFlagSet(FileStatus.NewInIndex | FileStatus.NewInWorkdir))
			{
				return FileStatus.NewInIndex | FileStatus.NewInWorkdir;
			}
			if (status.IsFlagSet(FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir))
			{
				return FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir;
			}
			if (status.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				return FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir;
			}
			if (status.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				return FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir;
			}
			return status;
		}
		#endregion

		#region Status List
		public class StatusList : IEnumerable<StatusListEntry>
		{
			private List<StatusListEntry> entires;
			private SortType sortType;
			private SortDir sortDir;

			public StatusList(IEnumerable<GitStatusEntry> enumerable, FileStatus filter,SortType sortType, SortDir sortDir)
			{
				entires = new List<StatusListEntry>();
				this.sortType = sortType;
				this.sortDir = sortDir;
				BuildList(enumerable, filter);
			}

			public void Update(IEnumerable<GitStatusEntry> enumerable,string[] paths, FileStatus fileStatus,SortType sortType,SortDir sortDir)
			{
				this.sortType = sortType;
				this.sortDir = sortDir;

				entires.RemoveAll(e => paths.Contains(e.Path.EndsWith(".meta") ? AssetDatabase.GetAssetPathFromTextMetaFilePath(e.Path) : e.Path));

				var entries = enumerable as GitStatusEntry[] ?? enumerable.ToArray();
				foreach (var path in paths)
				{
					var p = path;
					foreach (var entry in entries.Where(e => e.Path.StartsWith(p) && fileStatus.IsFlagSet(e.Status)))
					{
						Add(entry);
					}
				}

				entires.Sort(SortHandler);
			}

			private void BuildList(IEnumerable<GitStatusEntry> enumerable, FileStatus filter)
			{
				foreach (var entry in enumerable.Where(e => filter.IsFlagSet(e.Status)))
				{
					Add(entry);
				}

				entires.Sort(SortHandler);
			}

			private void Add(GitStatusEntry entry)
			{
				if (entry.Path.EndsWith(".meta"))
				{
					string mainAssetPath = GitManager.AssetPathFromMeta(entry.Path);
					if (!GitManager.Settings.ShowEmptyFolders && GitManager.IsEmptyFolder(mainAssetPath)) return;

					StatusListEntry ent = entires.FirstOrDefault(e => e.Path == mainAssetPath);
					if (ent != null)
					{
						ent.MetaChange |= MetaChangeEnum.Meta;
					}
					else
					{
						entires.Add(new StatusListEntry(mainAssetPath, entry.Status, MetaChangeEnum.Meta));
					}
				}
				else
				{
					StatusListEntry ent = entires.FirstOrDefault(e => e.Path == entry.Path);
					if (ent != null)
					{
						ent.State = entry.Status;
					}
					else
					{
						entires.Add(new StatusListEntry(entry.Path, entry.Status, MetaChangeEnum.Object));
					}
				}
			}

			private int SortHandler(StatusListEntry left, StatusListEntry right)
			{
				int stateCompare = GetPriority(left.State).CompareTo(GetPriority(right.State));
				if (stateCompare == 0)
				{
					string convertedDataPath = cachedDataPath.Replace("Assets", "").Replace('/', '\\');

					if (sortDir == SortDir.Descending)
					{
						var oldLeft = left;
						left = right;
						right = oldLeft;
					}

					switch (sortType)
					{
						case SortType.Name:
							stateCompare = string.Compare(left.Name, right.Name, StringComparison.InvariantCultureIgnoreCase);
							break;
						case SortType.Path:
							stateCompare = string.Compare(left.Path, right.Path, StringComparison.InvariantCultureIgnoreCase);
							break;
						case SortType.ModificationDate:
							DateTime modifedTimeLeft = GetClosest(GitManager.GetPathWithMeta(left.Path).Select(p => File.GetLastWriteTime(convertedDataPath + p)));
							DateTime modifedRightTime = GetClosest(GitManager.GetPathWithMeta(right.Path).Select(p => File.GetLastWriteTime(convertedDataPath + p)));
							stateCompare = DateTime.Compare(modifedRightTime,modifedTimeLeft);
							break;
						case SortType.CreationDate:
							DateTime createdTimeLeft = GetClosest(GitManager.GetPathWithMeta(left.Path).Select(p => File.GetCreationTime(convertedDataPath + p)));
							DateTime createdRightTime = GetClosest(GitManager.GetPathWithMeta(right.Path).Select(p => File.GetCreationTime(convertedDataPath + p)));
							stateCompare = DateTime.Compare(createdRightTime,createdTimeLeft);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
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

			public void SelectAll(Func<StatusListEntry, bool> predicate, bool select)
			{
				foreach (var entry in entires.Where(predicate))
				{
					entry.Selected = select;
				}
			}

			public void SelectAll(bool select)
			{
				foreach (var entry in entires)
				{
					entry.Selected = select;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public IEnumerator<StatusListEntry> GetEnumerator()
			{
				return entires.GetEnumerator();
			}
		}

		[Serializable]
		public class StatusListEntry
		{
			[SerializeField]
			private string path;
			[SerializeField]
			private string name;
			[SerializeField]
			private MetaChangeEnum metaChange;
			[SerializeField]
			private FileStatus state;
			[SerializeField]
			private bool selected;

			public StatusListEntry(string path, FileStatus state, MetaChangeEnum metaChange)
			{
				this.path = path;
				this.name = System.IO.Path.GetFileName(path);
				this.state = state;
				this.metaChange = metaChange;
			}

			public bool Selected
			{
				get { return selected; }
				set { selected = value; }
			}

			public string Path
			{
				get { return path; }
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
		}

		[Serializable]
		[Flags]
		public enum MetaChangeEnum
		{
			Object = 1 << 0,
			Meta = 1 << 1
		}

		[Serializable]
		public enum SortType
		{
			Name,
			Path,
			[Description("Modification Date")]
			ModificationDate,
			[Description("Creation Date")]
			CreationDate
		}

		[Serializable]
		public enum SortDir
		{
			Ascending,
			Descending
		}

		#endregion
		}
}