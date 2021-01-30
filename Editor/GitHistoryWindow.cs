using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;
using Random = UnityEngine.Random;

namespace UniGit
{
	public class GitHistoryWindow : GitUpdatableWindow, IHasCustomMenu
	{
		private const string WindowName = "Git History";
		private static readonly Color headColor = new Color32(72, 123, 207, 255);
		private static readonly Color remoteColor = new Color32(234, 141, 43, 255);
		private const int CommitsPerExpand = 8;
		private const int MaxFirstCommitCount = 16;
		public const int ProfilePixtureSize = 32;

		private List<string> loadingProfilePicturesToRemove; 
		private Dictionary<string, UnityWebRequest> loadingProfilePictures;
		private Dictionary<string, Texture2D> cachedProfilePicturesDictionary;
		[SerializeField] private List<ProfilePicture> serializedProfilePictures;
		private static Styles styles;
		private BranchInfo selectedBranch;
		private readonly List<BranchInfo> cachedBranches = new List<BranchInfo>();
		private CommitInfo[] cachedCommits = new CommitInfo[0];
		private Rect[] commitRects;
		private Rect historyScrollContentsRect;
		private Rect warningBoxRect;
		private bool hasConflicts;
		private readonly Queue<KeyValuePair<Rect, PopupWindowContent>> popupsQueue = new Queue<KeyValuePair<Rect, PopupWindowContent>>();
		[SerializeField] private Vector2 historyScroll;
		[SerializeField] private string selectedBranchName;
		[SerializeField] private int maxCommitsCount;
		private readonly object commitCachesLock = new object();
		private GitAsyncOperation loadingCommits;
		private GitExternalManager externalManager;
		private GitAsyncManager asyncManager;
		private GitOverlay gitOverlay;
		private InjectionHelper injectionHelper;

		#region Visual Elements

        private VisualElement commitsWindowElement;
        private VisualElement commitsElement;
        private VisualElement toolbarElement;

        #endregion

        #region Styles
        public class Styles
		{
			public GUIStyle historyKnobNormal;
			public GUIStyle historyKnobHead;
			public GUIStyle historyKnobRemote;
			public GUIStyle historyKnobOther;
			public GUIStyle headCommitTag;
			public GUIStyle remoteCommitTag;
			public GUIStyle otherCommitTag;
			public GUIStyle commitLine;
			public GUIStyle historyHelpBox;
			public GUIStyle historyHelpBoxLabel;
			public GUIStyle commitMessage;
			public GUIStyle avatar;
			public GUIStyle avatarName;
			public GUIStyle historyLine;
			public GUIStyle loadMoreCommitsBtn;
			public GUIStyle resetCommitsBtn;
			public GUIStyle commitArrow;
			public GUIStyle commitBg;
			public GUIStyle commitHashSeparator;
		}

		private void CreateStyles()
        {
            if (styles != null) return;
            GitProfilerProxy.BeginSample("Git History Window Style Creation", this);
            try
            {
                styles = new Styles
                {
                    historyKnobNormal = new GUIStyle("sv_iconselector_labelselection")
                    {
                        border = new RectOffset(6, 6, 6, 6),
                        margin = new RectOffset(0, 0, 0, 0),
                        fixedHeight = 0
                    },
                    historyKnobHead = new GUIStyle() {border = new RectOffset(6, 6, 6, 6), fixedHeight = 0, normal = new GUIStyleState() {background = EditorGUIUtility.FindTexture("sv_label_1")}},
                    historyKnobRemote = new GUIStyle() {border = new RectOffset(6, 6, 6, 6), fixedHeight = 0, normal = new GUIStyleState() {background = EditorGUIUtility.FindTexture("sv_label_5")}},
                    historyKnobOther = new GUIStyle() {border = new RectOffset(6, 6, 6, 6), fixedHeight = 0, normal = new GUIStyleState() {background = EditorGUIUtility.FindTexture("sv_label_2")}},
                    headCommitTag = new GUIStyle("AssetLabel") {normal = {background = EditorGUIUtility.FindTexture("sv_label_0") }},
                    remoteCommitTag = new GUIStyle("AssetLabel") {normal = {background = EditorGUIUtility.FindTexture("sv_label_0") }},
                    otherCommitTag = new GUIStyle("OverrideMargin"){padding = new RectOffset(3,3,1,1)},
                    historyHelpBox = new GUIStyle(EditorStyles.helpBox) {richText = true, padding = new RectOffset(8, 8, 8, 8), alignment = TextAnchor.MiddleLeft, contentOffset = new Vector2(24, -2)},
                    historyHelpBoxLabel = new GUIStyle("CN EntryWarn"),
                    commitMessage = new GUIStyle("Badge") {alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 4, 6, 4),fixedHeight = 22, clipping = TextClipping.Clip},
                    avatar = new GUIStyle("ShurikenEffectBg") {contentOffset = Vector3.zero, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, imagePosition = ImagePosition.ImageOnly},
                    avatarName = new GUIStyle("ShurikenEffectBg") {contentOffset = Vector3.zero, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, imagePosition = ImagePosition.TextOnly, fontSize = 28, fontStyle = FontStyle.Bold, normal = {textColor = Color.white}},
                    historyLine = "AppToolbar",
                    loadMoreCommitsBtn = "ButtonLeft",
                    resetCommitsBtn = "ButtonRight",
                    commitArrow = "AC LeftArrow",
                    commitBg = "ProfilerScrollviewBackground",
                    commitHashSeparator = "EyeDropperHorizontalLine"
                };
            }
            finally
            {
                GitProfilerProxy.EndSample();
            }
        }
		#endregion

