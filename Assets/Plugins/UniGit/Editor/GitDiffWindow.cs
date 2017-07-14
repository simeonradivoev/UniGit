using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using Utils.Extensions;
using Object = UnityEngine.Object;

namespace UniGit
{
	public class GitDiffWindow : GitUpdatableWindow, IHasCustomMenu
	{
		[MenuItem("Window/GIT Diff Window")]
		public static void CreateEditor()
		{
			GetWindow(true,GitManager.Instance);
		}

		public static GitDiffWindow GetWindow(bool focus,GitManager gitManager)
		{
			var window = GetWindow<GitDiffWindow>("Git Diff", focus);
			window.Construct(gitManager);
			return window;
		}

		public static GitDiffWindow CreateWindow(bool focus, GitManager gitManager)
		{
			var window = CreateInstance<GitDiffWindow>();
			window.Show();
			if (focus) window.Focus();
			window.Construct(gitManager);
			return window;
		}

		public Rect CommitRect { get { return new Rect(0,0,position.width, commitMaximized ? 48 + CalculateCommitTextHeight() : 46);} }
		public Rect DiffToolbarRect { get { return new Rect(0, CommitRect.height, position.width, 18); } }
		public Rect DiffRect { get { return new Rect(0,CommitRect.height + DiffToolbarRect.height, position.width,position.height - CommitRect.height - DiffToolbarRect.height);} }

		private const string CommitMessageKey = "UniGitCommitMessage";
		private const string CommitMessageUndoGroup = "Commit Message Change";
		private const string BuildngStatusOperationName = "BuildingStatus";

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
		private char commitMessageLastChar;
		private GitConflictsHandler conflictsHandler;

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
			[SerializeField] internal string commitMessage;
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

		public override void Construct(GitManager gitManager)
		{
			base.Construct(gitManager);
			conflictsHandler = new GitConflictsHandler(gitManager);
		}

		protected override void OnEnable()
		{
			titleContent.text = "Git Diff";
			base.OnEnable();
			editoSerializedObject = new SerializedObject(this);
			if (settings == null) settings = new Settings();
			if (Undo.GetCurrentGroupName() == CommitMessageUndoGroup)
			{
				Undo.RegisterFullObjectHierarchyUndo(this, "Commit Message Changed");
			}
		}

		protected override void OnGitUpdate(GitRepoStatus status,string[] paths)
		{
			GitAsyncManager.QueueWorker(CreateStatusListThreaded, status,BuildngStatusOperationName);
		}

		private void UpdateStatusList()
		{
			if(gitManager.Repository == null) return;
			GitAsyncManager.QueueWorker(CreateStatusListThreaded, gitManager.LastStatus, BuildngStatusOperationName);
		}

		protected override void OnInitialize()
		{
			ReadCommitMessage();
			ReadCommitMessageFromFile();
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			
		}

		protected override void OnEditorUpdate()
		{
			
		}

