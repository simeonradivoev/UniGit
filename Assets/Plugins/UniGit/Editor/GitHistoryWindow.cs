using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;
using Tree = LibGit2Sharp.Tree;

namespace UniGit
{
	public class GitHistoryWindow : EditorWindow
	{
		private Rect toolbarRect { get { return new Rect(0,0,position.width, EditorGUIUtility.singleLineHeight);} }
		private Rect scorllRect { get { return new Rect(0,toolbarRect.height+2,position.width,position.height);} }

		private Dictionary<string, WWW> cachedProfilePicturesDictionary;
		private Styles styles;
		private Branch selectedBranch;
		private Branch[] cachedBranches;
		private Commit[] cachedCommits;
		[SerializeField] private Vector2 historyScroll;
		[SerializeField] private string selectedBranchName;

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
		}

		[MenuItem("Window/GIT History")]
		public static void CreateEditor()
		{
			GitHistoryWindow browser = GetWindow<GitHistoryWindow>();
			browser.titleContent = new GUIContent("Git History");
		}

		private void CreateStyles()
		{
			if (styles == null)
			{
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
				styles.commitMessage = new GUIStyle("TL SelectionButton") {alignment = TextAnchor.UpperLeft,padding = new RectOffset(4,4,4,4),clipping = TextClipping.Clip};
			}
			
		}

		private void OnFocus()
		{
			GUI.FocusControl(null);
			if (!GitManager.IsValidRepo) return;
			OnRepositoryUpdate(null);
		}

		private void OnUnfocus()
		{
			GUI.FocusControl(null);
		}

		private void OnEnable()
		{
			cachedProfilePicturesDictionary = new Dictionary<string, WWW>();
			GitManager.updateRepository -= OnRepositoryUpdate;
			GitManager.updateRepository += OnRepositoryUpdate;

			FetchChanges();
			titleContent.image = GitManager.GetGitStatusIcon();
			Repaint();
		}

		private void FetchChanges()
		{
			if (!GitManager.IsValidRepo) return;
			Remote remote = GitManager.Repository.Network.Remotes.FirstOrDefault();
			if (remote == null) return;
			GitManager.Repository.Network.Fetch(remote,new FetchOptions() {CredentialsProvider = FetchChangesCredentialHandler});
		}

		private Credentials FetchChangesCredentialHandler(string url, string user, SupportedCredentialTypes supported)
		{
			if (supported == SupportedCredentialTypes.UsernamePassword)
			{
				if (GitManager.GitCredentials != null)
				{
					var entry = GitManager.GitCredentials.GetEntry(url);
					if (entry != null)
					{
						return new UsernamePasswordCredentials()
						{
							Username = entry.Username,
							Password = entry.DecryptPassword()
						};
					}
				}
			}
			return new DefaultCredentials();
		}

		private void Init()
		{
			if (selectedBranch == null)
			{
				UpdateBranch();
			}

			if (cachedBranches == null)
			{
				UpdateBranches();
			}
		}

		private void OnRepositoryUpdate(RepositoryStatus status)
		{
			if (selectedBranch != null) UpdateBranch();
			if (cachedBranches != null) UpdateBranches();
			titleContent.image = GitManager.GetGitStatusIcon();
			Repaint();
		}

		private void UpdateBranches()
		{
			cachedBranches = GitManager.Repository.Branches.ToArray();
		}

		private void UpdateBranch()
		{
			selectedBranch = GitManager.Repository.Branches.FirstOrDefault(b => b.CanonicalName == selectedBranchName);
			if (selectedBranch == null)
			{
				selectedBranch = GitManager.Repository.Head;
				selectedBranchName = selectedBranch.CanonicalName;
			}
			cachedCommits = selectedBranch.Commits.ToArray();
		}

		private void OnDestory()
		{
			foreach (var profilePicture in cachedProfilePicturesDictionary)
			{
				profilePicture.Value.Dispose();
			}
		}

		private const float helpBoxHeight = 38;