        [UniGitInject]
		private void Construct(GitExternalManager externalManager, 
	        GitAsyncManager asyncManager,
	        GitOverlay gitOverlay,
	        InjectionHelper injectionHelper,
	        GitSettingsManager settingsManager)
		{
			this.gitOverlay = gitOverlay;
			this.externalManager = externalManager;
			this.asyncManager = asyncManager;
	        this.injectionHelper = injectionHelper;
        }

		protected override void OnEnable()
		{
			titleContent.text = WindowName;
			base.OnEnable();
			if (maxCommitsCount <= 0)
			{
				maxCommitsCount = MaxFirstCommitCount;
			}
		}

		protected override void OnGitUpdate(GitRepoStatus status,string[] path)
		{
			Repaint();
			StartUpdateCaches(status);
		}

		private void StartUpdateCaches(GitRepoStatus status)
		{
			if (gitManager.Threading.IsFlagSet(GitSettingsJson.ThreadingType.CommitListGui))
			{
				loadingCommits = asyncManager.QueueWorkerWithLock(UpdateChaches, status, "Loading Commits", (o) =>
				{
					OnCachesUpdated();
				}, commitCachesLock,true);
			}
			else
			{
				UpdateChaches(status);
				OnCachesUpdated();
			}
		}

		private void OnCachesUpdated()
		{
			UpdateGitStatusIcon();
			Repaint();
		}

		private void UpdateChaches(GitRepoStatus status)
		{
			try
			{
				cachedBranches.Clear();
				//update all branches
				cachedBranches.AddRange(gitManager.Repository.Branches.Select(b => new BranchInfo(b)));

				//update selected branch
				SetSelectedBranch(selectedBranchName);

				var commitCount = 0;
				CommitInfo[] newCachedCommits = null;
				if (selectedBranch != null)
				{
					//update commits and limit them depending on settings
					var loadedBranch = selectedBranch.LoadBranch(gitManager);
					if (loadedBranch != null && loadedBranch.Commits != null)
					{
						var commits = loadedBranch.Commits.Take(maxCommitsCount).ToArray();
						newCachedCommits = new CommitInfo[commits.Length];
						for (var i = 0; i < commits.Length; i++)
						{
							newCachedCommits[i] = new CommitInfo(commits[i],cachedBranches.Where(b => b.Tip.Id == commits[i].Id).ToArray());
						}
						commitCount = newCachedCommits.Length;
					}
				}

				commitRects = new Rect[commitCount];
				cachedCommits = newCachedCommits;
				lock (status.LockObj)
				{
					hasConflicts = status.Any(s => s.Status == FileStatus.Conflicted);
				}
			}
			catch (Exception e)
			{
				logger.LogException(e);
			}
		}

		private void UpdateGitStatusIcon()
		{
			titleContent.image = gitManager.GetGitStatusIcon();
		}

		private void SetSelectedBranch(string canonicalName)
		{
			selectedBranchName = canonicalName;
			var tmpBranch = gitManager.Repository.Branches.FirstOrDefault(b => b.CanonicalName == canonicalName);
			if (tmpBranch != null)
			{
				selectedBranch = new BranchInfo(tmpBranch);

			}

            if (selectedBranch != null) return;
            selectedBranch = new BranchInfo(gitManager.Repository.Head);
            selectedBranchName = selectedBranch.CanonicalName;
        }

		protected override void OnInitialize()
		{
			loadingProfilePicturesToRemove = new List<string>();
			cachedProfilePicturesDictionary = new Dictionary<string, Texture2D>();
			loadingProfilePictures = new Dictionary<string, UnityWebRequest>();
			if (serializedProfilePictures != null)
			{
				foreach (var picture in serializedProfilePictures)
				{
					cachedProfilePicturesDictionary.Add(picture.email, picture.texture);
				}
			}
			else
			{
				serializedProfilePictures = new List<ProfilePicture>();
			}
		}

		protected override void OnEditorUpdate()
		{
			loadingProfilePictures ??= new Dictionary<string, UnityWebRequest>();
			cachedProfilePicturesDictionary ??= new Dictionary<string, Texture2D>();
			loadingProfilePicturesToRemove ??= new List<string>();

			if (loadingProfilePictures.Count > 0)
			{
				foreach (var profilePicture in loadingProfilePictures)
                {
                    Texture2D avatarTexture;

                    if (profilePicture.Value.result != UnityWebRequest.Result.Success && profilePicture.Value.result != UnityWebRequest.Result.InProgress)
                    {
                        logger.LogError("error", profilePicture.Value.error,this);
                        avatarTexture = new Texture2D(1, 1);
                        avatarTexture.SetPixel(0,0,new Color(0,0,0));
                    }
                    else
                    {
                        if (!profilePicture.Value.isDone) continue;

                        avatarTexture = new Texture2D(ProfilePixtureSize, ProfilePixtureSize, TextureFormat.RGBA32, false, true);
                        avatarTexture.LoadImage(profilePicture.Value.downloadHandler.data);
                    }

                    cachedProfilePicturesDictionary.Add(profilePicture.Key, avatarTexture);
                    serializedProfilePictures.RemoveAll(p => p.email == profilePicture.Key);
                    serializedProfilePictures.Add(new ProfilePicture(avatarTexture, profilePicture.Key));
                    loadingProfilePicturesToRemove.Add(profilePicture.Key);
                    profilePicture.Value.Dispose();
                    Repaint();
                }

                if (loadingProfilePicturesToRemove.Count <= 0) return;
                foreach (var key in loadingProfilePicturesToRemove)
                {
                    loadingProfilePictures.Remove(key);
                }
                loadingProfilePicturesToRemove.Clear();
            }
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			Repaint();
		}