		private void CreateStatusListThreaded(GitRepoStatus param)
		{
			Monitor.Enter(statusListLock);
			try
			{
				GitRepoStatus status = param ?? new GitRepoStatus(gitManager.Repository.RetrieveStatus());
				statusList = new StatusList(status, settings.showFileStatusTypeFilter, settings.sortType, settings.sortDir,gitSettings,gitManager.RepoPath);
				gitManager.ActionQueue.Enqueue(Repaint);
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
			if(!gitManager.IsValidRepo) return;
			if(statusList != null) statusList.SelectAll(false);
		}

		[UsedImplicitly]
		protected override void OnFocus()
		{
			base.OnFocus();
			GUI.FocusControl(null);

			if (gitSettings != null && gitSettings.ReadFromFile)
			{
				if (File.Exists(gitManager.GitCommitMessageFilePath))
				{
					var lastWriteTime = File.GetLastWriteTime(gitManager.GitCommitMessageFilePath);
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
				styles.diffElementPath = new GUIStyle(EditorStyles.label) {onNormal = new GUIStyleState() { textColor = Color.white * 0.9f, background = Texture2D.blackTexture },wordWrap = true,fixedHeight = 0,alignment = TextAnchor.MiddleLeft};
				styles.diffElement = new GUIStyle("ProjectBrowserHeaderBgTop") {fixedHeight = 0,border = new RectOffset(8,8,8,8)};
				styles.toggle = new GUIStyle("IN Toggle") {normal = {background = (Texture2D)GitGUI.IconContentTex("toggle@2x") },onNormal = {background = (Texture2D)GitGUI.IconContentTex("toggle on@2x") },active = {background = (Texture2D)GitGUI.IconContentTex("toggle act@2x")}, onActive = { background = (Texture2D)GitGUI.IconContentTex("toggle on act@2x") }, fixedHeight = 0,fixedWidth = 0,border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset()};
				GitProfilerProxy.EndSample();
			}
		}

		[UsedImplicitly]
		private void OnGUI()
		{
			CreateStyles();

			if (gitManager == null || !gitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI(gitManager);
				return;
			}

			if (gitManager.Repository == null) return;
			RepositoryInformation repoInfo = gitManager.Repository.Info;
			GUILayout.BeginArea(CommitRect);
			DoCommit(repoInfo);
			GUILayout.EndArea();

			if (statusList == null)
			{
				Repaint();
				GitGUI.DrawLoading(position,new GUIContent("Loading Status List..."));
			}
			else
			{
				DoDiffScroll(Event.current);
			}
			

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
			commitMaximized = GUILayout.Toggle(commitMaximized, GitGUI.GetTempContent(gitSettings.ReadFromFile ? "File Commit Message: (Read Only)" : "Commit Message: "), "IN Foldout",GUILayout.Width(gitSettings.ReadFromFile ? 210 : 116));
			if (!commitMaximized)
			{
				if (!gitSettings.ReadFromFile)
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
				if (!gitSettings.ReadFromFile)
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
				BuildCommitMenu(commitMenu);
				commitMenu.ShowAsContext();
			}
			GitGUI.StartEnable(!gitSettings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Commit));
			settings.emptyCommit = GUILayout.Toggle(settings.emptyCommit, GitGUI.GetTempContent("Empty Commit", "Commit the message only without changes"));
			EditorGUI.BeginChangeCheck();
			settings.amendCommit = GUILayout.Toggle(settings.amendCommit, GitGUI.GetTempContent("Amend Commit", "Amend previous commit."));
			if (EditorGUI.EndChangeCheck())
			{
				if (settings.amendCommit)
				{
					AmmendCommit();
				}
			}
			settings.prettify = GUILayout.Toggle(settings.prettify, GitGUI.GetTempContent("Prettify", "Prettify the commit message"));
			GitGUI.EndEnable();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("_Help"),"IconButton"))
			{
				GoToHelp();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		private void BuildCommitMenu(GenericMenu commitMenu)
		{
			commitMenu.AddItem(new GUIContent("Commit"), false, CommitCallback);
			if (!gitSettings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Commit))
			{
				commitMenu.AddItem(new GUIContent("Commit And Push"), false, CommitAndPushCallback);
			}
			else
			{
				commitMenu.AddDisabledItem(new GUIContent("Commit And Push"));
			}
			commitMenu.AddSeparator("");
			commitMenu.AddItem(new GUIContent("Commit Message/Clear"), false, ClearCommitMessage);
			commitMenu.AddItem(new GUIContent("Commit Message/Read from file"), gitSettings.ReadFromFile, ToggleReadFromFile);
			if (File.Exists(gitManager.GitCommitMessageFilePath))
			{
				commitMenu.AddItem(new GUIContent("Commit Message/Open File"), false, OpenCommitMessageFile);
			}
			else
			{
				commitMenu.AddDisabledItem(new GUIContent("Commit Message/Open File"));
			}
			commitMenu.AddItem(new GUIContent("Commit Message/Reload"), false, ReadCommitMessage);
		}