		private void OnGUI()
		{
			CreateStyles();

			Event current = Event.current;

			if (!GitManager.IsValidRepo)
			{
				InvalidRepoGUI();
				return;
			}

			Init();

			RepositoryInformation repoInformation = GitManager.Repository.Info;
			GUI.Box(toolbarRect,GUIContent.none, "Toolbar");
			bool hasConflicts = GitManager.Repository.Index.Conflicts.Any();
			Rect btRect = new Rect(toolbarRect.x, toolbarRect.y, 64, toolbarRect.height);
			GUIContent pushButtonContent = new GUIContent("Push", EditorGUIUtility.FindTexture("CollabPush"),"Push local changes to a remote repository.");
			if (repoInformation.CurrentOperation == CurrentOperation.Merge)
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
				ScriptableWizard.DisplayWizard<GitPushWizard>("Push","Push").Init(selectedBranch);
			}
			btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
			GUI.enabled = !hasConflicts;
			GUIContent pullButtonContent = EditorGUIUtility.IconContent("CollabPull");
			pullButtonContent.tooltip = hasConflicts ? "Must resolve conflicts before pulling" : "Pull changes from remote repository by fetching them and then merging them. This is the same as calling Fetch then Merge.";
			pullButtonContent.text = "Pull";
			if (GUI.Button(btRect, pullButtonContent, "toolbarbutton"))
			{
				ScriptableWizard.DisplayWizard<GitPullWizard>("Pull","Pull").Init(selectedBranch);
			}
			btRect = new Rect(btRect.x + 70, btRect.y, 64, btRect.height);
			GUIContent fetchButtonContent = EditorGUIUtility.IconContent("UniGit/GitFetch");
			fetchButtonContent.tooltip = "Get changes from remote repository but do not merge them.";
			fetchButtonContent.text = "Fetch";
			if (GUI.Button(btRect, fetchButtonContent, "toolbarbutton"))
			{
				ScriptableWizard.DisplayWizard<GitFetchWizard>("Fetch","Fetch").Init(selectedBranch);
			}
			btRect = new Rect(btRect.x + 64, btRect.y, 64, btRect.height);
			GUIContent mergeButtonContent = EditorGUIUtility.IconContent("UniGit/GitMerge");
			mergeButtonContent.tooltip = hasConflicts ? "Must Resolve conflict before merging" : "Merge fetched changes from remote repository. Changes from the latest fetch will be merged.";
			mergeButtonContent.text = "Merge";
			if (GUI.Button(btRect, mergeButtonContent, "toolbarbutton"))
			{
				ScriptableWizard.DisplayWizard<GitMergeWizard>("Merge","Merge");
			}
			GUI.enabled = true;
			btRect = new Rect(toolbarRect.x + toolbarRect.width - 64, btRect.y, 64, btRect.height);
			if (GUI.Button(btRect, new GUIContent(string.IsNullOrEmpty(selectedBranchName) ? "Branch" : selectedBranch.FriendlyName), "ToolbarDropDown"))
			{
				GenericMenu selectBranchMenu = new GenericMenu();
				foreach (var branch in GitManager.Repository.Branches)
				{
					selectBranchMenu.AddItem(new GUIContent(branch.FriendlyName),false, (b) =>
					{
						selectedBranchName = (string)b;
						UpdateBranch();
					}, branch.CanonicalName);
				}
				selectBranchMenu.ShowAsContext();
			}
			btRect = new Rect(btRect.x - 64, btRect.y, 64, btRect.height);
			GUI.enabled = !selectedBranch.IsRemote && !selectedBranch.IsCurrentRepositoryHead;
			GUIContent checkoutButtonContent = EditorGUIUtility.IconContent("UniGit/GitCheckout");
			checkoutButtonContent.text = "Switch";
			checkoutButtonContent.tooltip = selectedBranch.IsRemote ? "Cannot switch to remote branches." : selectedBranch.IsCurrentRepositoryHead ? "This branch is the active one" : "Switch to another branch";
			if (GUI.Button(btRect, checkoutButtonContent, "toolbarbutton"))
			{

			}
			GUI.enabled = true;
			EditorGUILayout.Space();