		protected override void OnFocus()
		{
			base.OnFocus();
			GUI.FocusControl(null);
		}

        protected override void OnLostFocus()
        {
            base.OnLostFocus();
            GUI.FocusControl(null);
        }

        private const float helpBoxHeight = 38;
		private readonly float commitSpacing = EditorGUIUtility.singleLineHeight / 2;

		protected override void ConstructGUI(VisualElement root)
		{
			var uxml = resourceManager.LoadUniGitAsset<VisualTreeAsset>("Editor/UI/HistoryWindow.uxml");
			var uss = resourceManager.LoadUniGitAsset<StyleSheet>("Editor/UI/HistoryWindowSheet.uss");
			uxml.CloneTree(root);
			root.styleSheets.Add(uss);

			base.ConstructGUI(root);

            toolbarElement = root.Q("Toolbar");
            commitsWindowElement = root.Q("CommitsWindow");
            commitsElement = root.Q("Commits");

            var mainGuiElement = new IMGUIContainer(MainGUI);
			mainGuiElement.style.flexGrow = 1;

			var toolbarImgui = new IMGUIContainer(DoToolbar);
			toolbarImgui.style.flexGrow = 1;
			toolbarElement.Add(toolbarImgui);

			commitsElement.Add(mainGuiElement);
		}

		private void OnGUI()
		{
			CreateStyles();
        }

		[UsedImplicitly]
        protected override void Update()
		{
			base.Update();
			var validRepo = gitManager != null && initializer.IsValidRepo;
            if (commitsWindowElement != null)
            {
                commitsWindowElement.style.display = validRepo && gitManager.Repository != null && selectedBranch != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (toolbarElement != null)
                {
                    toolbarElement.style.display = commitsWindowElement.style.display;
                }
            }
        }

		private void MainGUI()
		{
            if (gitManager == null || !initializer.IsValidRepo || gitManager.Repository == null || selectedBranch == null)
                return;

            var repoInformation = gitManager.Repository.Info;

			DoHistoryScrollRect(commitsElement.contentRect, repoInformation);

            if (popupsQueue.Count <= 0) return;
            var content = popupsQueue.Dequeue();
            PopupWindow.Show(content.Key, content.Value);
        }

