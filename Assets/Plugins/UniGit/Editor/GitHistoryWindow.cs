using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitHistoryWindow : GitUpdatableWindow
	{
		private Rect toolbarRect { get { return new Rect(0,0,position.width, EditorGUIUtility.singleLineHeight);} }
		private Rect scorllRect { get { return new Rect(0,toolbarRect.height+2,position.width,position.height);} }

		private List<string> lodingProfilePicturesToRemove; 
		private Dictionary<string, WWW> loadingProfilePictures;
		private Dictionary<string, Texture2D> cachedProfilePicturesDictionary;
		[SerializeField] private List<ProfilePicture> serializedProfilePictures;
		private static Styles styles;
		private BranchInfo selectedBranch;
		private BranchInfo[] cachedBranches = new BranchInfo[0];
		private CommitInfo[] cachedCommits = new CommitInfo[0];
		private Rect[] commitRects;
		private Rect historyScrollContentsRect;
		private Rect warningBoxRect;
		private bool hasConflicts;
		[SerializeField] private Vector2 historyScroll;
		[SerializeField] private string selectedBranchName;
		private object commitCachesLock = new object();

		public class Styles
		{
			public GUIStyle historyKnobNormal;
			public GUIStyle historyKnobHead;
			public GUIStyle historyKnobRemote;
			public GUIStyle historyKnobOther;
			public GUIStyle headCommitTag;
			public GUIStyle remoteCommitTag;
			public GUIStyle otherCommitTag;
			public GUIStyle commitLineHead;
			public GUIStyle commitLineRemote;
			public GUIStyle historyHelpBox;
			public GUIStyle historyHelpBoxLabel;
			public GUIStyle commitMessage;
			public GUIStyle avatar;
			public GUIStyle avatarName;
		}

		[MenuItem("Window/GIT History")]
		public static void CreateEditor()
		{
			GetWindow(true);
		}

		public static GitHistoryWindow GetWindow(bool focus)
		{
			return GetWindow<GitHistoryWindow>("Git History", focus);
		}

		private void CreateStyles()
		{
			if (styles == null)
			{
				GitProfilerProxy.BeginSample("Git History Window Style Creation",this);
				Texture2D blueTexture = new Texture2D(1,1) {hideFlags = HideFlags.HideAndDontSave};
				blueTexture.SetPixel(0,0,new Color32(72,123,207,255));
				blueTexture.Apply();

				Texture2D orangeTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
				orangeTexture.SetPixel(0, 0, new Color32(234, 141, 43, 255));
				orangeTexture.Apply();

				styles = new Styles();
				styles.historyKnobNormal = new GUIStyle("CN CountBadge");
				styles.historyKnobNormal.border = new RectOffset(8, 8, 8, 8);
				styles.historyKnobNormal.margin = new RectOffset(0, 0, 0, 0);
				styles.historyKnobNormal.fixedHeight = 0;
				styles.historyKnobHead = new GUIStyle() {border = new RectOffset(6,6,6,6),fixedHeight = 0,normal = new GUIStyleState() {background = EditorGUIUtility.FindTexture("sv_label_1") } };
				styles.historyKnobRemote = new GUIStyle() { border = new RectOffset(6, 6, 6, 6), fixedHeight = 0, normal = new GUIStyleState() { background = EditorGUIUtility.FindTexture("sv_label_5") } };
				styles.historyKnobOther = new GUIStyle() { border = new RectOffset(6, 6, 6, 6), fixedHeight = 0, normal = new GUIStyleState() { background = EditorGUIUtility.FindTexture("sv_label_2") } };
				styles.headCommitTag = new GUIStyle("AssetLabel");
				styles.headCommitTag.normal.background = EditorGUIUtility.FindTexture("sv_label_1");
				styles.remoteCommitTag = new GUIStyle("AssetLabel");
				styles.remoteCommitTag.normal.background = EditorGUIUtility.FindTexture("sv_label_5");
				styles.otherCommitTag = new GUIStyle("AssetLabel");
				styles.otherCommitTag.normal.background = EditorGUIUtility.FindTexture("sv_label_2");
				styles.commitLineHead = new GUIStyle() {normal = new GUIStyleState() { background = blueTexture } };
				styles.commitLineRemote = new GUIStyle() { normal = new GUIStyleState() { background = orangeTexture } };
				styles.historyHelpBox = new GUIStyle(EditorStyles.helpBox) {richText = true,padding = new RectOffset(8,8,8,8),alignment = TextAnchor.MiddleLeft,contentOffset = new Vector2(24,-2)};
				styles.historyHelpBoxLabel = new GUIStyle("CN EntryWarn");
				styles.commitMessage = new GUIStyle("TL SelectionButton") {alignment = TextAnchor.UpperLeft,padding = new RectOffset(6,4,4,4),clipping = TextClipping.Clip};
				styles.avatar = new GUIStyle("ShurikenEffectBg") {contentOffset = Vector3.zero, alignment = TextAnchor.MiddleCenter,clipping = TextClipping.Clip,imagePosition = ImagePosition.ImageOnly};
				styles.avatarName = new GUIStyle("ShurikenEffectBg") {contentOffset = Vector3.zero, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, imagePosition = ImagePosition.TextOnly, fontSize = 28, fontStyle = FontStyle.Bold, normal = {textColor = Color.white}};
				GitProfilerProxy.EndSample();
			}
		}

		protected override void OnGitUpdate(GitRepoStatus status)
		{
			ThreadPool.QueueUserWorkItem(UpdateChachesThreaded, status);
		}

		private void UpdateChachesThreaded(object statusObj)
		{
			Monitor.Enter(commitCachesLock);
			try
			{
				GitRepoStatus status = (GitRepoStatus) statusObj;

				//update all branches
				cachedBranches = GitManager.Repository.Branches.Select(b => new BranchInfo(b)).ToArray();

				//update selected branch
				UpdateSelectedBranch();

				if (selectedBranch != null)
				{
					//update commits and limit them depending on settings
					cachedCommits = (GitManager.Settings.MaxCommits >= 0 ? selectedBranch.LoadBranch().Commits.Take(GitManager.Settings.MaxCommits) : selectedBranch.LoadBranch().Commits).Take(GitManager.Settings.MaxCommits).Select(c => new CommitInfo(c, cachedBranches.Where(b => b.Tip.Id == c.Id).ToArray())).ToArray();

					commitRects = new Rect[cachedCommits.Length];
				}

				hasConflicts = status.Any(s => s.Status == FileStatus.Conflicted);
				GitManager.ActionQueue.Enqueue(UpdateGitStatusIcon);
				GitManager.ActionQueue.Enqueue(Repaint);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				Monitor.Exit(commitCachesLock);
			}
		}

		private void UpdateGitStatusIcon()
		{
			titleContent.image = GitManager.GetGitStatusIcon();
		}

		private void UpdateSelectedBranch()
		{
			var tmpBranch = GitManager.Repository.Branches.FirstOrDefault(b => b.CanonicalName == selectedBranchName);
			if (tmpBranch != null)
			{
				selectedBranch = new BranchInfo(tmpBranch);

			}
			if (selectedBranch == null)
			{
				selectedBranch = new BranchInfo(GitManager.Repository.Head);
				selectedBranchName = selectedBranch.CanonicalName;
			}
		}

		protected override void OnInitialize()
		{
			lodingProfilePicturesToRemove = new List<string>();
			cachedProfilePicturesDictionary = new Dictionary<string, Texture2D>();
			loadingProfilePictures = new Dictionary<string, WWW>();
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
			if (loadingProfilePictures == null) loadingProfilePictures = new Dictionary<string, WWW>();
			if (cachedProfilePicturesDictionary == null) cachedProfilePicturesDictionary = new Dictionary<string, Texture2D>();
			if(lodingProfilePicturesToRemove == null) lodingProfilePicturesToRemove = new List<string>();

			if (loadingProfilePictures.Count > 0)
			{
				foreach (var profilePicture in loadingProfilePictures)
				{
					if (profilePicture.Value.isDone)
					{
						cachedProfilePicturesDictionary.Add(profilePicture.Key,profilePicture.Value.texture);
						serializedProfilePictures.RemoveAll(p => p.email == profilePicture.Key);
						serializedProfilePictures.Add(new ProfilePicture(profilePicture.Value.texture, profilePicture.Key));
						lodingProfilePicturesToRemove.Add(profilePicture.Key);
						profilePicture.Value.Dispose();
						Repaint();
					}
				}

				if (lodingProfilePicturesToRemove.Count > 0)
				{
					foreach (var key in lodingProfilePicturesToRemove)
					{
						loadingProfilePictures.Remove(key);
					}
					lodingProfilePicturesToRemove.Clear();
				}
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

		[UsedImplicitly]
		private void OnUnfocus()
		{
			GUI.FocusControl(null);
		}

		[UsedImplicitly]
		private void OnDestory()
		{
			/*foreach (var profilePicture in cachedProfilePicturesDictionary)
			{
				profilePicture.Value.Dispose();
			}*/
		}

		private const float helpBoxHeight = 38;
		private readonly float commitSpacing = EditorGUIUtility.singleLineHeight / 2;

		[UsedImplicitly]
		private void OnGUI()
		{
			CreateStyles();

			if (!GitManager.IsValidRepo)
			{
				InvalidRepoGUI();
				return;
			}

			if(GitManager.Repository == null || selectedBranch == null) return;
			RepositoryInformation repoInformation = GitManager.Repository.Info;
			DoToolbar(toolbarRect, repoInformation);
			EditorGUILayout.Space();

			DoHistoryScrollRect(scorllRect, repoInformation);
		}

		private void DoToolbar(Rect rect, RepositoryInformation info)
		{
			GitProfilerProxy.BeginSample("Git History Window Toolbar GUI",this);
			GUI.Box(rect, GUIContent.none, "Toolbar");
			Rect btRect = new Rect(rect.x, rect.y, 64, rect.height);
			GUIContent pushButtonContent = GitGUI.GetTempContent(EditorGUIUtility.FindTexture("CollabPush"),"Push", "Push local changes to a remote repository.");
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
			if (GUI.Button(btRect, pushButtonContent, "toolbarbutton"))
			{
				if (GitExternalManager.TakePush())
				{
					GitManager.MarkDirty();
				}
				else
				{
					ScriptableWizard.DisplayWizard<GitPushWizard>("Push", "Push").Init(selectedBranch.LoadBranch());
				}
			}
			btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
			GUI.enabled = !hasConflicts;
			if (GUI.Button(btRect, GitGUI.GetTempContent(EditorGUIUtility.IconContent("CollabPull").image, "Pull", hasConflicts ? "Must resolve conflicts before pulling" : "Pull changes from remote repository by fetching them and then merging them. This is the same as calling Fetch then Merge."), "toolbarbutton"))
			{
				Branch branch = selectedBranch.LoadBranch();
				if (GitExternalManager.TakePull())
				{
					AssetDatabase.Refresh();
					GitManager.MarkDirty();
				}
				else
				{
					ScriptableWizard.DisplayWizard<GitPullWizard>("Pull", "Pull").Init(branch);
				}
			}
			btRect = new Rect(btRect.x + 70, btRect.y, 64, btRect.height);
			if (GUI.Button(btRect, GitGUI.GetTempContent(EditorGUIUtility.IconContent("UniGit/GitFetch").image, "Fetch", "Get changes from remote repository but do not merge them."), "toolbarbutton"))
			{
				Branch branch = selectedBranch.LoadBranch();
				if (GitExternalManager.TakeFetch(branch.Remote.Name))
				{
					GitManager.MarkDirty();
				}
				else
				{
					ScriptableWizard.DisplayWizard<GitFetchWizard>("Fetch", "Fetch").Init(branch);
				}
			}
			btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
			if (GUI.Button(btRect, GitGUI.GetTempContent(EditorGUIUtility.IconContent("UniGit/GitMerge").image, "Merge", hasConflicts ? "Must Resolve conflict before merging" : "Merge fetched changes from remote repository. Changes from the latest fetch will be merged."), "toolbarbutton"))
			{
				if (GitExternalManager.TakeMerge())
				{
					GitManager.MarkDirty();
				}
				else
				{
					ScriptableWizard.DisplayWizard<GitMergeWizard>("Merge", "Merge");
				}
			}
			GUI.enabled = true;
			btRect = new Rect(rect.x + rect.width - 64, btRect.y, 64, btRect.height);
			if (GUI.Button(btRect, GitGUI.GetTempContent(string.IsNullOrEmpty(selectedBranchName) ? "Branch" : selectedBranch.FriendlyName), "ToolbarDropDown"))
			{
				GenericMenu selectBranchMenu = new GenericMenu();
				foreach (var branch in cachedBranches)
				{
					selectBranchMenu.AddItem(new GUIContent(branch.FriendlyName), false, (b) =>
					{
						selectedBranchName = (string)b;
						UpdateSelectedBranch();
					}, branch.FriendlyName);
				}
				selectBranchMenu.ShowAsContext();
			}
			btRect = new Rect(btRect.x - 64, btRect.y, 64, btRect.height);
			GUI.enabled = GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Switch) || (!selectedBranch.IsRemote && !selectedBranch.IsCurrentRepositoryHead);
			if (GUI.Button(btRect, GitGUI.GetTempContent(EditorGUIUtility.IconContent("UniGit/GitCheckout").image, "Switch", selectedBranch.IsRemote ? "Cannot switch to remote branches." : selectedBranch.IsCurrentRepositoryHead ? "This branch is the active one" : "Switch to another branch"), "toolbarbutton"))
			{
				if (GitExternalManager.TakeSwitch())
				{
					AssetDatabase.Refresh();
					GitManager.MarkDirty();
				}

				//todo Implement native switching
			}
			GUI.enabled = true;
			GitProfilerProxy.EndSample();
		}

		private void DoHistoryScrollRect(Rect rect, RepositoryInformation info)
		{
			
			Event current = Event.current;

			GUI.Box(new Rect(14, rect.y + 2, 2, rect.height), GUIContent.none, "AppToolbar");

			//behind,ahead and merge checking

			bool displayWarnningBox = DoWarningBoxValidate(info);

			//commit layout
			if (current.type == EventType.Layout)
			{
				GitProfilerProxy.BeginSample("Git History Window Scroll Rect GUI Layout", this);
				Rect lastCommitRect = new Rect(32, commitSpacing, Mathf.Max(rect.width - 24, 512) - 32, 0);

				if (displayWarnningBox)
				{
					warningBoxRect = new Rect(lastCommitRect.x, lastCommitRect.y, lastCommitRect.width, helpBoxHeight);
					lastCommitRect.y += helpBoxHeight + commitSpacing;
				}

				for (int i = 0; i < cachedCommits.Length; i++)
				{
					lastCommitRect = LayoutCommit(lastCommitRect, cachedCommits[i]);
					commitRects[i] = lastCommitRect;
				}

				historyScrollContentsRect = new Rect(0, 0, lastCommitRect.width + 32, lastCommitRect.y + lastCommitRect.height + commitSpacing*2);
				GitProfilerProxy.EndSample();
			}
			else
			{
				GitProfilerProxy.BeginSample("Git History Window Scroll Rect GUI Other", this);
				historyScroll = GUI.BeginScrollView(rect, historyScroll, historyScrollContentsRect);

				if (displayWarnningBox)
				{
					DoWarningBox(warningBoxRect, info);
				}

				for (int i = 0; i < cachedCommits.Length; i++)
				{
					DoCommit(commitRects[i], rect, cachedCommits[i]);
				}

				GUI.EndScrollView();
				GitProfilerProxy.EndSample();
			}
			
		}

		private Rect LayoutCommit(Rect lastCommitRect, CommitInfo commit)
		{
			bool isHeadOrRemote = commit.IsHead || commit.IsRemote;
			float commitHeight = 7 * EditorGUIUtility.singleLineHeight;
			if (isHeadOrRemote) commitHeight += EditorGUIUtility.singleLineHeight;
			Rect commitRect = new Rect(lastCommitRect.x, lastCommitRect.y + lastCommitRect.height + commitSpacing, lastCommitRect.width, commitHeight);
			return commitRect;
		}

		private void DoCommit(Rect rect,Rect scrollRect,CommitInfo commit)
		{
			GitProfilerProxy.BeginSample("Git History Window Commit GUI",this);
			Event current = Event.current;

			if (rect.y > scrollRect.height + historyScroll.y || rect.y + scrollRect.height < historyScroll.y)
			{
				return;
			}

			BranchInfo[] branches = commit.Branches;
			bool isHead = commit.IsHead;
			bool isRemote = commit.IsRemote;

			GUI.Box(new Rect(8, rect.y + 6, 16, 16), GUIContent.none, "AC LeftArrow");
			GUI.Box(new Rect(8, rect.y + 6, 16, 16), GUIContent.none, branches != null && branches.Length > 0 ? isHead ? styles.historyKnobHead : isRemote ? styles.historyKnobRemote : styles.historyKnobOther : styles.historyKnobNormal);

			float y = 8;
			float x = 12;
			if (isHead)
			{
				//GUI.Box(new Rect(commitRect.x + 4, commitRect.y, commitRect.width - 8, commitRect.height - 8), GUIContent.none, "TL SelectionButton PreDropGlow");
			}
			GUI.Box(rect, GUIContent.none, "RegionBg");
			if (isHead || isRemote)
			{
				GUI.Box(new Rect(rect.x + 4, rect.y, rect.width - 8, 5), GUIContent.none, isHead ? styles.commitLineHead : styles.commitLineRemote);
				y += 4;
			}

			if (GitManager.Settings.UseGavatar && Application.internetReachability != NetworkReachability.NotReachable)
			{
				Texture2D avatar = GetProfilePixture(commit.Committer.Email);
				if (avatar != null)
				{
					GUI.Box(new Rect(rect.x + x, rect.y + y, 32, 32), GitGUI.GetTempContent(avatar), styles.avatar);
				}
				else
				{
					GUI.Box(new Rect(rect.x + x, rect.y + y, 32, 32), GitManager.icons.loadingIconSmall, styles.avatar);
				}
			}
			else
			{
				UnityEngine.Random.InitState(commit.Committer.Name.GetHashCode());
				GUI.contentColor = UnityEngine.Random.ColorHSV(0, 1, 0.6f, 0.6f, 0.8f, 1, 1, 1);
				GUI.Box(new Rect(rect.x + x, rect.y + y, 32, 32), GitGUI.GetTempContent(commit.Committer.Name.Substring(0,1).ToUpper()), styles.avatarName);
				GUI.contentColor = Color.white;
			}
			
			
			//if (avatar != null)
			//{
				//GUI.DrawTexture(new Rect(rect.x + x, rect.y + y, 32, 32), avatar);
			//}
			x += 38;
			EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(commit.Committer.Name), EditorStyles.boldLabel);
			y += 16;
			EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(FormatRemainningTime(commit.Committer.When.UtcDateTime)));
			y += EditorGUIUtility.singleLineHeight + 3;
			int firstNewLineIndex = commit.Message.IndexOf(Environment.NewLine);
			EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x - 10, EditorGUIUtility.singleLineHeight + 4), GitGUI.GetTempContent(firstNewLineIndex > 0 ? commit.Message.Substring(0, firstNewLineIndex) : commit.Message), styles.commitMessage);
			y += 8;
			if (branches != null)
			{
				if (branches.Length > 0)
				{
					y += EditorGUIUtility.singleLineHeight;
				}
				foreach (var branch in branches)
				{
					GUIStyle style = branch.IsRemote ? styles.remoteCommitTag : branch.IsCurrentRepositoryHead ? styles.headCommitTag : styles.otherCommitTag;
					GUIContent labelContent = GitGUI.GetTempContent(branch.FriendlyName);
					float labelWidth = style.CalcSize(labelContent).x;
					GUI.Label(new Rect(rect.x + x, rect.y + y, labelWidth, EditorGUIUtility.singleLineHeight), labelContent, style);
					x += labelWidth + 4;
				}
			}

			x = 12;
			y += EditorGUIUtility.singleLineHeight * 1.5f;
			GUI.Box(new Rect(rect.x + x, rect.y + y, rect.width - x - x, EditorGUIUtility.singleLineHeight), GUIContent.none, "EyeDropperHorizontalLine");
			y += EditorGUIUtility.singleLineHeight / 3;
			EditorGUI.LabelField(new Rect(rect.x + x, rect.y + y, rect.width - x, EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(commit.Id.Sha));
			x += GUI.skin.label.CalcSize(GitGUI.GetTempContent(commit.Id.Sha)).x + 8;
			Rect buttonRect = new Rect(rect.x + x, rect.y + y, 64, EditorGUIUtility.singleLineHeight);
			x += 64;
			GUI.enabled = selectedBranch.IsCurrentRepositoryHead && !isHead;
			if (GUI.Button(buttonRect, GitGUI.GetTempContent("Reset", "Reset changes made up to this commit"), "minibuttonleft"))
			{
				if (GitExternalManager.TakeReset(GitManager.Repository.Lookup<Commit>(commit.Id)))
				{
					AssetDatabase.Refresh();
					GitManager.MarkDirty();
				}
				else
				{
					PopupWindow.Show(buttonRect, new ResetPopupWindow(GitManager.Repository.Lookup<Commit>(commit.Id)));
				}
			}
			GUI.enabled = true;
			buttonRect = new Rect(rect.x + x, rect.y + y, 64, EditorGUIUtility.singleLineHeight);
			if (GUI.Button(buttonRect, GitGUI.GetTempContent("Details"), "minibuttonright"))
			{
				PopupWindow.Show(buttonRect, new GitCommitDetailsWindow(GitManager.Repository.Lookup<Commit>(commit.Id)));
			}

			if (rect.Contains(current.mousePosition))
			{
				if (current.type == EventType.ContextClick)
				{
					//GenericMenu commitContexMenu = new GenericMenu();

					//commitContexMenu.ShowAsContext();
					current.Use();
				}
			}
			GitProfilerProxy.EndSample();
		}

		private void DoWarningBox(Rect rect, RepositoryInformation info)
		{
			int? behindBy = selectedBranch.TrackingDetails.BehindBy;
			GUIContent content = GUIContent.none;
			if (info.CurrentOperation == CurrentOperation.Merge)
			{
				content = GitGUI.GetTempContent(string.Format("Merging with remote branch in progress. You <b>must</b> do a merge commit before pushing."));
			}
			else if (behindBy != null && behindBy.Value > 0)
			{
				content = GitGUI.GetTempContent(string.Format("Branch <b>{0}</b> behind tracked branch <b>{1}</b>", selectedBranch.FriendlyName, selectedBranch.TrackedBranch));
			}

			GUI.Box(rect, content, styles.historyHelpBox);
			GUI.Box(rect, GUIContent.none, styles.historyHelpBoxLabel);
		}

		private bool DoWarningBoxValidate(RepositoryInformation info)
		{
			int? behindBy = selectedBranch.TrackingDetails.BehindBy;
			return (behindBy != null && behindBy.Value > 0) | info.CurrentOperation == CurrentOperation.Merge;
		}

		#region Menus
		#endregion

		#region Helper Methods
		private Texture2D GetProfilePixture(string email)
		{
			Texture2D tex;
			if (cachedProfilePicturesDictionary.TryGetValue(email, out tex))
			{
				if (tex != null)
				{
					return tex;
				}
				cachedProfilePicturesDictionary.Remove(email);
			}

			WWW texWww;
			if (loadingProfilePictures.TryGetValue(email,out texWww))
			{
				if (texWww.isDone)
				{
					tex = texWww.texture;
					cachedProfilePicturesDictionary.Add(email, tex);
					serializedProfilePictures.RemoveAll(p => p.email == email);
					serializedProfilePictures.Add(new ProfilePicture(tex,email));
					loadingProfilePictures.Remove(email);
					texWww.Dispose();
				}
				
				return tex;
			}

			string hash = HashEmailForGravatar(email.Trim());
			WWW loading = new WWW("https://www.gravatar.com/avatar/" + hash + "?s=32");
			loadingProfilePictures.Add(email, loading);
			return null;
		}

		public static string HashEmailForGravatar(string email)
		{
			// Create a new instance of the MD5CryptoServiceProvider object.  
			MD5 md5Hasher = MD5.Create();

			// Convert the input string to a byte array and compute the hash.  
			byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(email));

			// Create a new Stringbuilder to collect the bytes  
			// and create a string.  
			StringBuilder sBuilder = new StringBuilder();

			// Loop through each byte of the hashed data  
			// and format each one as a hexadecimal string.  
			for (int i = 0; i < data.Length; i++)
			{
				sBuilder.Append(data[i].ToString("x2"));
			}

			return sBuilder.ToString(); // Return the hexadecimal string. 
		}

		private string FormatRemainningTime(DateTime timeOffset)
		{
			const int SECOND = 1;
			const int MINUTE = 60 * SECOND;
			const int HOUR = 60 * MINUTE;
			const int DAY = 24 * HOUR;
			const int MONTH = 30 * DAY;

			var ts = new TimeSpan(DateTime.UtcNow.Ticks - timeOffset.Ticks);
			double delta = Math.Abs(ts.TotalSeconds);

			if (delta < 1 * MINUTE)
				return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago";

			if (delta < 2 * MINUTE)
				return "a minute ago";

			if (delta < 45 * MINUTE)
				return ts.Minutes + " minutes ago";

			if (delta < 90 * MINUTE)
				return "an hour ago";

			if (delta < 24 * HOUR)
				return ts.Hours + " hours ago";

			if (delta < 48 * HOUR)
				return "yesterday";

			if (delta < 30 * DAY)
				return ts.Days + " days ago";

			if (delta < 12 * MONTH)
			{
				int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
				return months <= 1 ? "one month ago" : months + " months ago";
			}
			else
			{
				int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
				return years <= 1 ? "one year ago" : years + " years ago";
			}

		}
		#endregion

		#region Invalid Repo GUI
		internal static void InvalidRepoGUI()
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Box(GitGUI.GetTempContent("Not a GIT Repository"), "NotificationBackground");
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.GetTempContent("Create"), "LargeButton", GUILayout.Height(32), GUILayout.Width(128)))
			{
				if (EditorUtility.DisplayDialog("Initialize Repository", "Are you sure you want to initialize a Repository for your project", "Yes", "Cancel"))
				{
					Repository.Init(Application.dataPath.Replace("/Assets", ""));
					TextAsset textAsset = EditorGUIUtility.Load("UniGit/gitignore.txt") as TextAsset;
					if (textAsset != null)
					{
						string textAssetPath = AssetDatabase.GetAssetPath(textAsset).Replace("Assets/", "");
						string newGitIgnoreFile = Path.Combine(Application.dataPath.Replace("Assets", "").Replace("Contents", ""), ".gitignore");
						if (!File.Exists(newGitIgnoreFile))
						{
							File.Copy(Path.Combine(Application.dataPath, textAssetPath), newGitIgnoreFile);
						}
						else
						{
#if UNITY_EDITOR
							Debug.Log("Git Ignore file already present");
#endif
						}
					}
					else
					{
						Debug.LogError("Missing default gitignore.txt in resources");
					}
					AssetDatabase.Refresh();
					AssetDatabase.SaveAssets();
					GitManager.Initlize();
					GitManager.MarkDirty();
					GUIUtility.ExitGUI();
					return;
				}
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}
		#endregion

		#region Popup Windows

		private abstract class CommitPopupWindow : PopupWindowContent
		{
			protected Commit commit;

			protected CommitPopupWindow(Commit commit)
			{
				this.commit = commit;
			}
		}

		private class ResetPopupWindow : CommitPopupWindow
		{
			private ResetMode resetMode = ResetMode.Mixed;
			private CheckoutOptions checkoutOptions = new CheckoutOptions();

			public override Vector2 GetWindowSize()
			{
				return new Vector2(256,128);
			}

			public ResetPopupWindow(Commit commit) : base(commit)
			{
				
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
						GitManager.Repository.Reset(resetMode,commit, checkoutOptions);
						GitManager.MarkDirty(true);
						editorWindow.Close();
						GitProfilerProxy.EndSample();
						AssetDatabase.Refresh();
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

			public Branch LoadBranch()
			{
				return GitManager.Repository.Branches[CanonicalName];
			}
		}

		public struct CommitInfo
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