			float commitSpacing = EditorGUIUtility.singleLineHeight / 2;
			float commitHeight = 8 * EditorGUIUtility.singleLineHeight;


			float historyContentsHeight = (commitHeight + commitSpacing) * cachedCommits.Length + commitSpacing;
			GUI.Box(new Rect(14, scorllRect.y + 2, 2, scorllRect.height), GUIContent.none, "AppToolbar");

			//behind,ahead and merge checking
			
			bool displayWarnningBox = DoWarningBoxValidate(repoInformation);
			if (displayWarnningBox) historyContentsHeight += helpBoxHeight + commitSpacing;

			Rect scrollContentRect = new Rect(0, 0, Mathf.Max(scorllRect.width - 24,512), historyContentsHeight + commitSpacing);
			historyScroll = GUI.BeginScrollView(scorllRect, historyScroll, scrollContentRect);

			float commitY = commitSpacing;

			if (displayWarnningBox)
			{
				Rect helpBoxRect = new Rect(32, commitY, scrollContentRect.width - 32, helpBoxHeight);
				DoWarningBox(helpBoxRect, repoInformation);
				commitY += helpBoxHeight + commitSpacing;
			}

			for (int i = 0; i < cachedCommits.Length; i++)
			{
				Rect commitRect = new Rect(32, commitY, scrollContentRect.width - 32, commitHeight);
				commitY += (commitHeight + commitSpacing);
				if (commitRect.y > scorllRect.height + historyScroll.y || commitRect.y + scorllRect.height < historyScroll.y)
				{
					continue;
				}

				Commit commit = cachedCommits[i];
				Branch[] branches = cachedBranches.Where(b => b.Tip == commit).ToArray();
				bool isHead = branches.Any(b => b.IsCurrentRepositoryHead);
				bool isRemote = branches.Any(b => b.IsRemote);

				GUI.Box(new Rect(8, commitRect.y + 6, 16, 16), GUIContent.none, "AC LeftArrow");
				GUI.Box(new Rect(8, commitRect.y + 6, 16, 16), GUIContent.none, branches.Length > 0 ? isHead ? styles.historyKnobHead : isRemote ? styles.historyKnobRemote : styles.historyKnobOther : styles.historyKnobNormal);

				float y = 8;
				float x = 12;
				if (isHead)
				{
					//GUI.Box(new Rect(commitRect.x + 4, commitRect.y, commitRect.width - 8, commitRect.height - 8), GUIContent.none, "TL SelectionButton PreDropGlow");
				}
				GUI.Box(commitRect, GUIContent.none, "RegionBg");
				if (isHead || isRemote)
				{
					GUI.Box(new Rect(commitRect.x + 4, commitRect.y, commitRect.width - 8, 5), GUIContent.none,isHead ? styles.commitLineHead : styles.commitLineRemote);
					y += 4;
				}

				Texture2D avatar = GetProfilePixture(commit.Committer.Email);
				GUI.Box(new Rect(commitRect.x + x, commitRect.y + y, 32, 32), GUIContent.none, "ShurikenEffectBg");
				if (avatar != null)
				{
					GUI.DrawTexture(new Rect(commitRect.x + x, commitRect.y + y, 32, 32), avatar);
				}
				x += 38;
				EditorGUI.LabelField(new Rect(commitRect.x + x, commitRect.y + y, commitRect.width - x, EditorGUIUtility.singleLineHeight), new GUIContent(commit.Committer.Name), EditorStyles.boldLabel);
				y += 16;
				EditorGUI.LabelField(new Rect(commitRect.x + x, commitRect.y + y, commitRect.width - x, EditorGUIUtility.singleLineHeight), new GUIContent(FormatRemainningTime(commit.Committer.When.UtcDateTime)));
				y += EditorGUIUtility.singleLineHeight + 3;
				EditorGUI.LabelField(new Rect(commitRect.x + x, commitRect.y + y, commitRect.width - x - 10, EditorGUIUtility.singleLineHeight+4), new GUIContent(commit.Message), styles.commitMessage);
				y += EditorGUIUtility.singleLineHeight + 8;
				foreach (var branch in branches)
				{
					GUIStyle style = branch.IsRemote ? styles.remoteCommitTag : branch.IsCurrentRepositoryHead ? styles.headCommitTag : styles.otherCommitTag;
					GUIContent labelContent = new GUIContent(branch.FriendlyName);
					float labelWidth = style.CalcSize(labelContent).x;
					GUI.Label(new Rect(commitRect.x + x, commitRect.y + y, labelWidth, EditorGUIUtility.singleLineHeight), labelContent, style);
					x += labelWidth + 4;
				}

				x = 12;
				y += EditorGUIUtility.singleLineHeight * 1.5f;
				GUI.Box(new Rect(commitRect.x + x, commitRect.y + y, commitRect.width - x - x, EditorGUIUtility.singleLineHeight), GUIContent.none, "EyeDropperHorizontalLine");
				y += EditorGUIUtility.singleLineHeight / 3;
				EditorGUI.LabelField(new Rect(commitRect.x + x, commitRect.y + y, commitRect.width - x, EditorGUIUtility.singleLineHeight), new GUIContent(commit.Id.Sha));
				x += GUI.skin.label.CalcSize(new GUIContent(commit.Id.Sha)).x + 8;
				Rect buttonRect = new Rect(commitRect.x + x, commitRect.y + y, 64, EditorGUIUtility.singleLineHeight);
				x += 64;
				GUI.enabled = selectedBranch.IsCurrentRepositoryHead && !isHead;
				if (GUI.Button(buttonRect, new GUIContent("Reset","Reset changes made up to this commit"), "minibuttonleft"))
				{
					PopupWindow.Show(buttonRect, new ResetPopupWindow(commit));
				}
				GUI.enabled = true;
				buttonRect = new Rect(commitRect.x + x, commitRect.y + y, 64, EditorGUIUtility.singleLineHeight);
				if (GUI.Button(buttonRect, new GUIContent("Inspect"), "minibuttonright"))
				{
					PopupWindow.Show(buttonRect, new InspectPopupWindow(commit));
				}

				if (commitRect.Contains(current.mousePosition))
				{
					if (current.type == EventType.ContextClick)
					{
						//GenericMenu commitContexMenu = new GenericMenu();
						
						//commitContexMenu.ShowAsContext();
						current.Use();
					}
				}
			}