        private void DoToolbar()
        {
            if(gitManager == null || !initializer.IsValidRepo || gitManager.Repository == null || selectedBranch == null)
                return;

	        var rect = toolbarElement.contentRect;
	        var info = gitManager.Repository.Info;

	        var branch = selectedBranch.LoadBranch(gitManager);
			if (branch == null)
			{
				EditorGUILayout.HelpBox($"Invalid Branch: '{selectedBranch.CanonicalName}'",MessageType.Warning,true);
				return;
			}

			GitGUI.StartEnable();
			GitProfilerProxy.BeginSample("Git History Window Toolbar GUI",this);
			try
			{
				GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
				var btRect = new Rect(rect.x, rect.y, 64, rect.height);
				var pushButtonContent = GitGUI.GetTempContent("Push", GitGUI.Textures.CollabPush, "Push local changes to a remote repository.");
				if (info.CurrentOperation == CurrentOperation.Merge)
				{
					GUI.enabled = false;
					pushButtonContent.tooltip = "Do a Merge commit before pushing.";
				}
				else if (hasConflicts)
				{
					GUI.enabled = false;
					pushButtonContent.tooltip = "Resolve conflicts before pushing.";
				}
				else if (selectedBranch == null)
				{
					GUI.enabled = false;
					pushButtonContent.tooltip = "No Selected branch. Create a new branch or create atleast one commit.";
				}
				if (GUI.Button(btRect, pushButtonContent, EditorStyles.toolbarButton))
				{
					GoToPush();
				}
				btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
				GUI.enabled = !hasConflicts;
				if (GUI.Button(btRect, GitGUI.IconContent("CollabPull", "Pull", hasConflicts ? "Must resolve conflicts before pulling" : "Pull changes from remote repository by fetching them and then merging them. This is the same as calling Fetch then Merge."), EditorStyles.toolbarButton))
				{
					GoToPull();
				}
				btRect = new Rect(btRect.x + 70, btRect.y, 64, btRect.height);
				var fetchContent = GitGUI.GetTempContent("Fetch", gitOverlay.icons.fetch.image, "Get changes from remote repository but do not merge them.");
				if (branch.Remote == null)
				{
					fetchContent.tooltip = "Branch does not have a remote.";
					GUI.enabled = false;
				}
				else if (branch.IsRemote)
				{
					fetchContent.tooltip = "Branch is remote.";
					GUI.enabled = false;
				}
				if (GUI.Button(btRect, fetchContent, EditorStyles.toolbarButton))
				{
					GoToFetch();
				}
				GUI.enabled = true;
				btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
				if (info.CurrentOperation == CurrentOperation.Merge)
				{
					if (GUI.Button(btRect, GitGUI.GetTempContent("Merge", gitOverlay.icons.merge.image, hasConflicts ? "Must Resolve conflict before merging" : "Merge fetched changes from remote repository. Changes from the latest fetch will be merged."), EditorStyles.toolbarDropDown))
					{
						var menu = new GenericMenu();
						menu.AddDisabledItem(new GUIContent("Merge"));
						menu.AddItem(new GUIContent("Cancel Merge (Reset Head)"),false, () =>
						{
							if (externalManager.TakeReset(gitManager.Repository.Head.Tip))
							{
								gitCallbacks.IssueAssetDatabaseRefresh();
								gitManager.MarkDirty();
							}
							else
							{
								popupsQueue.Enqueue(new KeyValuePair<Rect, PopupWindowContent>(btRect, injectionHelper.CreateInstance<ResetPopupWindow>(gitManager.Repository.Head.Tip)));
							}
						});
						menu.DropDown(btRect);
					}
				}
				else
				{
					if (GUI.Button(btRect, GitGUI.GetTempContent("Merge", gitOverlay.icons.merge.image, hasConflicts ? "Must Resolve conflict before merging" : "Merge fetched changes from remote repository. Changes from the latest fetch will be merged."), EditorStyles.toolbarButton))
					{
						GoToMerge();
					}
				}
				
				GUI.enabled = initializer.IsValidRepo;
				btRect = new Rect(btRect.x + 64,btRect.y,64,btRect.height);
				if (GUI.Button(btRect, GitGUI.GetTempContent("Stash", gitOverlay.icons.stashIcon.image), EditorStyles.toolbarButton))
				{
					PopupWindow.Show(btRect,injectionHelper.CreateInstance<GitStashWindow>());
				}
				GUI.enabled = true;

				var branchNameContent = GitGUI.GetTempContent(string.IsNullOrEmpty(selectedBranchName) ? "Branch" : selectedBranch.FriendlyName);
				if (selectedBranch.IsRemote)
					branchNameContent.image = GitGUI.IconContentTex("ToolHandleGlobal");
				else if (!selectedBranch.IsCurrentRepositoryHead)
					branchNameContent.image = GitGUI.IconContentTex("IN LockButton on");

				var branchNameWidth = EditorStyles.toolbarDropDown.CalcSize(branchNameContent).x;
				btRect = new Rect(rect.x + rect.width - branchNameWidth, btRect.y, branchNameWidth, btRect.height);
				if (GUI.Button(btRect, branchNameContent, EditorStyles.toolbarDropDown))
				{
					var selectBranchMenu = new GenericMenu();
					foreach (var cachedBranch in cachedBranches)
					{
						selectBranchMenu.AddItem(new GUIContent(cachedBranch.FriendlyName), selectedBranchName == cachedBranch.CanonicalName, (b) =>
						{
							SetSelectedBranch((string)b);
							StartUpdateCaches(data.RepositoryStatus);
						}, cachedBranch.CanonicalName);
					}
					selectBranchMenu.ShowAsContext();
				}
				GitGUI.EndEnable();
				btRect = new Rect(btRect.x - 64, btRect.y, 64, btRect.height);
				GitGUI.StartEnable(gitSettings.ExternalsType.IsFlagSet(GitSettingsJson.ExternalsTypeEnum.Switch) || (!selectedBranch.IsRemote && !selectedBranch.IsCurrentRepositoryHead));
				if (GUI.Button(btRect, GitGUI.GetTempContent("Switch", gitOverlay.icons.checkout.image, selectedBranch.IsRemote ? "Cannot switch to remote branches." : selectedBranch.IsCurrentRepositoryHead ? "This branch is the active one" : "Switch to another branch"), EditorStyles.toolbarButton))
				{
					if (externalManager.TakeSwitch())
					{
						gitCallbacks.IssueAssetDatabaseRefresh();
						gitManager.MarkDirty();
					}
					else
					{
						PopupWindow.Show(btRect,injectionHelper.CreateInstance<GitCheckoutWindowPopup>(selectedBranch.LoadBranch(gitManager)));
					}
				}
				GitGUI.EndEnable();
				btRect = new Rect(btRect.x - 21, btRect.y+1, 21, btRect.height);
				EditorGUIUtility.AddCursorRect(btRect,MouseCursor.Link);
				if (GUI.Button(btRect, GitGUI.Contents.Help, GitGUI.Styles.IconButton))
				{
					GitLinks.GoTo(GitLinks.HistoryWindowHelp);
				}
			}
			finally
			{
				GitProfilerProxy.EndSample();
			}
		}

		private void GoToMerge()
		{
			if (externalManager.TakeMerge())
			{
				gitManager.MarkDirty();
			}
			else
			{
				UniGitLoader.DisplayWizard<GitMergeWizard>("Git Merge","Merge");
			}
		}

		private void GoToFetch()
		{
			var branch = selectedBranch.LoadBranch(gitManager);
			if (externalManager.TakeFetch(branch.Remote.Name))
			{
				gitManager.MarkDirty();
			}
			else
			{
				var wizard = UniGitLoader.DisplayWizard<GitFetchWizard>("Git Fetch","Fetch");
				wizard.Init(branch);
			}
		}

		private void GoToPull()
		{
			if (externalManager.TakePull())
			{
				gitCallbacks.IssueAssetDatabaseRefresh();
				gitManager.MarkDirty();
			}
			else
			{
			    var wizard = UniGitLoader.DisplayWizard<GitPullWizard>("Git Pull", "Pull");
			    wizard.Init(selectedBranch.LoadBranch(gitManager));
			}
		}

