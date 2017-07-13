using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitBlameWizard : EditorWindow, IGitWindow
	{
		private const float commitLineHeight = 21;

		[SerializeField] private string blamePath;

		private BlameHunkCollection blameHunk;
		private string[] lines;
		private Vector2 linesScroll;
		private Vector2 hunksScroll;
		private string selectedCommit;
		private LogEntry[] commitLog;
		private bool isResizingCommits;
		private float commitsWindowHeight = 180;
		private string invalidMessage;
		private GitManager manager;

		private class Styles
		{
			public GUIStyle lineStyle;
			public GUIStyle lineStyleSelected;
			public GUIStyle lineNumStyle;
			public GUIStyle hunkStyle;
		}
		private Styles styles;

		internal void SetBlamePath(string blamePath)
		{
			this.blamePath = blamePath;
			CheckBlame();
			LoadFileLines();

			titleContent = new GUIContent("Git Blame: " + blamePath);
		}

		public void Construct(GitManager gitManager)
		{
			manager = gitManager;
		}

		private void OnEnable()
		{
			Construct(GitManager.Instance);
		}

		private void CheckBlame()
		{
			try
			{
				blameHunk = manager.Repository.Blame(blamePath);
				invalidMessage = null;
			}
			catch (Exception e)
			{
				invalidMessage = e.Message;
			}
		}

		private void LoadFileLines()
		{
			var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(blamePath);
			if (asset != null)
			{
				lines = asset.text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
			}
			else
			{
				lines = File.ReadAllLines(blamePath);
			}
			
			commitLog = manager.Repository.Commits.QueryBy(blamePath).Where(e => blameHunk.Any(h => h.FinalCommit.Sha == e.Commit.Sha)).ToArray();
		}

		private void InitGUI()
		{
			styles = new Styles();
			styles.lineStyle = new GUIStyle("CN StatusInfo") { padding = { left = 4 } };
			styles.lineStyleSelected = new GUIStyle(EditorStyles.label) { padding = { left = 4 }, normal = { background = ((GUIStyle)"ChannelStripAttenuationBar").normal.background } };
			styles.lineNumStyle = new GUIStyle(EditorStyles.label) { normal = { background = ((GUIStyle)"OL EntryBackEven").normal.background }, padding = { left = 4 } };
			styles.hunkStyle = new GUIStyle("CN EntryBackOdd") {alignment = TextAnchor.MiddleLeft};
		}

		private void Update()
		{
			if (!string.IsNullOrEmpty(blamePath) && blameHunk == null && manager.Repository != null)
			{
				CheckBlame();
				LoadFileLines();
				titleContent = new GUIContent("Git Blame: " + blamePath);
			}
		}

		protected bool OnGUI()
		{
			if (styles == null) InitGUI();

			if (!string.IsNullOrEmpty(invalidMessage))
			{
				EditorGUILayout.HelpBox(invalidMessage,MessageType.Error,true);
				if (GUILayout.Button(new GUIContent("Close")))
				{
					Close();
				}
				return false;
			}

			if(Event.current.type == EventType.MouseMove) Repaint();

			if (blameHunk == null)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label(GitGUI.IconContent("WaitSpin00"));
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.LabelField(new GUIContent("Checking Blame..."),EditorStyles.centeredGreyMiniLabel);
				return false;
			}
			else
			{
				Rect scrollRect = new Rect(0,0,position.width,position.height- commitsWindowHeight);
				Rect viewRect = new Rect(0,0,0,lines.Length * EditorGUIUtility.singleLineHeight);
				for (int i = 0; i < lines.Length; i++)
				{
					viewRect.width = Mathf.Max(viewRect.width, styles.lineStyle.CalcSize(new GUIContent(lines[i])).x);
				}
				viewRect.width += 32;
				linesScroll = GUI.BeginScrollView(scrollRect, linesScroll, viewRect);
				for (int i = 0; i < lines.Length; i++)
				{
					GUIContent lineContent = new GUIContent(lines[i]);
					Rect lineRect = new Rect(32, i * EditorGUIUtility.singleLineHeight, viewRect.width - 32, EditorGUIUtility.singleLineHeight);
					if (lineRect.y < linesScroll.y + scrollRect.height && lineRect.y + lineRect.height > linesScroll.y)
					{
						bool isFromHunk = blameHunk.Any(hunk => hunk.ContainsLine(i) && hunk.FinalCommit.Sha == selectedCommit);
						if (Event.current.type == EventType.Repaint)
						{
							styles.lineNumStyle.Draw(new Rect(0, i * EditorGUIUtility.singleLineHeight, 32, EditorGUIUtility.singleLineHeight), i.ToString(),false,false,false,false);
							styles.lineStyle.Draw(lineRect,lineContent,false,false,isFromHunk,false);
						}
						else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && lineRect.Contains(Event.current.mousePosition))
						{
							foreach (var hunk in blameHunk)
							{
								if (hunk.ContainsLine(i))
								{
									selectedCommit = hunk.FinalCommit.Sha;
									MoveToCommit(selectedCommit);
									Repaint();
									break;
								}
							}
						}
					}
				}
				GUI.EndScrollView();

				DoCommitsResize(new Rect(0, position.height - commitsWindowHeight, position.width, 4));

				int hunkCount = 0;
				float hunkMaxWidth = 0;
				foreach (var entry in commitLog)
				{
					Vector2 hunkSize = styles.hunkStyle.CalcSize(new GUIContent(entry.Commit.MessageShort));
					hunkMaxWidth = Mathf.Max(hunkMaxWidth, hunkSize.x);
					hunkCount++;
				}
				viewRect = new Rect(0, 0, hunkMaxWidth, hunkCount * commitLineHeight);
				scrollRect = new Rect(0, position.height - commitsWindowHeight + 4, position.width, commitsWindowHeight - 4);
				hunksScroll = GUI.BeginScrollView(scrollRect, hunksScroll, viewRect);

				int hunkId = 0;
				foreach (var entry in commitLog)
				{
					GUIContent commitContent = new GUIContent(entry.Commit.MessageShort);
					Rect commitRect = new Rect(0, hunkId * commitLineHeight, hunkMaxWidth, commitLineHeight);
					Rect commitInfoRect = new Rect(commitRect.x, commitRect.y, 24, commitRect.height);
					EditorGUIUtility.AddCursorRect(commitInfoRect,MouseCursor.Link);
					if (Event.current.type == EventType.Repaint)
					{
						int controlId = GUIUtility.GetControlID(commitContent, FocusType.Passive, commitRect);
						styles.hunkStyle.Draw(commitRect, commitContent, controlId, selectedCommit == entry.Commit.Sha);
						GUIStyle.none.Draw(commitInfoRect, GitGUI.IconContent("SubAssetCollapseButton"), false,false,false,false);
					}
					else if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
					{
						if (commitInfoRect.Contains(Event.current.mousePosition))
						{
							PopupWindow.Show(commitInfoRect,new CommitInfoPopupContent(entry.Commit));
							Event.current.Use();
						}
						else if (commitRect.Contains(Event.current.mousePosition))
						{
							selectedCommit = entry.Commit.Sha;
							MoveToLineFromCommit(selectedCommit);
							Repaint();
							Event.current.Use();
						}
					}
					hunkId++;
				}

				GUI.EndScrollView();
			}
			return false;
		}

		private void DoCommitsResize(Rect rect)
		{
			GUI.DrawTexture(rect,EditorGUIUtility.whiteTexture);
			EditorGUIUtility.AddCursorRect(rect,MouseCursor.ResizeVertical);
			if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
			{
				isResizingCommits = true;
			}

			if (isResizingCommits)
			{
				commitsWindowHeight = Mathf.Max(position.height - Event.current.mousePosition.y,64);
				Repaint();
			}

			if (Event.current.type == EventType.MouseUp)
				isResizingCommits = false;
		}

		private void MoveToLineFromCommit(string sha)
		{
			foreach (var hunk in blameHunk)
			{
				if (hunk.FinalCommit.Sha == sha)
				{
					linesScroll.y = hunk.FinalStartLineNumber * EditorGUIUtility.singleLineHeight;
					break;
				}
			}
		}

		private void MoveToCommit(string sha)
		{
			for (int j = 0; j < commitLog.Length; j++)
			{
				if (commitLog[j].Commit.Sha == sha)
				{
					hunksScroll.y = j * commitLineHeight;
					break;
				}
			}
		}

		public class CommitInfoPopupContent : PopupWindowContent
		{
			private Commit commit;

			public CommitInfoPopupContent(Commit commit)
			{
				this.commit = commit;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(360, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(commit.Message), 360) + EditorGUIUtility.singleLineHeight * 5);
			}

			public override void OnGUI(Rect rect)
			{
				EditorGUILayout.SelectableLabel(commit.Message, "AS TextArea");
				EditorGUILayout.TextField(new GUIContent("Author"), commit.Author.Name);
				EditorGUILayout.TextField(new GUIContent("Author Email"), commit.Author.Email);
				EditorGUILayout.TextField(new GUIContent("Date"), commit.Author.When.ToString());
				EditorGUILayout.TextField(new GUIContent("Revision (SHA)"), commit.Sha);
			}
		}
	}
}