		private void GoToHelp()
		{
			Application.OpenURL("https://github.com/simeonradivoev/UniGit/wiki/Committing");
		}

		private float CalculateCommitTextHeight()
		{
			string commitMessage = GetActiveCommitMessage(false);
			return Mathf.Clamp(GUI.skin.textArea.CalcHeight(GitGUI.GetTempContent(commitMessage), position.width) + EditorGUIUtility.singleLineHeight, 50, gitSettings.MaxCommitTextAreaSize);
		}

		public bool Commit()
		{
			Signature signature = gitManager.Signature;
			try
			{
				string commitMessage = GetActiveCommitMessage(true);
				if (!GitExternalManager.TakeCommit(commitMessage))
				{
					GitProfilerProxy.BeginSample("Git Commit");
					gitManager.Repository.Commit(commitMessage, signature, signature, new CommitOptions() { AllowEmptyCommit = settings.emptyCommit, AmendPreviousCommit = settings.amendCommit, PrettifyMessage = settings.prettify });
					GitProfilerProxy.EndSample();
					FocusWindowIfItsOpen<GitHistoryWindow>();
				}
				gitManager.MarkDirty();
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

		public string GetActiveCommitMessage()
		{
			return GetActiveCommitMessage(false);
		}

		public string GetActiveCommitMessage(bool forceUpdate)
		{
			if (gitSettings.ReadFromFile)
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
			if (gitSettings.ReadFromFile)
			{
				gitSettings.ReadFromFile = false;
				ReadCommitMessage();
			}
			else
			{
				gitSettings.ReadFromFile = true;
				ReadCommitMessageFromFile();
			}

			gitSettings.MarkDirty();
		}

		private void OpenCommitMessageFile()
		{
			if (File.Exists(gitManager.GitCommitMessageFilePath))
			{
				Application.OpenURL(gitManager.GitCommitMessageFilePath);
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
			if (gitSettings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Commit) || EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes and then push them?", "Commit and Push","Cancel"))
			{
				if (Commit())
				{
					var wizard = ScriptableWizard.DisplayWizard<GitPushWizard>("Push", "Push");
					wizard.Construct(gitManager);
				}
			}
		}

		public void AmmendCommit()
		{
			if (string.IsNullOrEmpty(settings.commitMessage))
			{
				settings.commitMessage = gitManager.Repository.Head.Tip.Message;
				SaveCommitMessage();
			}

			if (string.IsNullOrEmpty(settings.commitMessageFromFile))
			{
				settings.commitMessageFromFile = gitManager.Repository.Head.Tip.Message;
				SaveCommitMessageToFile();
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

			diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16,420), elementsTotalHeight);
			diffScroll = GUI.BeginScrollView(DiffRect, diffScroll, diffScrollContentRect);

			int index = 0;
			FileStatus? lastFileStatus = null;
			float infoX = 0;

			foreach (var info in statusList)
			{
				FileStatus mergedStatus = GetMergedStatus(info.State);
				bool isExpanded = IsVisible(info);
				bool isUpdating = gitManager.IsFileUpdating(info.Path);
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
			
			//check visibility
			if (rect.y > DiffRect.height + diffScroll.y || rect.y + rect.height < diffScroll.y)
			{
				return;
			}

			string filePath = info.Path;
			string fileName = info.Name;

			GitGUI.StartEnable(!isUpdating);
			Rect stageToggleRect = new Rect(rect.x + rect.width - 64, rect.y + 16, 32, 32);
			bool canUnstage = GitManager.CanUnstage(info.State);
			bool canStage = GitManager.CanStage(info.State);
			float maxPathSize = rect.width - stageToggleRect.width - 32 - 21;

			GUI.SetNextControlName(info.Path);
			GUI.Box(rect, GUIContent.none, info.Selected ? "TL LogicBar 1" : styles.diffElement);

			if (canStage && canUnstage)
			{
				maxPathSize -= stageToggleRect.width - 4;
				Rect stageWarnningRect = new Rect(stageToggleRect.x - stageToggleRect.width - 4, stageToggleRect.y, stageToggleRect.width, stageToggleRect.height);
				EditorGUIUtility.AddCursorRect(stageWarnningRect, MouseCursor.Link);
				if (GUI.Button(stageWarnningRect, GitGUI.IconContent("console.warnicon", "", "Upstaged changed pending. Stage to update index."), GUIStyle.none))
				{
					string[] paths = GitManager.GetPathWithMeta(info.Path).ToArray();
					gitManager.Repository.Stage(paths);
					gitManager.MarkDirty(paths);
					Repaint();
				}
			}

			if (current.type == EventType.Repaint)
			{
				Object asset = null;
				if (filePath.EndsWith(".meta"))
				{
					asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPathFromTextMetaFilePath(filePath), typeof (Object));
				}
				else
				{
					asset = AssetDatabase.LoadAssetAtPath(filePath, typeof (Object));
				}

				string extension = Path.GetExtension(filePath);
				GUIContent tmpContent = GUIContent.none;
				if (string.IsNullOrEmpty(extension))
				{
					tmpContent = GitGUI.IconContent("Folder Icon", string.Empty, "Folder");
				}

				if (tmpContent.image == null)
				{
					if (asset != null)
					{
						tmpContent = GitGUI.GetTempContent(AssetPreview.GetMiniThumbnail(asset), string.Empty, asset.GetType().Name);
					}
					else
					{
						tmpContent = GitGUI.IconContent("DefaultAsset Icon", string.Empty, "Unknown Type");
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

				Vector2 pathSize = styles.diffElementPath.CalcSize(GitGUI.GetTempContent(filePath));
				pathSize.x = Mathf.Min(pathSize.x, maxPathSize - x);

				Rect pathRect = new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight, pathSize.x, EditorGUIUtility.singleLineHeight*2);

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
						gitManager.Repository.Stage(paths);
						updateFlag = true;
					}
					else if (GitManager.CanUnstage(info.State))
					{
						paths = GitManager.GetPathWithMeta(info.Path).ToArray();
						gitManager.Repository.Unstage(paths);
						updateFlag = true;
					}

					if (updateFlag)
					{
						Repaint();
						current.Use();
						if (paths.Length > 0) gitManager.MarkDirty(paths);
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
					if (gitSettings.UseSimpleContextMenus)
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
			if (File.Exists(gitManager.GitCommitMessageFilePath))
			{
				settings.commitMessageFromFile = File.ReadAllText(gitManager.GitCommitMessageFilePath);
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
			if (gitManager.Prefs.HasKey(CommitMessageKey))
			{
				settings.commitMessage = gitManager.Prefs.GetString(CommitMessageKey);
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
				string settingsFolder = gitManager.GitSettingsFolderPath;
				if (!Directory.Exists(settingsFolder))
				{
					Directory.CreateDirectory(settingsFolder);
				}

				File.WriteAllText(gitManager.GitCommitMessageFilePath, settings.commitMessageFromFile);
			}
			catch (Exception e)
			{
#if UNITY_EDITOR
				Debug.LogError("Could not save commit message to file. Saving to Prefs");
				Debug.LogException(e);
#endif
			}
		}

		private void SaveCommitMessage()
		{
			gitManager.Prefs.SetString(CommitMessageKey, settings.commitMessage);
		}

		public void SetCommitMessage(string commitMessage)
		{
			if (gitSettings.ReadFromFile)
			{
				settings.commitMessageFromFile = commitMessage;
				SaveCommitMessageToFile();
			}
			settings.commitMessage = commitMessage;
			SaveCommitMessage();
		}

		[UsedImplicitly]
		private void OnDisable()
		{
			if (gitSettings != null && !gitSettings.ReadFromFile)
			{
				SaveCommitMessage();
			}
		}

		#region IHasCustomMenu

		public void AddItemsToMenu(GenericMenu menu)
		{
			BuildCommitMenu(menu);
			menu.AddItem(new GUIContent("Help"),false, GoToHelp);
		}

		#endregion

		#region Menu Callbacks

		private void DoDiffStatusContex(FileStatus fileStatus, GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Select All"), false, SelectFilteredCallback, fileStatus);
			if (GitManager.CanStage(fileStatus))
			{
				menu.AddItem(new GUIContent("Add All"), false, () =>
				{
					string[] paths = statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => GitManager.GetPathWithMeta(s.Path)).ToArray();
					gitManager.Repository.Stage(paths);
					gitManager.MarkDirty(paths);
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
					gitManager.Repository.Unstage(paths);
					gitManager.MarkDirty(paths);
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

			GUIContent addContent = new GUIContent("Stage", EditorGUIUtility.FindTexture("CollabPush"));
			if (GitManager.CanStage(selectedFlags))
			{
				editMenu.AddItem(addContent, false, AddSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(addContent);
			}
			GUIContent removeContent = new GUIContent("Unstage", EditorGUIUtility.FindTexture("CollabPull"));
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
					if (conflictsHandler.CanResolveConflictsWithTool(path))
					{
						editMenu.AddItem(new GUIContent("Resolve Conflicts","Resolve merge conflicts"), false, ResolveConflictsCallback, path);
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Resolve Conflicts"));
					}
					editMenu.AddItem(new GUIContent("Resolve (Using Ours)"), false, ResolveConflictsOursCallback, entries[0].Path);
					editMenu.AddItem(new GUIContent("Resolve (Using Theirs)"), false, ResolveConflictsTheirsCallback, entries[0].Path);
				}
				else
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
			}
			editMenu.AddSeparator("");
			editMenu.AddItem(new GUIContent("Revert", EditorGUIUtility.FindTexture("UnityEditor.AnimationWindow")), false, RevertSelectedCallback);
			editMenu.AddSeparator("");
			if (entries.Length == 1)
			{
				if (entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					if (gitManager.CanBlame(entries[0].State))
					{
						editMenu.AddItem(new GUIContent("Blame/Object", EditorGUIUtility.FindTexture("UnityEditor.GameView")), false, ()=> {BlameObject(entries[0]);});
						editMenu.AddItem(new GUIContent("Blame/Meta", EditorGUIUtility.FindTexture("UnityEditor.GameView")), false, ()=> {BlameMeta(entries[0]);});
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Blame", EditorGUIUtility.FindTexture("UnityEditor.GameView")));
					}
				}
				else
				{
					if (gitManager.CanBlame(entries[0].State))
					{
						editMenu.AddItem(new GUIContent("Blame", EditorGUIUtility.FindTexture("UnityEditor.GameView")), false, ()=> {BlameAuto(entries[0]);});
					}
					else
					{
						editMenu.AddDisabledItem(new GUIContent("Blame", EditorGUIUtility.FindTexture("UnityEditor.GameView")));
					}
				}
			}
			editMenu.AddSeparator("");
			if (entries.Length == 1)
			{
				editMenu.AddItem(new GUIContent("Show In Explorer", EditorGUIUtility.FindTexture("Folder Icon")), false, () => { EditorUtility.RevealInFinder(entries[0].Path); });
			}
			editMenu.AddItem(new GUIContent("Reload", EditorGUIUtility.FindTexture("RotateTool")), false, ReloadCallback);
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
			gitManager.MarkDirty(true);
		}

		private void ResolveConflictsTheirsCallback(object path)
		{
			conflictsHandler.ResolveConflicts((string)path,MergeFileFavor.Theirs);
		}

		private void ResolveConflictsOursCallback(object path)
		{
			conflictsHandler.ResolveConflicts((string)path, MergeFileFavor.Ours);
		}

		private void ResolveConflictsCallback(object path)
		{
			conflictsHandler.ResolveConflicts((string)path, MergeFileFavor.Normal);
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
			gitManager.ShowDiff(entry.Path);
		}

		private void SeeDifferenceMeta(StatusListEntry entry)
		{
			gitManager.ShowDiff(GitManager.MetaPathFromAsset(entry.Path));
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
			gitManager.ShowDiffPrev(entry.Path);
		}

		private void SeeDifferencePrevMeta(StatusListEntry entry)
		{
			gitManager.ShowDiffPrev(GitManager.MetaPathFromAsset(entry.Path));
		}

		private void RevertSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();

			if (GitExternalManager.TakeRevert(paths))
			{
				gitManager.Callbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirty(paths);
				return;
			}

			gitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions() {CheckoutModifiers = CheckoutModifiers.Force,OnCheckoutProgress = OnRevertProgress });
			EditorUtility.ClearProgressBar();
		}

		private void BlameAuto(StatusListEntry entry)
		{
			if (entry.MetaChange.IsFlagSet(MetaChangeEnum.Object))
			{
				gitManager.ShowBlameWizard(entry.Path);
			}
			else
			{
				gitManager.ShowBlameWizard(AssetDatabase.GetTextMetaFilePathFromAssetPath(entry.Path));
			}
		}

		private void BlameMeta(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(AssetDatabase.GetTextMetaFilePathFromAssetPath(entry.Path));
		}

		private void BlameObject(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(entry.Path);
		}

		private void OnRevertProgress(string path,int currentSteps,int totalSteps)
		{
			float percent = (float) currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File",string.Format("Reverting file {0} {1}%",path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				EditorUtility.ClearProgressBar();
				gitManager.MarkDirty();
				GetWindow<GitDiffWindow>().ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		private void RemoveSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();
			gitManager.Repository.Unstage(paths);
			gitManager.MarkDirty(paths);
		}

		private void AddSelectedCallback()
		{
			string[] paths = statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)).ToArray();
			gitManager.Repository.Stage(paths);
			gitManager.MarkDirty(paths);
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

		#region Getters and Setters

		public Settings GitDiffSettings
		{
			get { return settings; }
		}

		#endregion

		#region Status List
		public class StatusList : IEnumerable<StatusListEntry>
		{
			private List<StatusListEntry> entires;
			private SortType sortType;
			private SortDir sortDir;
			private GitSettingsJson gitSettings;
			private string gitPath;

			public StatusList(IEnumerable<GitStatusEntry> enumerable, FileStatus filter,SortType sortType, SortDir sortDir, GitSettingsJson gitSettings,string gitPath)
			{
				this.gitPath = gitPath;
				this.gitSettings = gitSettings;
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
					if (!gitSettings.ShowEmptyFolders && GitManager.IsEmptyFolder(mainAssetPath)) return;

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
							DateTime modifedTimeLeft = GetClosest(GitManager.GetPathWithMeta(left.Path).Select(p => File.GetLastWriteTime(Path.Combine(gitPath, p))));
							DateTime modifedRightTime = GetClosest(GitManager.GetPathWithMeta(right.Path).Select(p => File.GetLastWriteTime(Path.Combine(gitPath,p))));
							stateCompare = DateTime.Compare(modifedRightTime,modifedTimeLeft);
							break;
						case SortType.CreationDate:
							DateTime createdTimeLeft = GetClosest(GitManager.GetPathWithMeta(left.Path).Select(p => File.GetCreationTime(Path.Combine(gitPath,p))));
							DateTime createdRightTime = GetClosest(GitManager.GetPathWithMeta(right.Path).Select(p => File.GetCreationTime(Path.Combine(gitPath,p))));
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