		private void GoToPush()
		{
			if (externalManager.TakePush())
			{
				gitManager.MarkDirty();
			}
			else
			{
				var wizard = UniGitLoader.DisplayWizard<GitPushWizard>("Git Push","Push");
				wizard.Init(selectedBranch.LoadBranch(gitManager));
			}
		}

		private void DoHistoryScrollRect(Rect rect, RepositoryInformation info)
		{
			if (loadingCommits != null && !loadingCommits.IsDone)
			{
				if(gitSettings.AnimationType.HasFlag(GitSettingsJson.AnimationTypeEnum.Loading)) Repaint();
				GitGUI.DrawLoading(rect, GitGUI.GetTempContent("Loading Commit History"));
				return;
			}

			var current = Event.current;

			GUI.Box(new Rect(14, rect.y + 2, 2, rect.height), GUIContent.none, styles.historyLine);

			//behind,ahead and merge checking

			var displayWarnningBox = DoWarningBoxValidate(info,selectedBranch);

			//commit layout
			if (current.type == EventType.Layout)
			{
				GitProfilerProxy.BeginSample("Git History Window Scroll Rect GUI Layout", this);
				try
				{
					var lastCommitRect = new Rect(32, commitSpacing, Mathf.Max(rect.width - 24, 512) - 32, 0);

					if (displayWarnningBox)
					{
						warningBoxRect = new Rect(lastCommitRect.x, lastCommitRect.y, lastCommitRect.width, helpBoxHeight);
						lastCommitRect.y += helpBoxHeight + commitSpacing;
					}

					for (var i = 0; i < cachedCommits.Length; i++)
					{
						lastCommitRect = LayoutCommit(lastCommitRect, cachedCommits[i]);
						if (i < commitRects.Length)
						{
							commitRects[i] = lastCommitRect;
						}
					}

					historyScrollContentsRect = new Rect(0, 0, lastCommitRect.width + 32, lastCommitRect.y + lastCommitRect.height + commitSpacing*2);
					historyScrollContentsRect.height += EditorGUIUtility.singleLineHeight*3;
				}
				finally
				{
					GitProfilerProxy.EndSample();
				}
			}
			else
			{
				GitProfilerProxy.BeginSample("Git History Window Scroll Rect GUI Other", this);
				try
				{
					historyScroll = GUI.BeginScrollView(rect, historyScroll, historyScrollContentsRect);

					if (displayWarnningBox)
					{
						DoWarningBox(warningBoxRect, info,selectedBranch);
					}

					for (var i = 0; i < cachedCommits.Length; i++)
					{
						if (i < commitRects.Length)
						{
							DoCommit(commitRects[i], rect, cachedCommits[i]);
						}
					}

					var commitsCountRect = new Rect(32, historyScrollContentsRect.height - EditorGUIUtility.singleLineHeight * 4, historyScrollContentsRect.width - 64, EditorGUIUtility.singleLineHeight);

					GUI.Label(commitsCountRect,GitGUI.GetTempContent(cachedCommits.Length + " / " + maxCommitsCount),EditorStyles.centeredGreyMiniLabel);

					var resetRect = new Rect(historyScrollContentsRect.width / 2, historyScrollContentsRect.height - EditorGUIUtility.singleLineHeight * 3, 64, EditorGUIUtility.singleLineHeight);
					var loadMoreRect = new Rect(historyScrollContentsRect.width / 2 - 64, historyScrollContentsRect.height - EditorGUIUtility.singleLineHeight * 3, 64, EditorGUIUtility.singleLineHeight);
					if (GUI.Button(loadMoreRect, GitGUI.IconContent("ol plus", "More","Show more commits."), styles.loadMoreCommitsBtn))
					{
						maxCommitsCount += CommitsPerExpand;
						StartUpdateCaches(data.RepositoryStatus);
					}
					GitGUI.StartEnable(maxCommitsCount != MaxFirstCommitCount);
					if (GUI.Button(resetRect, GitGUI.GetTempContent("Reset","Reset the number of commits show."), styles.resetCommitsBtn))
					{
						if (MaxFirstCommitCount < maxCommitsCount)
						{
							maxCommitsCount = MaxFirstCommitCount;
							Array.Resize(ref cachedCommits, maxCommitsCount);
						}
						else
						{
							maxCommitsCount = MaxFirstCommitCount;
							StartUpdateCaches(data.RepositoryStatus);
						}
					}
					GitGUI.EndEnable();
					GUI.EndScrollView();
				}
				finally
				{
					GitProfilerProxy.EndSample();
				}
			}
		}

		private Rect LayoutCommit(Rect lastCommitRect, CommitInfo commit)
		{
			var isHeadOrRemote = commit.IsRemote || commit.Branches != null;
			var commitHeight = 7 * EditorGUIUtility.singleLineHeight;
			if (isHeadOrRemote) commitHeight += EditorGUIUtility.singleLineHeight;
			var commitRect = new Rect(lastCommitRect.x, lastCommitRect.y + lastCommitRect.height + commitSpacing, lastCommitRect.width, commitHeight);
			return commitRect;
		}

