using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UniGit.Windows.Diff;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	public class GitDiffWindow : GitUpdatableWindow, IHasCustomMenu
	{
		private const string WindowName = "Git Diff";

		public Rect CommitRect { get { return new Rect(0,0,position.width, commitMaximized ? 48 + CalculateCommitTextHeight() : 46);} }
		public Rect DiffToolbarRect { get { return new Rect(0, CommitRect.height, position.width, 18); } }
		public Rect DiffRect { get { return new Rect(0,CommitRect.height + DiffToolbarRect.height, position.width,position.height - CommitRect.height - DiffToolbarRect.height);} }

		internal const string CommitMessageKey = "UniGitCommitMessage";
		internal const string CommitMessageUndoGroup = "Commit Message Change";

		[SerializeField] private Vector2 diffScroll;
		[SerializeField] private bool commitMaximized = true;
		[SerializeField] private string filter = "";
		[SerializeField] private Vector2 commitScroll;
		[SerializeField] private List<SelectionId> selections;
		[SerializeField] private Settings settings;
		[SerializeField] private DiffWindowStatusList diffWindowStatusList;

		private SerializedObject editoSerializedObject;
		private Rect commitsRect;
		private Styles styles;
		private int lastSelectedIndex;
		private readonly object statusListLock = new object();
		private GitAsyncOperation statusListUpdateOperation;
		private readonly HashSet<string> updatingPaths = new HashSet<string>();
		private readonly HashSet<string> pathsToBeUpdated = new HashSet<string>();
		private bool needsAsyncStatusListUpdate;
		private GitExternalManager externalManager;
		private GitAsyncManager asyncManager;
		private GitOverlay gitOverlay;
		private InjectionHelper injectionHelper;
		private GitDiffWindowToolbarRenderer toolbarRenderer;
		private GitDiffElementContextFactory elementContextFactory;
		private GitDiffWindowSorter sorter;
		private GitDiffWindowCommitRenderer gitDiffWindowCommitRenderer;
		private GitDiffWindowDiffElementRenderer diffElementRenderer;
		private Rect diffScrollContentRect;

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
			public bool unstagedChangesPriority = true;
			[SerializeField] internal string commitMessage;
			public string commitMessageFromFile;
			public DateTime lastMessageUpdate;
		}

		private class Styles
		{
			public GUIStyle diffScrollHeader;
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
						diffScrollHeader = new GUIStyle("CurveEditorBackground") { contentOffset = new Vector2(48, 0), alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white * 0.9f }, padding = new RectOffset(12, 12, 12, 12), imagePosition = ImagePosition.ImageLeft },
					};
					toolbarRenderer.LoadStyles();
					gitDiffWindowCommitRenderer.LoadStyles();
					diffElementRenderer.LoadStyles();
				}
				finally
				{
					GitProfilerProxy.EndSample();
				}
			}
		}

		[UniGitInject]
		private void Construct(GitExternalManager externalManager,
			GitAsyncManager asyncManager,
			GitOverlay gitOverlay,
			InjectionHelper injectionHelper,
			GitDiffWindowToolbarRenderer toolbarRenderer,
			GitDiffElementContextFactory elementContextFactory,
			GitDiffWindowCommitRenderer gitDiffWindowCommitRenderer,
			GitDiffWindowDiffElementRenderer diffElementRenderer)
		{
			this.externalManager = externalManager;
			this.asyncManager = asyncManager;
			this.gitOverlay = gitOverlay;
			this.injectionHelper = injectionHelper;
			this.toolbarRenderer = toolbarRenderer;
			this.elementContextFactory = elementContextFactory;
			this.gitDiffWindowCommitRenderer = gitDiffWindowCommitRenderer;
			this.diffElementRenderer = diffElementRenderer;
			sorter = new GitDiffWindowSorter(this,gitManager);
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

		internal void UpdateStatusList()
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
					CreateStatusListInternal(status,paths,true);
				}, 
					(o) =>
				{
					updatingPaths.Clear();
					Repaint();
				}, statusListLock,true);

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
			CreateStatusListInternal(param,paths,false);
			Repaint();
		}

		private void CreateStatusList(GitRepoStatus param)
		{
			CreateStatusListInternal(param,null,false);
			Repaint();
		}

		private void CreateStatusListInternal(GitRepoStatus status,string[] paths,bool threaded)
		{
			if (status == null)
			{
				logger.Log(LogType.Assert,"Trying to create status list from empty status");
				return;
			}
			try
			{
				var newStatusList = new DiffWindowStatusList(gitSettings,gitManager);

				if (paths == null || paths.Length <= 0)
				{
					lock (diffWindowStatusList.LockObj)
					{
						lock (status.LockObj)
						{
							foreach (var entry in status.Where(e => settings.showFileStatusTypeFilter.IsFlagSet(e.Status)))
							{
								newStatusList.Add(entry, null);
							}
							newStatusList.Sort(sorter);
							diffWindowStatusList = newStatusList;
						}
					}
				}
				else
				{
					lock (diffWindowStatusList.LockObj)
					{
						newStatusList.Copy(diffWindowStatusList);
						newStatusList.RemoveRange(paths);
						List<GitAsyncOperation> operations = new List<GitAsyncOperation>();
						foreach (var path in paths)
						{
							operations.Add(asyncManager.QueueWorkerWithLock((p) =>
							{
								GitStatusEntry entry;
								if (status.Get(p, out entry) && settings.showFileStatusTypeFilter.IsFlagSet(entry.Status))
								{
									newStatusList.Add(entry, sorter);
								}
							}, path, newStatusList.LockObj, threaded));
						}
						while (operations.Any(o => !o.IsDone))
						{
							//spin wait
						}
						diffWindowStatusList = newStatusList;
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
				if (File.Exists(initializer.GitCommitMessageFilePath))
				{
					var lastWriteTime = File.GetLastWriteTime(initializer.GitCommitMessageFilePath);
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

			if (gitManager == null || !initializer.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI(initializer);
				return;
			}

			if (gitManager.Repository == null) return;
			RepositoryInformation repoInfo = gitManager.Repository.Info;
			GUILayout.BeginArea(CommitRect);
			gitDiffWindowCommitRenderer.DoCommit(repoInfo,this,ref commitScroll);
			GUILayout.EndArea();

			toolbarRenderer.DoDiffToolbar(DiffToolbarRect,this,ref filter);

			if (diffWindowStatusList == null)
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

		internal float CalculateCommitTextHeight()
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

		internal void ClearCommitMessage()
		{
			settings.commitMessage = string.Empty;
			settings.commitMessageFromFile = string.Empty;
			SaveCommitMessageToFile();
			SaveCommitMessage();
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

		private void DoDiffScroll(Event current)
		{
			float elementHeight = diffElementRenderer.ElementHeight;

			if (IsGrouping())
				{
					float totalTypesCount = diffWindowStatusList.Select(i => GetMergedStatus(i.State)).Distinct().Count();
					float elementsTotalHeight = (diffWindowStatusList.Count(IsVisible) + totalTypesCount) * elementHeight;
					diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16, 420), elementsTotalHeight);
				}
				else
				{
					diffScrollContentRect = new Rect(0, 0, Mathf.Max(DiffRect.width - 16, 420), diffWindowStatusList.Count(IsVisible) * elementHeight);
				}

			diffScroll = GUI.BeginScrollView(DiffRect, diffScroll, diffScrollContentRect);

			int index = 0;
			FileStatus? lastFileStatus = null;
			float infoX = 0;

			for (int i = 0; i < diffWindowStatusList.Count; i++)
			{
				var info = diffWindowStatusList[i];
				bool isVisible = IsVisible(info);
				Rect elementRect;

				if (IsGrouping())
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
								elementContextFactory.Build(newState, selectAllMenu,this);
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
					bool isUpdating = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileUpdating(info.LocalPath)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileUpdating(GitManager.MetaPathFromAsset(info.LocalPath))) || updatingPaths.Contains(info.LocalPath) || pathsToBeUpdated.Contains(info.LocalPath);
					bool isStaging = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileStaging(info.LocalPath)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileStaging(GitManager.MetaPathFromAsset(info.LocalPath)));
					bool isDirty = (info.MetaChange.IsFlagSet(MetaChangeEnum.Object) && gitManager.IsFileDirty(info.LocalPath)) || (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta) && gitManager.IsFileDirty(GitManager.MetaPathFromAsset(info.LocalPath)));

					bool selected = IsSelected(info);
					bool enabled = !isUpdating && !isDirty && !isStaging;
					diffElementRenderer.DoFileDiff(elementRect, info, enabled, selected,this);
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
						elementContextFactory.Build(genericMenuWrapper,this);
						genericMenuWrapper.GenericMenu.ShowAsContext();
					}
					else
					{
						ContextGenericMenuPopup popup = injectionHelper.CreateInstance<ContextGenericMenuPopup>();
						elementContextFactory.Build(popup,this);
						PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), popup);
					}

					current.Use();
				}else if (current.type == EventType.KeyUp && current.keyCode == KeyCode.Delete)
				{
					foreach (var id in selections)
					{
						var entry = diffWindowStatusList.FirstOrDefault(e => SelectionPredicate(id, e));
						if (!string.IsNullOrEmpty(entry.LocalPath))
						{
							DeleteAsset(entry.LocalPath);
							current.Use();
						}
					}
				}

				if (current.type == EventType.MouseDrag && current.button == 2)
				{
					diffScroll.y -= current.delta.y;
					Repaint();
				}
			}
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
							GUI.FocusControl(info.LocalPath);
						}
						else if (current.shift)
						{
							if (!current.control) ClearSelection();

							int tmpIndex = 0;
							foreach (var selectInfo in diffWindowStatusList)
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
							GUI.FocusControl(info.LocalPath);
						}
						else
						{
							if (current.clickCount == 2)
							{
								Selection.activeObject = AssetDatabase.LoadAssetAtPath(gitManager.ToProjectPath(info.LocalPath), typeof (Object));
							}
							else
							{
								lastSelectedIndex = index;
								ClearSelection();
								AddSelected(info);
								GUI.FocusControl(info.LocalPath);
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
			if (IsGrouping())
			{
				if ((settings.MinimizedFileStatus & GetMergedStatus(entry.State)) == FileStatus.Unaltered) return false;
			}
			return entry.Name == null || string.IsNullOrEmpty(filter) || entry.Name.IndexOf(filter,StringComparison.InvariantCultureIgnoreCase) >= 0;
		}

		internal bool IsGrouping()
		{
			return settings.merge && string.IsNullOrEmpty(filter);
		}

		private static FileStatus GetMergedStatus(FileStatus status)
		{
			if ((status & (FileStatus.NewInIndex | FileStatus.NewInWorkdir)) != FileStatus.Unaltered)
			{
				return FileStatus.NewInIndex | FileStatus.NewInWorkdir;
			}
			if ((status & (FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir)) != FileStatus.Unaltered)
			{
				return FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir;
			}
			if ((status & (FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir)) != FileStatus.Unaltered)
			{
				return FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir;
			}
			if ((status & (FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir)) != FileStatus.Unaltered)
			{
				return FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir;
			}
			return status;
		}

		internal string GetStatusBuildingState()
		{
			return statusListUpdateOperation == null ? "Waiting on repository..." : "Building Status List...";
		}

		internal void ReadCommitMessageFromFile()
		{
			if (File.Exists(initializer.GitCommitMessageFilePath))
			{
				settings.commitMessageFromFile = File.ReadAllText(initializer.GitCommitMessageFilePath);
			}
			else
			{
				logger.Log(LogType.Error,"Commit message file missing. Creating new file.");
				SaveCommitMessageToFile();
			}
		}

		internal void ReadCommitMessage()
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
				SaveCommitMessageToFile(initializer, settings.commitMessageFromFile);
			}
			catch (Exception e)
			{
#if UNITY_EDITOR
				logger.Log(LogType.Error,"Could not save commit message to file. Saving to Prefs");
				logger.LogException(e);
#endif
			}
		}

	    private static void SaveCommitMessageToFile(GitInitializer initializer,string message)
	    {
	        try
	        {
	            string settingsFolder = initializer.GitSettingsFolderPath;
	            if (!Directory.Exists(settingsFolder))
	            {
	                Directory.CreateDirectory(settingsFolder);
	            }

	            File.WriteAllText(initializer.GitCommitMessageFilePath, message);
	        }
	        catch (Exception e)
	        {
#if UNITY_EDITOR
	            Debug.LogError("Could not save commit message to file. Saving to Prefs");
	            Debug.LogException(e);
#endif
	        }
	    }

        internal void SaveCommitMessage()
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

	    internal static void SetCommitMessage(GitInitializer initializer,GitManager gitManager,GitSettingsJson gitSettings,string commitMessage)
	    {
	        if (gitSettings.ReadFromFile)
	        {
	            SaveCommitMessageToFile(initializer, commitMessage);
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
			string projectPath = gitManager.ToProjectPath(entry.LocalPath);
			string guid = GitManager.IsPathInAssetFolder(projectPath) ? AssetDatabase.AssetPathToGUID(projectPath) : projectPath;
			return string.IsNullOrEmpty(guid) ? new SelectionId(projectPath,true) : new SelectionId(guid,false);
		}

		internal bool IsSelected(StatusListEntry entry)
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

		internal void AddSelected(StatusListEntry entry)
		{
			if(!IsSelected(entry))
				selections.Add(CreateSelectionId(entry));
		}

		internal void RemoveSelected(StatusListEntry entry)
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
				return gitManager.ToProjectPath(entry.LocalPath) == id.id;
			}
			return entry.GetGuid(gitManager) == id.id;
		}

		public void ClearSelected(Func<StatusListEntry, bool> predicate)
		{
			foreach (var entry in diffWindowStatusList.Where(predicate))
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
			menu.AddItem(new GUIContent("♺ Reload"),false, ReloadCallback);
			menu.AddItem(new GUIContent("💰 Donate"),false, ()=>{GitLinks.GoTo(GitLinks.Donate);});
			menu.AddItem(new GUIContent("⛐ Help"),false, ()=>{GitLinks.GoTo(GitLinks.DiffWindowHelp);});
		}

		#endregion

		#region Menu Callbacks

		internal void DeleteAsset(string localPath)
		{
			string projectPath = gitManager.ToProjectPath(localPath);
			if (GitManager.IsPathInAssetFolder(projectPath))
			{
				AssetDatabase.DeleteAsset(projectPath);
			}
			else
			{
				File.Delete(projectPath);
				gitManager.MarkDirty(localPath);
			}
		}

		internal void ReloadCallback()
		{
			gitManager.MarkDirty(true);
		}
		
		#endregion

		#region Getters and Setters

		public Settings GitDiffSettings
		{
			get { return settings; }
		}

		internal DiffWindowStatusList GetStatusList()
		{
			return diffWindowStatusList;
		}

		internal GitAsyncOperation GetStatusListUpdateOperation()
		{
			return statusListUpdateOperation;
		}

		public bool CommitMaximized
		{
			get { return commitMaximized; }
			set { commitMaximized = value; }
		}

		#endregion

		#region Status List

		

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