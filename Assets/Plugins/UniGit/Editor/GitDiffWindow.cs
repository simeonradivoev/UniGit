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
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	public class GitDiffWindow : GitUpdatableWindow, IHasCustomMenu, IComparer<GitDiffWindow.StatusListEntry>
	{
		private const string WindowName = "Git Diff";

		public Rect CommitRect { get { return new Rect(0,0,position.width, commitMaximized ? 48 + CalculateCommitTextHeight() : 46);} }
		public Rect DiffToolbarRect { get { return new Rect(0, CommitRect.height, position.width, 18); } }
		public Rect DiffRect { get { return new Rect(0,CommitRect.height + DiffToolbarRect.height, position.width,position.height - CommitRect.height - DiffToolbarRect.height);} }

		private const string CommitMessageKey = "UniGitCommitMessage";
		private const string CommitMessageUndoGroup = "Commit Message Change";

		[SerializeField] private Vector2 diffScroll;
		[SerializeField] private bool commitMaximized = true;
		[SerializeField] private string filter = "";
		[SerializeField] private Vector2 commitScroll;
		[SerializeField] private List<SelectionId> selections;
		[SerializeField] private Settings settings;
		[SerializeField] private StatusList statusList;

		private SerializedObject editoSerializedObject;
		private Rect commitsRect;
		private Styles styles;
		private int lastSelectedIndex;
		private readonly object statusListLock = new object();
		private char commitMessageLastChar;
		private GitConflictsHandler conflictsHandler;
		private GitAsyncOperation statusListUpdateOperation;
		private readonly HashSet<string> updatingPaths = new HashSet<string>();
		private readonly HashSet<string> pathsToBeUpdated = new HashSet<string>();
		private bool needsAsyncStatusListUpdate;
		private GitExternalManager externalManager;
		private GitLfsHelper lfsHelper;
		private GitAsyncManager asyncManager;
		private SearchField searchField;
		private GitOverlay gitOverlay;
		private InjectionHelper injectionHelper;

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
			public bool merge = true;
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
			public GUIStyle diffElementSelected;
			public GUIStyle toggle;
			public GUIStyle mergeIndicator;
			public GUIStyle commitMessageFoldoud;
			public GUIStyle commitButton;
		}

		private void CreateStyles()
		{
			if (styles == null)
			{
				GitProfilerProxy.BeginSample("Git Diff Window Style Creation", this);
				try
				{
					styles = new Styles()
					{ 
						commitTextArea = new GUIStyle("sv_iconselector_labelselection") { margin = new RectOffset(4, 4, 4, 4), normal = { textColor = Color.black }, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4) },
						assetIcon = new GUIStyle("NotificationBackground") { contentOffset = Vector2.zero, alignment = TextAnchor.MiddleCenter, imagePosition = ImagePosition.ImageOnly, padding = new RectOffset(4, 4, 4, 4), border = new RectOffset(12, 12, 12, 12) },
						diffScrollHeader = new GUIStyle("CurveEditorBackground") { contentOffset = new Vector2(48, 0), alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white * 0.9f }, padding = new RectOffset(12, 12, 12, 12), imagePosition = ImagePosition.ImageLeft },
						diffElementName = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, onNormal = new GUIStyleState() { textColor = Color.white * 0.95f, background = Texture2D.blackTexture } },
						diffElementPath = new GUIStyle(EditorStyles.label) { onNormal = new GUIStyleState() { textColor = Color.white * 0.9f, background = Texture2D.blackTexture }, wordWrap = true, fixedHeight = 0, alignment = TextAnchor.MiddleLeft },
						diffElement = new GUIStyle("ProjectBrowserHeaderBgTop") { fixedHeight = 0, border = new RectOffset(8, 8, 8, 8) },
						diffElementSelected = "TL LogicBar 1",
						toggle = new GUIStyle("IN Toggle") { normal = { background = (Texture2D)GitGUI.IconContentTex("toggle@2x") }, onNormal = { background = (Texture2D)GitGUI.IconContentTex("toggle on@2x") }, active = { background = (Texture2D)GitGUI.IconContentTex("toggle act@2x") }, onActive = { background = (Texture2D)GitGUI.IconContentTex("toggle on act@2x") }, fixedHeight = 0, fixedWidth = 0, border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset() },
						mergeIndicator = "AssetLabel",
						commitMessageFoldoud = "IN Foldout",
						commitButton = "DropDownButton"
					};
				}
				finally
				{
					GitProfilerProxy.EndSample();
				}
			}
		}

		[UniGitInject]
		private void Construct(GitExternalManager externalManager, 
			GitLfsHelper lfsHelper, 
			GitAsyncManager asyncManager,
			GitOverlay gitOverlay,
			InjectionHelper injectionHelper)
		{
			this.externalManager = externalManager;
			this.lfsHelper = lfsHelper;
			this.asyncManager = asyncManager;
			this.gitOverlay = gitOverlay;
			this.injectionHelper = injectionHelper;
			conflictsHandler = new GitConflictsHandler(gitManager, externalManager);
		}

		protected override void Subscribe(GitCallbacks callbacks)
		{
			base.Subscribe(callbacks);
			callbacks.AsyncStageOperationDone += OnAsyncStageOperationDone;
		}

		protected override void Unsubscribe(GitCallbacks callbacks)
		{
			base.Unsubscribe(callbacks);
			callbacks.AsyncStageOperationDone -= OnAsyncStageOperationDone;
		}

		protected override void OnEnable()
		{
			if(searchField == null) searchField = new SearchField();
			if(selections == null) selections = new List<SelectionId>();
			titleContent.text = WindowName;
			base.OnEnable();
			editoSerializedObject = new SerializedObject(this);
			if (settings == null) settings = new Settings();
			if (Undo.GetCurrentGroupName() == CommitMessageUndoGroup)
			{
				Undo.RegisterFullObjectHierarchyUndo(this, "Commit Message Changed");
			}
		}

		private void OnAsyncStageOperationDone(GitAsyncOperation operation)
		{
			Repaint();
		}

		protected override void OnGitUpdate(GitRepoStatus status,string[] paths)
		{
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.StatusListGui))
				CreateStatusListThreaded(status, paths);
			else
				CreateStatusList(status,paths);
		}

		private void UpdateStatusList()
		{
			if(gitManager.Repository == null || !IsInitialized) return;
			if (data.Initialized)
			{
				if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.StatusListGui))
					CreateStatusListThreaded(data.RepositoryStatus, null);
				else
					CreateStatusList(data.RepositoryStatus);
			}
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
			if (IsInitialized && needsAsyncStatusListUpdate && (statusListUpdateOperation == null || statusListUpdateOperation.IsDone))
			{
				if (data.Initialized)
				{
					needsAsyncStatusListUpdate = false;
					CreateStatusListThreaded(data.RepositoryStatus, null);
				}
			}
		}

		private void CreateStatusListThreaded(GitRepoStatus status,string[] paths)
		{
			if (statusListUpdateOperation != null && !statusListUpdateOperation.IsDone)
			{
				needsAsyncStatusListUpdate = true;
				if (paths != null)
				{
					foreach (var path in paths)
					{
						pathsToBeUpdated.Add(path);
					}
				}
			}
			else
			{
				statusListUpdateOperation = asyncManager.QueueWorkerWithLock(() =>
				{
					CreateStatusListInternal(status,paths);
				}, 
					(o) =>
				{
					updatingPaths.Clear();
					Repaint();
				}, statusListLock);

				if (paths != null)
				{
					foreach (var path in paths)
					{
						updatingPaths.Add(path);
					}
				}

				foreach (var path in pathsToBeUpdated)
				{
					updatingPaths.Add(path);
				}
				pathsToBeUpdated.Clear();
			}
		}

		private void CreateStatusList(GitRepoStatus param,string[] paths)
		{
			CreateStatusListInternal(param,paths);
			Repaint();
		}

		private void CreateStatusList(GitRepoStatus param)
		{
			CreateStatusListInternal(param,null);
			Repaint();
		}

		private void CreateStatusListInternal(GitRepoStatus status,string[] paths)
		{
			if (status == null)
			{
				logger.Log(LogType.Assert,"Trying to create status list from empty status");
				return;
			}
			try
			{
				if(statusList == null)statusList = new StatusList();
				statusList.Setup(gitSettings);

				if (paths == null || paths.Length <= 0)
				{
					statusList.Lock();

					try
					{
						statusList.Clear();
						status.Lock();
						foreach (var entry in status.Where(e => settings.showFileStatusTypeFilter.IsFlagSet(e.Status)))
						{
							statusList.Add(entry,null);
						}
						statusList.Sort(this);
						status.Unlock();
					}
					finally
					{
						statusList.Unlock();
						status.Unlock();
					}
				}
				else
				{
					statusList.Lock();

					try
					{
						statusList.RemoveRange(paths);
						foreach (var path in paths)
						{
							GitStatusEntry entry;
							if (status.Get(path,out entry) && settings.showFileStatusTypeFilter.IsFlagSet(entry.Status))
							{
								statusList.Add(entry,this);
							}
						}
					}
					finally
					{
						statusList.Unlock();
					}
				}
				
				//statusList.Sort();
			}
			catch (Exception e)
			{
				logger.LogException(e);
			}
		}

		[UsedImplicitly]
		protected override void OnFocus()
		{
			base.OnFocus();
			GUI.FocusControl(null);

			if (gitManager != null && gitSettings != null && gitSettings.ReadFromFile)
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

			DoDiffToolbar();

			if (statusList == null)
			{
				if(gitSettings.AnimationType.HasFlag(GitSettingsJson.AnimationTypeEnum.Loading)) Repaint();
				GitGUI.DrawLoading(new Rect(0, 0, position.width, position.height), GitGUI.GetTempContent(GetStatusBuildingState()));
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
				GUILayout.Label(GitGUI.GetTempContent("Merge"), styles.mergeIndicator);
			commitMaximized = GUILayout.Toggle(commitMaximized, GitGUI.GetTempContent(gitSettings.ReadFromFile ? "File Commit Message: (Read Only)" : "Commit Message: "), styles.commitMessageFoldoud, GUILayout.Width(gitSettings.ReadFromFile ? 210 : 116));
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
					GUILayout.Label(GitGUI.GetTempContent(settings.commitMessageFromFile), GUI.skin.textArea, GUILayout.Height(EditorGUIUtility.singleLineHeight));
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
					GUILayout.Label(GitGUI.GetTempContent(settings.commitMessageFromFile), GUI.skin.textArea,GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndScrollView();
			}

			EditorGUILayout.BeginHorizontal();
			
			if (GUILayout.Button(GitGUI.GetTempContent("Commit"), styles.commitButton))
			{
				GenericMenu commitMenu = new GenericMenu();
				BuildCommitMenu(commitMenu);
				commitMenu.ShowAsContext();
			}
			GitGUI.StartEnable(!gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit));
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
			if (GitGUI.LinkButtonLayout(gitOverlay.icons.donateSmall, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.Donate);
			}
			if (GitGUI.LinkButtonLayout(GitGUI.Contents.Help,GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.DiffWindowHelp);
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		private void BuildCommitMenu(GenericMenu commitMenu)
		{
			if(gitManager == null) return;
			commitMenu.AddItem(new GUIContent("Commit"), false, CommitCallback);
			if (!gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit))
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
				if (!externalManager.TakeCommit(commitMessage))
				{
					GitProfilerProxy.BeginSample("Git Commit");
					try
					{
						var commit = gitManager.Repository.Commit(commitMessage, signature, signature, new CommitOptions() {AllowEmptyCommit = settings.emptyCommit, AmendPreviousCommit = settings.amendCommit, PrettifyMessage = settings.prettify});
						FocusWindowIfItsOpen<GitHistoryWindow>();
						logger.LogFormat(LogType.Log,"Commit {0} Successful.",commit.Sha);
					}
					catch (Exception e)
					{
						logger.Log(LogType.Error,"There was a problem while trying to commit");
						logger.LogException(e);
						return false;
					}
					finally
					{
						GitProfilerProxy.EndSample();
					}
				}
				gitManager.MarkDirty();
				return true;
			}
			catch (Exception e)
			{
				logger.LogException(e);
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
			if (gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Commit) || EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to commit the changes and then push them?", "Commit and Push","Cancel"))
			{
				if (Commit())
				{
					UniGitLoader.DisplayWizard<GitPushWizard>("Git Push","Push");
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

		private void DoDiffToolbar()
		{
			GUILayout.BeginArea(DiffToolbarRect, GUIContent.none, EditorStyles.toolbar);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(GitGUI.GetTempContent("Edit"), EditorStyles.toolbarDropDown, GUILayout.MinWidth(64)))
			{
				GenericMenuWrapper editMenu = new GenericMenuWrapper(new GenericMenu());
				DoDiffElementContex(editMenu);
				editMenu.GenericMenu.ShowAsContext();
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Filter"), EditorStyles.toolbarDropDown, GUILayout.MinWidth(64)))
			{
				GenericMenu genericMenu = new GenericMenu();
				FileStatus[] fileStatuses = (FileStatus[])Enum.GetValues(typeof(FileStatus));
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
					genericMenu.AddItem(new GUIContent(flag.ToString()), settings.showFileStatusTypeFilter != (FileStatus)(-1) && settings.showFileStatusTypeFilter.IsFlagSet(flag), () =>
					{
						settings.showFileStatusTypeFilter = settings.showFileStatusTypeFilter.SetFlags(flag, !settings.showFileStatusTypeFilter.IsFlagSet(flag));
						UpdateStatusList();
					});
				}
				genericMenu.ShowAsContext();
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Sort"), EditorStyles.toolbarDropDown, GUILayout.MinWidth(64)))
			{
				GenericMenu genericMenu = new GenericMenu();
				foreach (SortType type in Enum.GetValues(typeof(SortType)))
				{
					SortType t = type;
					genericMenu.AddItem(new GUIContent(type.GetDescription()), type == settings.sortType, () =>
					{
						settings.sortType = t;
						UpdateStatusList();
					});
				}
				genericMenu.AddSeparator("");
				foreach (SortDir dir in Enum.GetValues(typeof(SortDir)))
				{
					SortDir d = dir;
					genericMenu.AddItem(new GUIContent(dir.GetDescription()), dir == settings.sortDir, () =>
					{
						settings.sortDir = d;
						UpdateStatusList();
					});
				}
				genericMenu.AddSeparator("");
				genericMenu.AddItem(new GUIContent("Group"), settings.merge, () =>
				{
					settings.merge = !settings.merge;
					UpdateStatusList();
				});
				genericMenu.ShowAsContext();
			}

			bool isUpdating = gitManager.IsUpdating;
			bool isStaging = gitManager.IsAsyncStaging;
			bool isDirty = gitManager.IsDirty;
			bool statusListUpdate = statusListUpdateOperation != null && !statusListUpdateOperation.IsDone;
			GUIContent statusContent = null;

			if (isUpdating)
			{
				statusContent = GitGUI.GetTempContent("Updating...",GitGUI.GetTempSpinAnimatedTexture());
			}
			else if (isStaging)
			{
				statusContent = GitGUI.GetTempContent("Staging...",GitGUI.GetTempSpinAnimatedTexture());
			}
			else if (isDirty)
			{
				string updateStatus = GetUpdateStatusMessage(gitManager.GetUpdateStatus());
				statusContent =  GitGUI.GetTempContent(updateStatus + "... ",GitGUI.GetTempSpinAnimatedTexture());
			}
			else if (statusListUpdate)
			{
				statusContent = GitGUI.GetTempContent(GetStatusBuildingState(),GitGUI.GetTempSpinAnimatedTexture());
			}

			if (statusContent != null)
			{
				GUILayout.Label(statusContent, EditorStyles.toolbarButton);
				if(gitSettings.AnimationType.HasFlag(GitSettingsJson.AnimationTypeEnum.Loading)) Repaint();
			}

			GUILayout.FlexibleSpace();
			filter = searchField.OnToolbarGUI(filter);
			EditorGUILayout.EndHorizontal();
			GUILayout.EndArea();
		}

		private void DoDiffScroll(Event current)
		{
			try
			{
				if (statusList.TryLock())
				{
					if (settings.merge)
					{
						float totalTypesCount = statusList.Select(i => GetMergedStatus(i.State)).Distinct().Count();
						float elementsTotalHeight = (statusList.Count(IsVisible) + totalTypesCount) * elementHeight;
						diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16, 420), elementsTotalHeight);
					}
					else
					{
						diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16, 420), statusList.Count(IsVisible) * elementHeight);
					}
				}
			}
			finally
			{
				statusList.Unlock();
			}

			diffScroll = GUI.BeginScrollView(DiffRect, diffScroll, diffScrollContentRect);

			int index = 0;
			FileStatus? lastFileStatus = null;
			float infoX = 0;

			for (int i = 0; i < statusList.Count; i++)
			{
				var info = statusList[i];
				bool isVisible = IsVisible(info);
				Rect elementRect;

				if (settings.merge)
				{
					FileStatus mergedStatus = GetMergedStatus(info.State);
					if (!lastFileStatus.HasValue || lastFileStatus != mergedStatus)
					{
						elementRect = new Rect(0, infoX, diffScrollContentRect.width + 16, elementHeight);
						lastFileStatus = mergedStatus;
						FileStatus newState = lastFileStatus.Value;
						if (current.type == EventType.Repaint)
						{
							styles.diffScrollHeader.Draw(elementRect, GitGUI.GetTempContent(mergedStatus.ToString()), false, false, false, false);
							GUIStyle.none.Draw(new Rect(elementRect.x + 12, elementRect.y + 14, elementRect.width - 12, elementRect.height - 24), GitGUI.GetTempContent(gitOverlay.GetDiffTypeIcon(info.State, false).image), false, false, false, false);
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
							else if (current.type == EventType.MouseDown && current.button == 0)
							{
								settings.MinimizedFileStatus = settings.MinimizedFileStatus.SetFlags(mergedStatus, !isVisible);
								if (!isVisible)
								{
									ClearSelected(e => e.State == newState);
								}

								Repaint();
								current.Use();
							}

						}

						infoX += elementRect.height;
					}
				}

				if (!isVisible) continue;
				elementRect = new Rect(0, infoX, diffScrollContentRect.width + 16, elementHeight);
				//check visibility
				if (elementRect.y <= DiffRect.height + diffScroll.y && elementRect.y + elementRect.height >= diffScroll.y)
				{
					bool isUpdating = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileUpdating(info.Path)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileUpdating(GitManager.MetaPathFromAsset(info.Path))) || updatingPaths.Contains(info.Path) || pathsToBeUpdated.Contains(info.Path);
					bool isStaging = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileStaging(info.Path)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileStaging(GitManager.MetaPathFromAsset(info.Path)));
					bool isDirty = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileDirty(info.Path)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileDirty(GitManager.MetaPathFromAsset(info.Path)));

					bool selected = IsSelected(info);
					bool enabled = !isUpdating && !isDirty && !isStaging;
					DoFileDiff(elementRect, info, enabled, selected);
					DoFileDiffSelection(elementRect, info, index, enabled, selected);
				}

				infoX += elementRect.height;
				index++;
			}

			GUI.EndScrollView();

			if (DiffRect.Contains(current.mousePosition))
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
						ContextGenericMenuPopup popup = injectionHelper.CreateInstance<ContextGenericMenuPopup>();
						DoDiffElementContex(popup);
						PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), popup);
					}

					current.Use();
				}

				if (current.type == EventType.MouseDrag && current.button == 2)
				{
					diffScroll.y -= current.delta.y;
					Repaint();
				}
			}
		}

		private void DoFileDiff(Rect rect,StatusListEntry info,bool enabled,bool selected)
		{
			Event current = Event.current;
			string filePath = info.Path;
			string fileName = info.Name;

			GitGUI.StartEnable(enabled);
			Rect stageToggleRect = new Rect(rect.x + rect.width - 64, rect.y + 16, 32, 32);
			bool canUnstage = GitManager.CanUnstage(info.State);
			bool canStage = GitManager.CanStage(info.State);
			float maxPathSize = rect.width - stageToggleRect.width - 32 - 21;

			if (current.type == EventType.Repaint)
			{
				(selected ? styles.diffElementSelected : styles.diffElement).Draw(rect,false,false,false,false);
			}

			if (canStage && canUnstage)
			{
				maxPathSize -= stageToggleRect.width - 4;
				Rect stageWarnningRect = new Rect(stageToggleRect.x - stageToggleRect.width - 4, stageToggleRect.y, stageToggleRect.width, stageToggleRect.height);
				EditorGUIUtility.AddCursorRect(stageWarnningRect, MouseCursor.Link);
				if (GUI.Button(stageWarnningRect, GitGUI.IconContent("console.warnicon", "", "Upstaged changed pending. Stage to update index."), GUIStyle.none))
				{
					string[] paths = gitManager.GetPathWithMeta(info.Path).ToArray();
					if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
					{
						gitManager.AsyncStage(paths).onComplete += (o) => { Repaint(); };
					}
					else
					{
						GitCommands.Stage(gitManager.Repository,paths);
						gitManager.MarkDirtyAuto(paths);
					}
					Repaint();
				}
			}

			if (current.type == EventType.Repaint)
			{
				Object asset = null;
				if(GitManager.IsPathInAssetFolder(filePath))
					asset = AssetDatabase.LoadAssetAtPath(GitManager.IsMetaPath(filePath) ? GitManager.AssetPathFromMeta(filePath) : filePath, typeof(Object));

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
						tmpContent = GitGUI.GetTempContent(string.Empty,AssetPreview.GetMiniThumbnail(asset), asset.GetType().Name);
					}
					else
					{
						tmpContent = GitGUI.IconContent("DefaultAsset Icon", string.Empty, "Unknown Type");
					}
				}

				float x = rect.x + elementSideMargin;
				GUI.Box(new Rect(x, rect.y + elementTopBottomMargin, iconSize, iconSize), tmpContent, styles.assetIcon);
				x += iconSize + 8;

				styles.diffElementName.Draw(new Rect(x, rect.y + elementTopBottomMargin + 2, rect.width - elementSideMargin - iconSize - rect.height, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(fileName), false, selected, selected, false);

				x = rect.x + elementSideMargin + iconSize + 8;
				foreach (var diffTypeIcon in gitOverlay.GetDiffTypeIcons(info.State,false))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), diffTypeIcon, GUIStyle.none);
					x += 25;
				}
				
				if (info.MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.objectIconSmall.image, "main asset file changed"), GUIStyle.none);
					x += 25;
				}
				if (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.metaIconSmall.image, ".meta file changed"), GUIStyle.none);
					x += 25;
				}
				if (lfsHelper.IsLfsPath(info.Path))
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempContent(gitOverlay.icons.lfsObjectIconSmall.image, "Lfs Object"), GUIStyle.none);
					x += 25;
				}

				Vector2 pathSize = styles.diffElementPath.CalcSize(GitGUI.GetTempContent(filePath));
				pathSize.x = Mathf.Min(pathSize.x, maxPathSize - x);

				Rect pathRect = new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight, pathSize.x, EditorGUIUtility.singleLineHeight*2);

				styles.diffElementPath.Draw(pathRect, GitGUI.GetTempContent(filePath),false, selected, selected, false);
				x += pathRect.width + 4;

				if (!enabled)
				{
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitGUI.GetTempSpinAnimatedTexture(),GUIStyle.none);
					//spinning animation needs constant repaint
					if(gitSettings.AnimationType.HasFlag(GitSettingsJson.AnimationTypeEnum.Loading)) Repaint();
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
					if (GitManager.CanStage(info.State))
					{
						string[] paths = gitManager.GetPathWithMeta(info.Path).ToArray();
						if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
						{
							gitManager.AsyncStage(paths).onComplete += (o)=>{ Repaint(); };
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
						string[] paths = gitManager.GetPathWithMeta(info.Path).ToArray();
						if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
						{
							gitManager.AsyncUnstage(paths).onComplete += (o) => { Repaint(); };
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
						Repaint();
						current.Use();
					}
				}
			}
			GitGUI.EndEnable();
		}

		private void DoFileDiffSelection(Rect elementRect,StatusListEntry info, int index,bool enabled,bool selected)
		{
			Event current = Event.current;

			if (elementRect.Contains(current.mousePosition) && enabled)
			{
				if (current.type == EventType.MouseDown)
				{
					if (current.button == 0)
					{
						if (current.modifiers == EventModifiers.Control)
						{
							lastSelectedIndex = index;
							if(selected)
								RemoveSelected(info);
							else
								AddSelected(info);
							GUI.FocusControl(info.Path);
						}
						else if (current.shift)
						{
							if (!current.control) ClearSelection();

							int tmpIndex = 0;
							foreach (var selectInfo in statusList)
							{
								FileStatus mergedStatus = GetMergedStatus(selectInfo.State);
								bool isExpanded = settings.MinimizedFileStatus.IsFlagSet(mergedStatus);
								if (!isExpanded) continue;
								if (tmpIndex >= Mathf.Min(lastSelectedIndex, index) && tmpIndex <= Mathf.Max(lastSelectedIndex, index))
								{
									AddSelected(selectInfo);
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
								ClearSelection();
								AddSelected(info);
								GUI.FocusControl(info.Path);
							}
						}
						current.Use();
						Repaint();
					}
					else if (current.button == 1)
					{
						if (!selected)
						{
							ClearSelection();
							AddSelected(info);
							current.Use();
							Repaint();
						}
					}
				}
			}
		}

		private bool IsVisible(StatusListEntry entry)
		{
			if (settings.merge)
			{
				if (!settings.MinimizedFileStatus.IsFlagSet(GetMergedStatus(entry.State))) return false;
			}
			return entry.Name == null || string.IsNullOrEmpty(filter) || entry.Name.Contains(filter);
		}

		private string GetUpdateStatusMessage(GitManager.UpdateStatusEnum status)
		{
			switch (status)
			{
				case GitManager.UpdateStatusEnum.InvalidRepo:
					return "Invalid Repository";
				case GitManager.UpdateStatusEnum.SwitchingToPlayMode:
					return "Switching to play mode";
				case GitManager.UpdateStatusEnum.Compiling:
					return "Compiling";
				case GitManager.UpdateStatusEnum.UpdatingAssetDatabase:
					return "Updating Asset Database";
				case GitManager.UpdateStatusEnum.Updating:
					return "Updating in progress";
				default:
					return "Waiting to update";
			}
		}

		private string GetStatusBuildingState()
		{
			return statusListUpdateOperation == null ? "Waiting on repository..." : "Building Status List...";
		}

		private void ReadCommitMessageFromFile()
		{
			if (File.Exists(gitManager.GitCommitMessageFilePath))
			{
				settings.commitMessageFromFile = File.ReadAllText(gitManager.GitCommitMessageFilePath);
			}
			else
			{
				logger.Log(LogType.Error,"Commit message file missing. Creating new file.");
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
				SaveCommitMessageToFile(gitManager, settings.commitMessageFromFile);
			}
			catch (Exception e)
			{
#if UNITY_EDITOR
				logger.Log(LogType.Error,"Could not save commit message to file. Saving to Prefs");
				logger.LogException(e);
#endif
			}
		}

	    private static void SaveCommitMessageToFile(GitManager gitManager,string message)
	    {
	        try
	        {
	            string settingsFolder = gitManager.GitSettingsFolderPath;
	            if (!Directory.Exists(settingsFolder))
	            {
	                Directory.CreateDirectory(settingsFolder);
	            }

	            File.WriteAllText(gitManager.GitCommitMessageFilePath, message);
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

	    internal static void SetCommitMessage(GitManager gitManager,GitSettingsJson gitSettings,string commitMessage)
	    {
	        if (gitSettings.ReadFromFile)
	        {
	            SaveCommitMessageToFile(gitManager, commitMessage);
	        }
            gitManager.Prefs.SetString(CommitMessageKey, commitMessage);
        }

		[UsedImplicitly]
		protected new void OnDisable()
		{
			base.OnDisable();
			if (gitManager != null && gitSettings != null && !gitSettings.ReadFromFile)
			{
				SaveCommitMessage();
			}
		}
		#region Selection

		private SelectionId CreateSelectionId(StatusListEntry entry)
		{
			string guid = GitManager.IsPathInAssetFolder(entry.Path) ? AssetDatabase.AssetPathToGUID(entry.Path) : entry.Path;
			return string.IsNullOrEmpty(guid) ? new SelectionId(entry.Path,true) : new SelectionId(guid,false);
		}

		private bool IsSelected(StatusListEntry entry)
		{
			int selectionsCount = selections.Count;
			for (int i = 0; i < selectionsCount; i++)
			{
				if (SelectionPredicate(selections[i], entry))
				{
					return true;
				}
			}
			return false;
		}

		private void AddSelected(StatusListEntry entry)
		{
			if(!IsSelected(entry))
				selections.Add(CreateSelectionId(entry));
		}

		private void RemoveSelected(StatusListEntry entry)
		{
			for (int i = selections.Count-1; i >= 0; i--)
			{
				var selection = selections[i];
				if (SelectionPredicate(selection, entry))
				{
					selections.RemoveAt(i);
					break;
				}
			}
		}

		private bool SelectionPredicate(SelectionId id, StatusListEntry entry)
		{
			if (id.isPath)
			{
				return entry.Path == id.id;
			}
			return entry.Guid == id.id;
		}

		public void ClearSelected(Func<StatusListEntry, bool> predicate)
		{
			foreach (var entry in statusList.Where(predicate))
			{
				RemoveSelected(entry);
			}
		}

		private void ClearSelection()
		{
			selections.Clear();
		}

		#endregion

		#region IHasCustomMenu

		public void AddItemsToMenu(GenericMenu menu)
		{
			BuildCommitMenu(menu);
			menu.AddItem(new GUIContent("Reload"),false, ReloadCallback);
			menu.AddItem(new GUIContent("Donate"),false, ()=>{GitLinks.GoTo(GitLinks.Donate);});
			menu.AddItem(new GUIContent("Help"),false, ()=>{GitLinks.GoTo(GitLinks.DiffWindowHelp);});
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
					string[] paths = statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => gitManager.GetPathWithMeta(s.Path)).ToArray();
					if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
					{
						gitManager.AsyncStage(paths).onComplete += (o) => { Repaint(); };
					}
					else
					{
					    GitCommands.Stage(gitManager.Repository,paths);
						gitManager.MarkDirtyAuto(paths);
					}
					Repaint();
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
					string[] paths = statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => gitManager.GetPathWithMeta(s.Path)).ToArray();
					if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
					{
						gitManager.AsyncUnstage(paths).onComplete += (o) => { Repaint(); };
					}
					else
					{
					    GitCommands.Unstage(gitManager.Repository,paths);
						gitManager.MarkDirtyAuto(paths);
					}
					Repaint();
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Remove All"));
			}
		}

		private void DoDiffElementContex(IGenericMenu editMenu)
		{
			StatusListEntry[] entries = statusList.Where(IsSelected).ToArray();
			FileStatus selectedFlags = entries.Select(e => e.State).CombineFlags();

			GUIContent addContent = new GUIContent("Stage", GitGUI.Textures.CollabPush);
			if (GitManager.CanStage(selectedFlags))
			{
				editMenu.AddItem(addContent, false, AddSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(addContent);
			}
			GUIContent removeContent = new GUIContent("Unstage", GitGUI.Textures.CollabPull);
			if (GitManager.CanUnstage(selectedFlags))
			{
				editMenu.AddItem(removeContent, false, RemoveSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(removeContent);
			}
			
			editMenu.AddSeparator("");
			Texture2D diffIcon = GitGUI.Textures.ZoomTool;
			if (entries.Length >= 1)
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
			else if(entries.Length >= 0)
			{
				if(entries[0].MetaChange == (MetaChangeEnum.Object | MetaChangeEnum.Meta))
				{
					editMenu.AddItem(new GUIContent("Revert/Asset", GitGUI.Textures.AnimationWindow), false, RevertSelectedObjects);
					editMenu.AddItem(new GUIContent("Revert/Meta", GitGUI.Textures.AnimationWindow), false, RevertSelectedMeta);
				}
				else
				{
					editMenu.AddItem(new GUIContent("Revert", GitGUI.Textures.AnimationWindow), false, RevertSelectedCallback);
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
				editMenu.AddItem(new GUIContent("Show In Explorer", GitGUI.Textures.FolderIcon), false, () => { EditorUtility.RevealInFinder(entries[0].Path); });
			}
			editMenu.AddItem(new GUIContent("Open", GitGUI.Textures.OrbitTool), false, () =>
			{
				AssetDatabase.OpenAsset(entries.Select(e => AssetDatabase.LoadAssetAtPath<Object>(e.Path)).Where(a => a != null).ToArray());
			});
			editMenu.AddItem(new GUIContent("Reload", GitGUI.Textures.RotateTool), false, ReloadCallback);
		}

		private void SelectFilteredCallback(object filter)
		{
			FileStatus state = (FileStatus)filter;
			foreach (var entry in statusList)
			{
				if(entry.State == state)
					AddSelected(entry);
				else
					RemoveSelected(entry);
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
			gitManager.ShowDiff(entry.Path,externalManager);
		}

		private void SeeDifferenceMeta(StatusListEntry entry)
		{
			gitManager.ShowDiff(GitManager.MetaPathFromAsset(entry.Path), externalManager);
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
			gitManager.ShowDiffPrev(entry.Path, externalManager);
		}

		private void SeeDifferencePrevMeta(StatusListEntry entry)
		{
			gitManager.ShowDiffPrev(GitManager.MetaPathFromAsset(entry.Path), externalManager);
		}

		private void RevertSelectedCallback()
		{
			string[] paths = statusList.Where(IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.Path)).ToArray();
			Revert(paths);
		}

		private void RevertSelectedMeta()
		{
			string[] metaPaths = statusList.Where(IsSelected).Select(e => GitManager.MetaPathFromAsset(e.Path)).ToArray();
			Revert(metaPaths);
		}

		private void RevertSelectedObjects()
		{
			string[] paths = statusList.Where(IsSelected).Select(e => e.Path).SkipWhile(gitManager.IsDirectory).ToArray();
			Revert(paths);
		}

		private void Revert(string[] paths)
		{
			if (externalManager.TakeRevert(paths))
			{
				gitCallbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirtyAuto(paths);
				return;
			}

			try
			{
				gitManager.Repository.CheckoutPaths("HEAD", paths, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force, OnCheckoutProgress = OnRevertProgress });
				gitManager.MarkDirtyAuto(paths);
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
			if (entry.MetaChange.IsFlagSet(MetaChangeEnum.Object))
			{
				gitManager.ShowBlameWizard(entry.Path, externalManager);
			}
			else
			{
				gitManager.ShowBlameWizard(AssetDatabase.GetTextMetaFilePathFromAssetPath(entry.Path), externalManager);
			}
		}

		private void BlameMeta(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(AssetDatabase.GetTextMetaFilePathFromAssetPath(entry.Path), externalManager);
		}

		private void BlameObject(StatusListEntry entry)
		{
			gitManager.ShowBlameWizard(entry.Path, externalManager);
		}

		private void OnRevertProgress(string path,int currentSteps,int totalSteps)
		{
			float percent = (float) currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File",string.Format("Reverting file {0} {1}%",path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private void RemoveSelectedCallback()
		{
			string[] paths = statusList.Where(IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.Path)).ToArray();
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage))
			{
				gitManager.AsyncUnstage(paths).onComplete += (o) => { Repaint(); };
			}
			else
			{
			    GitCommands.Unstage(gitManager.Repository,paths);
				gitManager.MarkDirtyAuto(paths);
			}
			Repaint();
		}

		private void AddSelectedCallback()
		{
			string[] paths = statusList.Where(IsSelected).SelectMany(e => gitManager.GetPathWithMeta(e.Path)).ToArray();
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage))
			{
				gitManager.AsyncStage(paths).onComplete += (o) => { Repaint(); };
			}
			else
			{
			    GitCommands.Stage(gitManager.Repository,paths);
				gitManager.MarkDirtyAuto(paths);
			}
			Repaint();
		}
		#endregion

		#region Sorting

		public int Compare(StatusListEntry x, StatusListEntry y)
		{
			int stateCompare = settings.merge ? GetPriority(x.State).CompareTo(GetPriority(y.State)) : 0;
			if (stateCompare == 0)
			{
				if (settings.sortDir == SortDir.Descending)
				{
					var oldLeft = x;
					x = y;
					y = oldLeft;
				}

				switch (settings.sortType)
				{
					case SortType.Name:
						stateCompare = string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
						break;
					case SortType.Path:
						stateCompare = string.Compare(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase);
						break;
					case SortType.ModificationDate:
						DateTime modifedTimeLeft = GetClosest(gitManager.GetPathWithMeta(x.Path).Select(p => File.GetLastWriteTime(UniGitPath.Combine(gitManager.RepoPath, p))));
						DateTime modifedRightTime = GetClosest(gitManager.GetPathWithMeta(x.Path).Select(p => File.GetLastWriteTime(UniGitPath.Combine(gitManager.RepoPath,p))));
						stateCompare = DateTime.Compare(modifedRightTime,modifedTimeLeft);
						break;
					case SortType.CreationDate:
						DateTime createdTimeLeft = GetClosest(gitManager.GetPathWithMeta(x.Path).Select(p => File.GetCreationTime(UniGitPath.Combine(gitManager.RepoPath,p))));
						DateTime createdRightTime = GetClosest(gitManager.GetPathWithMeta(y.Path).Select(p => File.GetCreationTime(UniGitPath.Combine(gitManager.RepoPath,p))));
						stateCompare = DateTime.Compare(createdRightTime,createdTimeLeft);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			if (stateCompare == 0)
			{
				stateCompare = String.Compare(x.Path, y.Path, StringComparison.Ordinal);
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

		[Serializable]
		public class StatusList : IEnumerable<StatusListEntry>
		{
			[SerializeField] private List<StatusListEntry> entires;
			private GitSettingsJson gitSettings;
			private readonly object lockObj;

			public StatusList()
			{
				entires = new List<StatusListEntry>();
				lockObj = new object();
			}

			internal void Setup(GitSettingsJson gitSettings)
			{
				this.gitSettings = gitSettings;
			}

			internal void Add(GitStatusEntry entry,IComparer<StatusListEntry> sorter)
			{
				StatusListEntry statusEntry;

				if (GitManager.IsMetaPath(entry.Path))
				{
					string mainAssetPath = GitManager.AssetPathFromMeta(entry.Path);
					if (!gitSettings.ShowEmptyFolders && GitManager.IsEmptyFolder(mainAssetPath)) return;

					StatusListEntry ent = entires.FirstOrDefault(e => e.Path == mainAssetPath);
					if (ent != null)
					{
						ent.MetaChange |= MetaChangeEnum.Meta;
						ent.State |= entry.Status;
						return;
					}

					statusEntry = new StatusListEntry(mainAssetPath, entry.Status, MetaChangeEnum.Meta);
				}
				else
				{
					StatusListEntry ent = entires.FirstOrDefault(e => e.Path == entry.Path);
					if (ent != null)
					{
						ent.State |= entry.Status;
						return;
					}

					statusEntry = new StatusListEntry(entry.Path, entry.Status, MetaChangeEnum.Object);
				}

				if(sorter != null) AddSorted(statusEntry,sorter);
				else entires.Add(statusEntry);
			}

			private void AddSorted(StatusListEntry entry,IComparer<StatusListEntry> sorter)
			{
				for (int i = 0; i < entires.Count; i++)
				{
					int compare = sorter.Compare(entires[i], entry);
					if (compare > 0)
					{
						entires.Insert(i,entry);
						return;
					}
				}

				entires.Add(entry);
			}

			public void Sort(IComparer<StatusListEntry> sorter)
			{
				entires.Sort(sorter);
			}

			public void RemoveRange(string[] paths)
			{
				foreach (var path in paths)
				{
					if (GitManager.IsMetaPath(path))
					{
						var assetPath = GitManager.AssetPathFromMeta(path);
						for (int i = entires.Count-1; i >= 0; i--)
						{
							var entry = entires[i];
							if (entry.Path == assetPath)
							{
								if (entry.MetaChange.HasFlag(MetaChangeEnum.Object))
									entry.MetaChange = entry.MetaChange.ClearFlags(MetaChangeEnum.Meta);
								else
									entires.RemoveAt(i);
							}
						}
					}
					else
					{
						for (int i = entires.Count-1; i >= 0; i--)
						{
							var entry = entires[i];
							if (entry.Path == path)
							{
								if (entry.MetaChange.HasFlag(MetaChangeEnum.Meta))
									entry.MetaChange = entry.MetaChange.ClearFlags(MetaChangeEnum.Object);
								else
									entires.RemoveAt(i);
							}
						}
					}
				}
			}

			public StatusListEntry this[int index]
			{
				get { return entires[index]; }
			}

			public void Clear()
			{
				entires.Clear();
			}

			public bool TryLock()
			{
				return Monitor.TryEnter(lockObj);
			}

			public void Lock()
			{
				Monitor.Enter(lockObj);
			}

			public void Unlock()
			{
				Monitor.Exit(lockObj);
			}

			public bool IsLocked()
			{
				return Monitor.Wait(lockObj);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public IEnumerator<StatusListEntry> GetEnumerator()
			{
				return entires.GetEnumerator();
			}

			public int Count
			{
				get { return entires.Count; }
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
			private string guid;

			public StatusListEntry(string path, FileStatus state, MetaChangeEnum metaChange)
			{
				this.path = path;
				this.name = System.IO.Path.GetFileName(path);
				this.state = state;
				this.metaChange = metaChange;
			}

			public string Guid
			{
				get { return guid ?? (guid = GitManager.IsPathInAssetFolder(path) ? AssetDatabase.AssetPathToGUID(path) : path); }
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
		private struct SelectionId
		{
			public bool isPath;
			public string id;

			public SelectionId(string id,bool isPath) : this()
			{
				this.id = id;
				this.isPath = isPath;
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