			/*if (commitCount - 8 > 0)
				{
					EditorGUILayout.BeginVertical("sv_iconselector_labelselection");
					EditorGUILayout.LabelField(new GUIContent(string.Format("{0} more commits", commitCount - 8)), EditorStyles.boldLabel);
					EditorGUILayout.EndVertical();
				}*/

			GUILayout.FlexibleSpace();

			GUI.EndScrollView();
			//Rect scrollRect = GUILayoutUtility.GetLastRect();
			//GUI.Box(new Rect(scrollRect.x - 12,scrollRect.y,2,scrollRect.height),GUIContent.none, "AppToolbar");
		}

		private void DoWarningBox(Rect rect, RepositoryInformation info)
		{
			int? behindBy = selectedBranch.TrackingDetails.BehindBy;
			GUIContent content = GUIContent.none;
			if (info.CurrentOperation == CurrentOperation.Merge)
			{
				content = new GUIContent(string.Format("Merging with remote branch in progress. You <b>must</b> do a merge commit before pushing."));
			}
			else if (behindBy != null && behindBy.Value > 0)
			{
				content = new GUIContent(string.Format("Branch <b>{0}</b> behind tracked branch <b>{1}</b>", selectedBranch.FriendlyName, selectedBranch.TrackedBranch.FriendlyName));
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
			WWW tex;
			if (cachedProfilePicturesDictionary.TryGetValue(email, out tex))
			{
				return tex.isDone ? tex.texture : null;
			}

			string hash = HashEmailForGravatar(email.Trim());
			WWW loading = new WWW("https://www.gravatar.com/avatar/" + hash + "?s=32");
			cachedProfilePicturesDictionary.Add(email, loading);
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
			GUILayout.Box(new GUIContent("Not a GIT Repository"), "NotificationBackground");
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Create"), "LargeButton", GUILayout.Height(32), GUILayout.Width(128)))
			{
				if (EditorUtility.DisplayDialog("Initialize Repository", "Are you sure you want to initialize a Repository for your project", "Yes", "Cancel"))
				{
					Repository.Init(Application.dataPath.Replace("/Assets", ""));
					TextAsset textAsset = EditorGUIUtility.Load("gitignore.txt") as TextAsset;
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
							Debug.Log("Git Ignore file already present");
						}
					}
					else
					{
						Debug.LogError("Missing default gitignore.txt in resources");
					}

					GitManager.Initlize();
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
				resetMode = (ResetMode)EditorGUILayout.EnumPopup(new GUIContent("Reset Type"), resetMode);
				GUIContent infoContent = new GUIContent();
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
						GitManager.Repository.Reset(resetMode,commit, checkoutOptions);
						GitManager.Update(true);
						editorWindow.Close();
					}
				}
				EditorGUILayout.Space();
			}
		}

		private class InspectPopupWindow : CommitPopupWindow
		{
			private TreeChanges changes;
			private Tree commitTree;
			private Vector2 scroll;

			public InspectPopupWindow(Commit commit) : base(commit)
			{
				commitTree = commit.Tree;
				Commit parentCommit = commit.Parents.FirstOrDefault();

				if (parentCommit != null)
				{
					changes = GitManager.Repository.Diff.Compare<TreeChanges>(parentCommit.Tree, commitTree);
				}
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(512,256);
			}

			public override void OnGUI(Rect rect)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				EditorGUILayout.Space();
				if (changes != null)
				{
					foreach (var change in changes)
					{
						//EditorGUILayout.BeginHorizontal();
						//GUILayout.Label(change.Status.ToString(), "AssetLabel");
						EditorGUILayout.BeginHorizontal("ProjectBrowserHeaderBgTop");
						GUILayout.Label(new GUIContent(GitManager.GetDiffTypeIcon(change.Status,true)),GUILayout.Width(16));
						GUILayout.Label(new GUIContent("(" + change.Status + ")"), "AboutWIndowLicenseLabel");
						GUILayout.Space(8);
						foreach (var chunk in change.Path.Split('\\'))
						{
							GUILayout.Label(new GUIContent(chunk), "GUIEditor.BreadcrumbMid");
						}
						//GUILayout.Label(new GUIContent(" (" + change.Status + ") " + change.Path));
						EditorGUILayout.EndHorizontal();
						Rect r = GUILayoutUtility.GetLastRect();
						if (Event.current.type == EventType.ContextClick)
						{
							GenericMenu menu = new GenericMenu();
							if (commit.Parents.Count() == 1)
							{
								menu.AddItem(new GUIContent("Difference with previous commit"), false, () =>
								{
									Commit parent = commit.Parents.Single();
									GitManager.ShowDiff(change.Path, parent,commit);
								});
							}
							else
							{
								menu.AddDisabledItem(new GUIContent(new GUIContent("Difference with previous commit")));
							}
							menu.AddItem(new GUIContent("Difference with HEAD"), false, () =>
							{
								GitManager.ShowDiff(change.Path,commit,GitManager.Repository.Head.Tip);
							});
							menu.ShowAsContext();
						}
						//EditorGUILayout.EndHorizontal();
					}
				}
				else
				{
					DrawTreeEntry(commitTree,0);
				}
				EditorGUILayout.Space();
				EditorGUILayout.EndScrollView();
			}

			private void DrawTreeEntry(Tree tree, int depth)
			{
				foreach (var file in tree)
				{
					if (file.TargetType == TreeEntryTargetType.Tree)
					{
						EditorGUI.indentLevel = depth;
						EditorGUILayout.LabelField(Path.GetFileName(file.Path));
						DrawTreeEntry(file.Target as Tree, depth + 1);
					}
					else if(!file.Path.EndsWith(".meta"))
					{
						EditorGUI.indentLevel = depth;
						EditorGUILayout.LabelField(file.Path);
					}
				}
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
	}
}