		private void DoCommit(Rect rect,Rect scrollRect,CommitInfo commit)
		{
			GitProfilerProxy.BeginSample("Git History Window Commit GUI",this);
			try
			{
				var current = Event.current;

				if (rect.y > scrollRect.height + historyScroll.y || rect.y + scrollRect.height < historyScroll.y)
				{
					return;
				}

				var branches = commit.Branches;
				var isHead = commit.IsHead;
				var isRemote = commit.IsRemote;

				var branchColor = Color.white;
				if (branches != null)
				{
					foreach (var branch in branches)
					{
						if (branch.IsRemote)
						{
							branchColor = remoteColor;
							break;
						}
						if (branch.IsCurrentRepositoryHead)
						{
							branchColor = headColor;
							break;
						}
						Random.InitState(branch.CanonicalName.GetHashCode());
						branchColor = Random.ColorHSV(0, 1, 0, 1);
					}
				}

				GUI.backgroundColor = new Color(1,1,1,0.4f);
				GUI.Box(new Rect(24, rect.y + 5, 16, 16), GUIContent.none, styles.commitArrow);
				GUI.backgroundColor = branchColor;
				GUI.Box(new Rect(9, rect.y + 6, 12, 12), GUIContent.none, styles.historyKnobNormal);
				GUI.backgroundColor = Color.white;

				float y = 8;
				float x = 12;
                GUI.Box(rect, GUIContent.none, styles.commitBg);
				if (isHead || isRemote)
				{
					GUI.color = branchColor;
					GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 5),Texture2D.whiteTexture);
					GUI.color = Color.white;
					y += 4;
				}

