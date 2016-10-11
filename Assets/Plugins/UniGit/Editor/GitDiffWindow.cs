using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
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

		public Rect CommitRect { get { return new Rect(0,0,position.width,120);} }
		public Rect DiffToolbarRect { get { return new Rect(0, CommitRect.height, position.width, 18); } }
		public Rect DiffRect { get { return new Rect(0,CommitRect.height + DiffToolbarRect.height, position.width,position.height - CommitRect.height - DiffToolbarRect.height);} }
		
		private SerializedObject editoSerializedObject;
		[SerializeField] private Vector2 diffScroll;
		[SerializeField] public string commitMessage;
		private Rect commitsRect;
		private Styles styles;
		private Settings settings;
		private int lastSelectedIndex;
		[SerializeField] private StatusList statusList;

		[Serializable]
		public class Settings
		{
			public FileStatus showFileStatusTypeFilter;
			public FileStatus MinimizedFileStatus;
			public bool emptyCommit;
			public bool amendCommit;
			public bool prettify;
		}

		private class Styles
		{
			public GUIStyle commitTextArea;
			public GUIStyle assetIcon;
			public GUIStyle diffScrollHeader;
			public GUIStyle diffElementName;
			public GUIStyle diffElementPath;
			public GUIStyle diffElement;
		}

		protected override void OnGitUpdate(RepositoryStatus status)
		{
			statusList = new StatusList(status,settings.showFileStatusTypeFilter);
		}

		private void UpdateStatusList()
		{
			statusList = new StatusList(GitManager.Repository.RetrieveStatus(), settings.showFileStatusTypeFilter);
		}

		protected override void OnInitialize()
		{
			settings = new Settings();
			settings.showFileStatusTypeFilter = (FileStatus)(-1);
			statusList = new StatusList(GitManager.Repository.RetrieveStatus(), settings.showFileStatusTypeFilter);
			editoSerializedObject = new SerializedObject(this);
		}

		[UsedImplicitly]
		private void OnUnfocus()
		{
			if(!GitManager.IsValidRepo) return;
			statusList.SelectAll(false);
		}

		[UsedImplicitly]
		protected override void OnFocus()
		{
			base.OnFocus();
			GUI.FocusControl(null);
		}

		private void CreateStyles()
		{
			if (styles == null)
			{
				styles = new Styles();
				styles.commitTextArea = new GUIStyle("sv_iconselector_labelselection") {margin = new RectOffset(4, 4, 4, 4), normal = {textColor = Color.black}, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4)};
				styles.assetIcon = new GUIStyle("NotificationBackground") {contentOffset = Vector2.zero, alignment = TextAnchor.MiddleCenter,imagePosition = ImagePosition.ImageOnly,padding = new RectOffset(4,4,4,4),border = new RectOffset(12,12,12,12)};
				styles.diffScrollHeader = new GUIStyle("AnimationCurveEditorBackground") {contentOffset = new Vector2(48,0),alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold, normal = {textColor = Color.white * 0.9f},padding = new RectOffset(12,12,12,12),imagePosition = ImagePosition.ImageLeft};
				styles.diffElementName = new GUIStyle(EditorStyles.boldLabel) {fontSize = 12,onNormal = new GUIStyleState() {textColor = Color.white * 0.95f,background = Texture2D.blackTexture} };
				styles.diffElementPath = new GUIStyle(EditorStyles.label) {onNormal = new GUIStyleState() { textColor = Color.white * 0.9f, background = Texture2D.blackTexture } };
				styles.diffElement = new GUIStyle("ProjectBrowserHeaderBgTop") {fixedHeight = 0,border = new RectOffset(8,8,8,8)};
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

			RepositoryInformation repoInfo = GitManager.Repository.Info;
			GUILayout.BeginArea(CommitRect);
			DoCommit(repoInfo);
			GUILayout.EndArea();

			SerializedProperty diffScrollProperty = editoSerializedObject.FindProperty("diffScroll");
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
				GUILayout.Label(new GUIContent("Merge"), "AssetLabel");
			GUILayout.Label(new GUIContent("Commit Message: "));
			EditorGUILayout.EndHorizontal();
			commitMessage = EditorGUILayout.TextArea(commitMessage, GUILayout.Height(70));
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("Commit")))
			{
				Commit();
			}
			GUI.enabled = !GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Commit);
			if (GUILayout.Button(new GUIContent("Commit and Push")))
			{
				Commit();
				ScriptableWizard.DisplayWizard<GitPushWizard>("Push", "Push");
			}
			settings.emptyCommit = GUILayout.Toggle(settings.emptyCommit,new GUIContent("Empty Commit", "Commit the message only without changes"));
			EditorGUI.BeginChangeCheck();
			settings.amendCommit = GUILayout.Toggle(settings.amendCommit,new GUIContent("Amend Commit", "Amend previous commit."));
			if (EditorGUI.EndChangeCheck())
			{
				if (settings.amendCommit && string.IsNullOrEmpty(commitMessage))
				{
					commitMessage = GitManager.Repository.Head.Tip.Message;
				}
			}
			settings.prettify = GUILayout.Toggle(settings.prettify,new GUIContent("Prettify", "Prettify the commit message"));
			GUI.enabled = true;
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		private void Commit()
		{
			Signature signature = GitManager.Signature;
			try
			{
				if (!GitExternalManager.TakeCommit(commitMessage))
				{
					GitManager.Repository.Commit(commitMessage, signature, signature, new CommitOptions() { AllowEmptyCommit = settings.emptyCommit, AmendPreviousCommit = settings.amendCommit, PrettifyMessage = settings.prettify });
					GitHistoryWindow.GetWindow(true);
				}
				GitManager.Update();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				GUI.FocusControl("");
				commitMessage = string.Empty;
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
			float elementsTotalHeight = (statusList.Count(i => settings.MinimizedFileStatus.IsFlagSet(i.State)) + totalTypesCount)  * elementHeight;

			GUILayout.BeginArea(DiffToolbarRect, GUIContent.none, "Toolbar");
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("Edit"), "TE ToolbarDropDown",GUILayout.MinWidth(64)))
			{
				GenericMenu editMenu = new GenericMenu();
				DoDiffElementContex(editMenu);
				editMenu.ShowAsContext();
			}
			if (GUILayout.Button(new GUIContent("Filter"), "TE ToolbarDropDown", GUILayout.MinWidth(64)))
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
			GUILayout.FlexibleSpace();
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
				bool isExpanded = settings.MinimizedFileStatus.IsFlagSet(mergedStatus);
				Rect elementRect;

				if (!lastFileStatus.HasValue || lastFileStatus != mergedStatus)
				{
					elementRect = new Rect(0, infoX, diffScrollContentRect.width, elementHeight);
					lastFileStatus = mergedStatus;
					FileStatus newState = lastFileStatus.Value;
					if (current.type == EventType.Repaint)
					{
						styles.diffScrollHeader.Draw(elementRect, new GUIContent(mergedStatus.ToString()),false,false,false,false);
						GUI.Box(new Rect(elementRect.x + 12, elementRect.y + 14, elementRect.width - 12, elementRect.height - 24), new GUIContent(GitManager.GetDiffTypeIcon(info.State,false)), GUIStyle.none);
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
				elementRect = new Rect(0, infoX, diffScrollContentRect.width, elementHeight);
				DoFileDiff(elementRect,info);
				DoFileDiffSelection(elementRect,info,index);
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

		private void DoFileDiff(Rect rect,StatusListEntry info)
		{
			Event current = Event.current;

			
			if (rect.y > DiffRect.height + diffScroll.y || rect.y + rect.height < diffScroll.y)
			{
				return;
			}

			Rect stageToggleRect = new Rect(rect.x + rect.width - rect.height, rect.y + 14, rect.height, rect.height);

			if (current.type == EventType.Repaint)
			{
				string filePath = info.Path;
				string fileName = Path.GetFileName(filePath);

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

				bool canUnstage = GitManager.CanUnstage(info.State);
				bool canStage = GitManager.CanStage(info.State);
				if (canUnstage)
				{
					GUIContent content = EditorGUIUtility.IconContent("toggle on act@2x");
					GUI.Box(new Rect(rect.x + rect.width - rect.height, rect.y + 14, rect.height, rect.height), content, GUIStyle.none);
				}
				else if(canStage)
				{
					GUIContent content = EditorGUIUtility.IconContent("toggle act@2x");
					GUI.Box(stageToggleRect, content, GUIStyle.none);
				}

				GUIContent iconContent = null;
				string extension = Path.GetExtension(filePath);
				if (string.IsNullOrEmpty(extension))
				{
					iconContent = EditorGUIUtility.IconContent("Folder Icon");
				}

				if (iconContent == null)
				{
					if (asset != null)
					{
						iconContent = new GUIContent(AssetPreview.GetMiniThumbnail(asset));
						iconContent.tooltip = asset.GetType().Name;
					}
					else
					{
						iconContent = EditorGUIUtility.IconContent("DefaultAsset Icon");
						iconContent.tooltip = "Unknown Type";
					}
				}

				float x = rect.x + elementSideMargin;
				GUI.Box(new Rect(x, rect.y + elementTopBottomMargin, iconSize, iconSize), iconContent, styles.assetIcon);
				x += iconSize + 8;


				styles.diffElementName.Draw(new Rect(x, rect.y + elementTopBottomMargin + 2, rect.width - elementSideMargin - iconSize - rect.height, EditorGUIUtility.singleLineHeight), new GUIContent(fileName), false, info.Selected, info.Selected, false);

				x = rect.x + elementSideMargin + iconSize + 8;
				GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), GitManager.GetDiffTypeIcon(info.State, false), GUIStyle.none);
				x += 25;
				if (info.MetaChange.IsFlagSet(MetaChangeEnum.Meta))
				{
					GUIContent metaIconContent = EditorGUIUtility.IconContent("UniGit/meta");
					metaIconContent.tooltip = ".meta file changed";
					GUI.Box(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 4, 21, 21), metaIconContent, GUIStyle.none);
					x += 25;
				}

				styles.diffElementPath.Draw(new Rect(x, rect.y + elementTopBottomMargin + EditorGUIUtility.singleLineHeight + 7 , rect.width - elementSideMargin - iconSize - rect.height*2, EditorGUIUtility.singleLineHeight), new GUIContent(filePath),false, info.Selected, info.Selected, false);
			}
			else if (current.type == EventType.MouseDown)
			{
				if (current.button == 0 && stageToggleRect.Contains(current.mousePosition))
				{
					bool updateFlag = false;
					if (GitManager.CanStage(info.State))
					{
						GitManager.Repository.Stage(info.Path);
						updateFlag = true;
					}
					else if(GitManager.CanUnstage(info.State))
					{
						GitManager.Repository.Unstage(info.Path);
						updateFlag = true;
					}

					if (updateFlag)
					{
						Repaint();
						current.Use();
						UpdateStatusList();
					}
				}
			}
		}

		private void DoFileDiffSelection(Rect elementRect,StatusListEntry info, int index)
		{
			Event current = Event.current;

			if (elementRect.Contains(current.mousePosition))
			{
				if (current.type == EventType.ContextClick)
				{
					GenericMenu editMenu = new GenericMenu();
					DoDiffElementContex(editMenu);
					editMenu.ShowAsContext();
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
							foreach (var selectInfo in statusList.Where(i => settings.showFileStatusTypeFilter.IsFlagSet(i.State)))
							{
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
							lastSelectedIndex = index;
							statusList.SelectAll(false);
							info.Selected = !info.Selected;
							GUI.FocusControl(info.Path);
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

		#region Menu Callbacks

		private void DoDiffStatusContex(FileStatus fileStatus, GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Select All"), false, SelectFilteredCallback, fileStatus);
			if (GitManager.CanStage(fileStatus))
			{
				menu.AddItem(new GUIContent("Add All"), false, () =>
				{
					GitManager.Repository.Stage(statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => GitManager.GetPathWithMeta(s.Path)));
					GitManager.Update();
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
					GitManager.Repository.Unstage(statusList.Where(s => s.State.IsFlagSet(fileStatus)).SelectMany(s => GitManager.GetPathWithMeta(s.Path)));
					GitManager.Update();
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Remove All"));
			}
		}

		private void DoDiffElementContex(GenericMenu editMenu)
		{
			StatusListEntry[] entries = statusList.Where(e => e.Selected).ToArray();
			FileStatus selectedFlags = entries.Select(e => e.State).CombineFlags();

			GUIContent addContent = new GUIContent("Stage");
			if (GitManager.CanStage(selectedFlags))
			{
				editMenu.AddItem(addContent, false, AddSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(addContent);
			}
			GUIContent removeContent = new GUIContent("Unstage");
			if (GitManager.CanUnstage(selectedFlags))
			{
				editMenu.AddItem(removeContent, false, RemoveSelectedCallback);
			}
			else
			{
				editMenu.AddDisabledItem(removeContent);
			}
			
			editMenu.AddSeparator("");
			if (entries.Length == 1)
			{
				string path = entries[0].Path;
				if (selectedFlags.IsFlagSet(FileStatus.Conflicted))
				{
					if (GitConflictsHandler.CanResolveConflictsWithTool(path))
					{
						editMenu.AddItem(new GUIContent("Resolve Conflicts"), false, ResolveConflictsCallback, path);
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
					editMenu.AddItem(new GUIContent("Difference"), false, SeeDifferenceSelectedCallback, entries[0].Path);
					editMenu.AddItem(new GUIContent("Difference with previous version"),false, SeeDifferencePrevSelectedCallback, entries[0].Path);
				}
			}
			editMenu.AddSeparator("");
			editMenu.AddItem(new GUIContent("Revert"), false, RevertSelectedCallback);
			editMenu.AddSeparator("");
			editMenu.AddItem(new GUIContent("Reload"), false, ReloadCallback);
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
			GitManager.Update(true);
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

		private void SeeDifferenceSelectedCallback(object path)
		{
			GitManager.ShowDiff((string)path);
		}

		private void SeeDifferencePrevSelectedCallback(object path)
		{
			GitManager.ShowDiffPrev((string)path);
		}

		private void RevertSelectedCallback()
		{
			GitManager.Repository.CheckoutPaths("HEAD",statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)),new CheckoutOptions() {CheckoutModifiers = CheckoutModifiers.Force,OnCheckoutProgress = OnRevertProgress });
			EditorUtility.ClearProgressBar();
		}

		private void OnRevertProgress(string path,int currentSteps,int totalSteps)
		{
			float percent = (float) currentSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Reverting File",string.Format("Reverting file {0} {1}%",path, (percent * 100).ToString("####")), percent);
			if (currentSteps >= totalSteps)
			{
				EditorUtility.ClearProgressBar();
				GitManager.Update();
				GetWindow<GitDiffWindow>().ShowNotification(new GUIContent("Revert Complete!"));
			}
		}

		private void RemoveSelectedCallback()
		{
			GitManager.Repository.Unstage(statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)));
			GitManager.Update();
		}

		private void AddSelectedCallback()
		{
			GitManager.Repository.Stage(statusList.Where(e => e.Selected).SelectMany(e => GitManager.GetPathWithMeta(e.Path)));
			GitManager.Update();
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
		[Serializable]
		public class StatusList : IEnumerable<StatusListEntry>
		{
			[SerializeField]
			private List<StatusListEntry> entires;

			public StatusList(IEnumerable<StatusEntry> enumerable, FileStatus filter)
			{
				entires = new List<StatusListEntry>();
				BuildList(enumerable, filter);
			}

			private void BuildList(IEnumerable<StatusEntry> enumerable, FileStatus filter)
			{
				foreach (var entry in enumerable.Where(e => filter.IsFlagSet(e.State)))
				{
					if (entry.FilePath.EndsWith(".meta"))
					{
						string mainAssetPath = entry.FilePath.Remove(entry.FilePath.Length - 5, 5);
						StatusListEntry ent = entires.FirstOrDefault(e => e.Path == mainAssetPath);
						if (ent != null)
						{
							ent.MetaChange |= MetaChangeEnum.Meta;
						}
						else
						{
							entires.Add(new StatusListEntry(mainAssetPath, entry.State, MetaChangeEnum.Meta));
						}
					}
					else
					{
						StatusListEntry ent = entires.FirstOrDefault(e => e.Path == entry.FilePath);
						if (ent != null)
						{
							ent.State = entry.State;
						}
						else
						{
							entires.Add(new StatusListEntry(entry.FilePath, entry.State, MetaChangeEnum.Object));
						}
					}
				}

				entires.Sort(SortHandler);
			}

			private int SortHandler(StatusListEntry left, StatusListEntry right)
			{
				int stateCompare = GetPriority(left.State).CompareTo(GetPriority(right.State));
				return stateCompare == 0 ? string.Compare(left.Path, right.Path, StringComparison.InvariantCultureIgnoreCase) : stateCompare;
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
			private MetaChangeEnum metaChange;
			[SerializeField]
			private FileStatus state;
			[SerializeField]
			private bool selected;

			public StatusListEntry(string path, FileStatus state, MetaChangeEnum metaChange)
			{
				this.path = path;
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
		#endregion
	}
}