				if (gitSettings.UseGavatar)
				{
					var avatar = GetProfilePixture(commit.Committer.Email);
					GUI.Box(new Rect(rect.x + x, rect.y + y, 32, 32), avatar != null ? GitGUI.GetTempContent(avatar) : gitOverlay.icons.loadingIconSmall, styles.avatar);
				}
				else
				{
					Random.InitState(commit.Committer.Name.GetHashCode());
					GUI.contentColor = Random.ColorHSV(0, 1, 0.6f, 0.6f, 0.8f, 1, 1, 1);
					GUI.Box(new Rect(rect.x + x, rect.y + y, 32, 32), GitGUI.GetTempContent(commit.Committer.Name.Substring(0,1).ToUpper()), styles.avatarName);
					GUI.contentColor = Color.white;
				}
			
			
				x += 38;
				EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(commit.Committer.Name), EditorStyles.boldLabel);
				y += 16;
				EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(GitGUI.FormatRemainingTime(commit.Committer.When.DateTime)));
				y += EditorGUIUtility.singleLineHeight + 3;
				var firstNewLineIndex = commit.Message.IndexOf(Environment.NewLine, StringComparison.Ordinal);
				EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x - 10, EditorGUIUtility.singleLineHeight + 4), GitGUI.GetTempContent(firstNewLineIndex > 0 ? commit.Message.Substring(0, firstNewLineIndex) : commit.Message, commit.Message), styles.commitMessage);
				y += 8;
				if (branches != null)
				{
					if (branches.Length > 0)
					{
						y += EditorGUIUtility.singleLineHeight;
					}
					foreach (var branch in branches)
					{
						var style = styles.otherCommitTag;
						if (branch.IsRemote)
						{
							GUI.backgroundColor = remoteColor;
						}
						else if (branch.IsCurrentRepositoryHead)
						{
							GUI.backgroundColor = headColor;
						}
						else
						{
							Random.InitState(branch.CanonicalName.GetHashCode());
							GUI.backgroundColor = Random.ColorHSV(0, 1, 0, 1);
						}
						var labelContent = GitGUI.GetTempContent(branch.FriendlyName, branch.CanonicalName);
						var labelWidth = style.CalcSize(labelContent).x;
						var branchIconRect = new Rect(rect.x + x, rect.y + y, labelWidth, EditorGUIUtility.singleLineHeight);
						GUI.Label(branchIconRect, labelContent, style);
						x += labelWidth + 4;
						GUI.backgroundColor = Color.white;

						if (Event.current.type == EventType.ContextClick && branchIconRect.Contains(Event.current.mousePosition))
						{
							var branchContextMenu = new GenericMenu();
							var b = branch;
							branchContextMenu.AddItem(new GUIContent("View branch"),false,()=> { ViewBranchCallback(b); });
							if(!b.IsRemote && !b.IsCurrentRepositoryHead)
								branchContextMenu.AddItem(new GUIContent("Switch To Branch"), false,()=> { SwitchToBranchCallback(b, new Rect(branchIconRect.x - historyScroll.x, branchIconRect.y - historyScroll.y, branchIconRect.width, branchIconRect.height)); });
							else
								branchContextMenu.AddDisabledItem(new GUIContent("Switch To Branch"));
							branchContextMenu.ShowAsContext();
						}
					}
				}

				x = 12;
				y += EditorGUIUtility.singleLineHeight * 1.5f;
				GUI.Box(new Rect(rect.x + x, rect.y + y, rect.width - x - x, EditorGUIUtility.singleLineHeight), GUIContent.none, styles.commitHashSeparator);
				y += EditorGUIUtility.singleLineHeight / 3;
				EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(commit.Id.Sha));
				x = rect.width + 24;
				GUI.enabled = true;
				var buttonRect = new Rect(x - 21, rect.y + y, 21, 21);
				x -= buttonRect.width;
				if (GUI.Button(buttonRect, GitGUI.IconContent("UnityEditor.InspectorWindow",string.Empty,"Details"), GitGUI.Styles.IconButton))
				{
					PopupWindow.Show(buttonRect, injectionHelper.CreateInstance<GitCommitDetailsWindow>(gitManager.Repository.Lookup<Commit>(commit.Id)));
				}
				EditorGUIUtility.AddCursorRect(buttonRect,MouseCursor.Link);
				buttonRect = new Rect(x - 21, rect.y + y, 21, 21);
				x -= buttonRect.width;
				if (GUI.Button(buttonRect, GitGUI.IconContent("UnityEditor.SceneHierarchyWindow",string.Empty,"Options"), GitGUI.Styles.IconButton))
				{
					var menu = new GenericMenu();

					if (selectedBranch.IsCurrentRepositoryHead && !isHead)
					{
						var rect1 = buttonRect;
						menu.AddItem(new GUIContent("♻ Reset"), false, () =>
						{
							if (externalManager.TakeReset(gitManager.Repository.Lookup<Commit>(commit.Id)))
							{
								gitCallbacks.IssueAssetDatabaseRefresh();
								gitManager.MarkDirty();
							}
							else
							{
								popupsQueue.Enqueue(new KeyValuePair<Rect, PopupWindowContent>(rect1, injectionHelper.CreateInstance<ResetPopupWindow>(gitManager.Repository.Lookup<Commit>(commit.Id))));
							}
						});
					}
					else
					{
						menu.AddDisabledItem(new GUIContent("♻ Reset"));
					}
					var rect2 = buttonRect;
					menu.AddItem(new GUIContent("⎇ Branch Out"), false, () =>
					{
						popupsQueue.Enqueue(new KeyValuePair<Rect, PopupWindowContent>(rect2, injectionHelper.CreateInstance<GitCreateBranchWindow>(gitManager.Repository.Lookup<Commit>(commit.Id))));
					});
					menu.DropDown(buttonRect);
				}
				EditorGUIUtility.AddCursorRect(buttonRect,MouseCursor.Link);
                if (!rect.Contains(current.mousePosition)) return;
                if (current.type == EventType.ContextClick)
                {
                    current.Use();
                }
            }
			finally
			{
				GitProfilerProxy.EndSample();
			}
		}

		private void ViewBranchCallback(BranchInfo branch)
		{
			SetSelectedBranch(branch.CanonicalName);
			StartUpdateCaches(data.RepositoryStatus);
		}

		private void SwitchToBranchCallback(BranchInfo branch,Rect rect)
		{
			popupsQueue.Enqueue(new KeyValuePair<Rect, PopupWindowContent>(rect,injectionHelper.CreateInstance<GitCheckoutWindowPopup>(branch.LoadBranch(gitManager))));
		}

		private void DoWarningBox(Rect rect, RepositoryInformation info, BranchInfo branch)
		{
			var behindBy = selectedBranch.TrackingDetails.BehindBy;
			var content = GUIContent.none;
			if (info.CurrentOperation == CurrentOperation.Merge)
			{
				content = GitGUI.GetTempContent("Merging with remote branch in progress. You <b>must</b> do a merge commit before pushing.");
			}
			else if (behindBy != null && behindBy.Value > 0)
			{
				content = GitGUI.GetTempContent(
                    $"Branch <b>{selectedBranch.FriendlyName}</b> behind tracked branch <b>{selectedBranch.TrackedBranch}</b>");
			}
			else if (branch.IsRemote)
			{
				content = GitGUI.GetTempContent("Viewing a remote branch. Showing local history of remote branch.");
			}
			else if (!branch.IsCurrentRepositoryHead)
            {
                content = !gitManager.InSubModule ? GitGUI.GetTempContent("Viewing a branch that is not the HEAD.",GitGUI.Textures.InfoIconSmall) : GitGUI.GetTempContent("Viewing a branch that is not the HEAD in sub module. Sub module may be out of sync with it's parent project. An update of sub module might be required.",GitGUI.Textures.WarrningIconSmall);
            }

			GUI.Box(rect, content, styles.historyHelpBox);
			GUI.Box(rect, GUIContent.none, styles.historyHelpBoxLabel);
		}

		private bool DoWarningBoxValidate(RepositoryInformation info,BranchInfo branchInfo)
		{
			var behindBy = selectedBranch.TrackingDetails.BehindBy;
			return (behindBy != null && behindBy.Value > 0) | info.CurrentOperation == CurrentOperation.Merge | !branchInfo.IsCurrentRepositoryHead | branchInfo.IsRemote;
		}

		#region Menus

		public void AddItemsToMenu(GenericMenu menu)
		{
			if(gitManager == null) return;
			menu.AddItem(new GUIContent("Push"), false, GoToPush);
			menu.AddItem(new GUIContent("Pull"),false,GoToPull);
			menu.AddItem(new GUIContent("Fetch"), false, GoToFetch);
			menu.AddItem(new GUIContent("Merge"), false, GoToMerge);
			menu.AddItem(new GUIContent("Donate"), false, ()=>{GitLinks.GoTo(GitLinks.Donate);});
			menu.AddItem(new GUIContent("Help"),false, ()=>{GitLinks.GoTo(GitLinks.HistoryWindowHelp);});
		}

		#endregion

		#region Helper Methods
		private Texture2D GetProfilePixture(string email)
		{
            if (cachedProfilePicturesDictionary.TryGetValue(email, out var tex))
			{
				if (tex != null)
				{
					return tex;
				}

				cachedProfilePicturesDictionary.Remove(email);
			}

            if (loadingProfilePictures.TryGetValue(email,out var texWww))
			{
                if (!texWww.isDone) return tex;
                tex = new Texture2D(ProfilePixtureSize,ProfilePixtureSize,TextureFormat.ARGB32,true,false);
                tex.LoadImage(texWww.downloadHandler.data);
                tex.Apply(true);
                cachedProfilePicturesDictionary.Add(email, tex);
                serializedProfilePictures.RemoveAll(p => p.email == email);
                serializedProfilePictures.Add(new ProfilePicture(tex,email));
                loadingProfilePictures.Remove(email);
                texWww.Dispose();

                return tex;
			}

			var hash = HashEmailForGravatar(email.Trim());
            var loading = UnityWebRequest.Get("https://www.gravatar.com/avatar/" + hash + "?s=" + ProfilePixtureSize);
            loading.timeout = 10;
            loading.SendWebRequest();
            loadingProfilePictures.Add(email, loading);
			return null;
		}

		public static string HashEmailForGravatar(string email)
		{
			// Create a new instance of the MD5CryptoServiceProvider object.  
			var md5Hasher = MD5.Create();

			// Convert the input string to a byte array and compute the hash.  
			var data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(email));

			// Create a new Stringbuilder to collect the bytes  
			// and create a string.  
			var sBuilder = new StringBuilder();

			// Loop through each byte of the hashed data  
			// and format each one as a hexadecimal string.  
			foreach (var b in data)
            {
                sBuilder.Append(b.ToString("x2"));
            }

			return sBuilder.ToString(); // Return the hexadecimal string. 
		}
		#endregion

		#region Popup Windows

		private abstract class CommitPopupWindow : PopupWindowContent
		{
			protected Commit commit;
			protected GitManager gitManager;

			[UniGitInject]
			protected CommitPopupWindow(GitManager gitManager,Commit commit)
			{
				this.gitManager = gitManager;
				this.commit = commit;
			}
		}

		private class ResetPopupWindow : CommitPopupWindow
		{
			private ResetMode resetMode = ResetMode.Mixed;
			private CheckoutOptions checkoutOptions = new CheckoutOptions();
			private readonly GitCallbacks gitCallbacks;

			public override Vector2 GetWindowSize()
			{
				return new Vector2(256,128);
			}

			[UniGitInject]
			public ResetPopupWindow(GitManager gitManager,GitCallbacks gitCallbacks,Commit commit) : base(gitManager,commit)
			{
				this.gitCallbacks = gitCallbacks;
			}

			public override void OnGUI(Rect rect)
			{
				EditorGUILayout.Space();
				resetMode = (ResetMode)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Reset Type"), resetMode);
				switch (resetMode)
				{
					case ResetMode.Soft:
						EditorGUILayout.HelpBox("Leave working tree and index untouched",MessageType.Info);
						break;
					case ResetMode.Mixed:
						EditorGUILayout.HelpBox("Leave working tree untouched,reset index (Default)", MessageType.Info);
						break;
					case ResetMode.Hard:
						EditorGUILayout.HelpBox("Reset working tree and index (Will delete all files)",MessageType.Error);
						break;
				}
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Reset"))
				{
					if (EditorUtility.DisplayDialog("Reset", "Are you sure you want to reset to the selected commit", "Reset", "Cancel"))
					{
						GitProfilerProxy.BeginSample("Git Reset Popup",editorWindow);
						try
						{
							gitManager.Repository.Reset(resetMode,commit, checkoutOptions);
							gitManager.MarkDirty(true);
							editorWindow.Close();
						}
						finally
						{
							GitProfilerProxy.EndSample();
						}
						gitCallbacks.IssueAssetDatabaseRefresh();
					}
				}
				EditorGUILayout.Space();
			}
		}
		#endregion

		[Serializable]
		public struct ProfilePicture
		{
			public Texture2D texture;
			public string email;

			public ProfilePicture(Texture2D texture, string email)
			{
				this.texture = texture;
				this.email = email;
			}
		}

		public class BranchInfo
		{
			public readonly string CanonicalName;
			public readonly Commit Tip;
			public readonly string TrackedBranch;
			public readonly BranchTrackingDetails TrackingDetails;
			public readonly bool IsCurrentRepositoryHead;
			public readonly bool IsRemote;
			public readonly string FriendlyName;

			public BranchInfo(Branch branch)
			{
				if (branch.TrackedBranch != null)
				{
					TrackedBranch = branch.TrackedBranch.FriendlyName;
				}
				TrackingDetails = branch.TrackingDetails;
				CanonicalName = branch.CanonicalName;
				Tip = branch.Tip;
				IsCurrentRepositoryHead = branch.IsCurrentRepositoryHead;
				IsRemote = branch.IsRemote;
				FriendlyName = branch.FriendlyName;
			}

			public Branch LoadBranch(GitManager gitManager)
			{
                if (gitManager.Repository == null) return null;
                if (gitManager.Repository.Head.CanonicalName == CanonicalName)
                {
                    return gitManager.Repository.Head;
                }

                return gitManager.Repository.Branches?[CanonicalName];
            }
		}

		public readonly struct CommitInfo
		{
			public readonly ObjectId Id;
			public readonly Signature Committer;
			public readonly bool IsHead;
			public readonly bool IsRemote;
			public readonly BranchInfo[] Branches;
			public readonly string Message;

			public CommitInfo(Commit commit, BranchInfo[] branches) : this()
			{
				Id = commit.Id;
				Committer = commit.Committer;
				Message = commit.Message;

				if (branches.Length > 0)
				{
					Branches = branches;
					IsHead = branches.Any(b => b.IsCurrentRepositoryHead);
					IsRemote = branches.Any(b => b.IsRemote);
				}
			}
		}
	}
}