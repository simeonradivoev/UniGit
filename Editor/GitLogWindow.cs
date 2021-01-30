using System;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitLogWindow : EditorWindow
	{
		private class Styles
		{
			public GUIStyle entryInfoStyle;
			public GUIStyle consoleBox;
			public GUIStyle entryInfoStyleSmall;
			public GUIStyle entryStyleEven;
			public GUIStyle entryStyleOdd;
			public GUIStyle entryLog;
			public GUIStyle entryError;
			public GUIStyle entryWarning;
			public GUIContent logIconSmall;
			public GUIContent warningIconSmall;
			public GUIContent warningIconSmallInactive;
			public GUIContent errorIconSmall;
			public GUIContent errorIconSmallInactive;
		}
		[SerializeField] private Vector2 scroll;
		[SerializeField] private Vector2 infoScroll;
		[SerializeField] private bool showWarnings = true;
		[SerializeField] private bool showLog = true;
		[SerializeField] private bool showError = true;
		private GitLog gitLog;
		private GitCallbacks gitCallbacks;
		private Styles styles;
		private int selected;

		[UniGitInject]
		private void Construct(GitLog gitLog,GitCallbacks gitCallbacks)
		{
			this.gitLog = gitLog;
			this.gitCallbacks = gitCallbacks;
			gitCallbacks.OnLogEntry += OnLogEntry;
		}

		private void OnEnable()
		{
			titleContent = GitGUI.IconContent("UnityEditor.ConsoleWindow","GitLog");
			GitWindows.AddWindow(this);
		}

		private void OnDisable()
		{
			GitWindows.RemoveWindow(this);
		}

		private void OnLogEntry(GitLog.LogEntry entry)
		{
			Repaint();
		}

		private void InitStyles()
		{
			if (styles == null)
			{
				styles = new Styles()
				{
					consoleBox = "CN Box",
					entryInfoStyle = "CN EntryInfo",
					entryInfoStyleSmall = "CN EntryInfoSmall",
					entryStyleEven = "CN EntryBackEven",
					entryStyleOdd = "CN EntryBackOdd",
					entryError = "CN EntryErrorIcon",
					entryLog = "CN EntryInfoIcon",
					entryWarning = "CN EntryWarnIcon",
					logIconSmall = GitGUI.IconContent("console.infoicon.sml"),
					warningIconSmall = GitGUI.IconContent("console.warnicon.sml"),
					warningIconSmallInactive = GitGUI.IconContent("console.warnicon.inactive.sml"),
					errorIconSmall = GitGUI.IconContent("console.erroricon.sml"),
					errorIconSmallInactive = GitGUI.IconContent("console.erroricon.inactive.sml")
				};
			}
		}

		private void OnGUI()
		{
			InitStyles();

			int logCount = 0;
			int warningCount = 0;
			int errorCount = 0;

			int visibleCount = 0;

			for (int i = 0; i < gitLog.Count; i++)
			{
				var logType = gitLog[i].LogType;
				switch (logType)
				{
					case LogType.Error:
					case LogType.Exception:
					case LogType.Assert:
						errorCount++;
						if (showError) visibleCount++;
						break;
					case LogType.Warning:
						warningCount++;
						if (showWarnings) visibleCount++;
						break;
					case LogType.Log:
						logCount++;
						if (showLog) visibleCount++;
						break;
					default:
						throw new ArgumentOutOfRangeException("logType", logType, null);
				}
			}

			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button(GitGUI.GetTempContent("Clear"),EditorStyles.toolbarButton))
			{
				gitLog.Clear();
			}

			/*if (GUILayout.Button(GitGUI.GetTempContent("Test"),EditorStyles.toolbarButton))
			{
				var enumNames = Enum.GetNames(typeof(LogType));
				for (int i = 0; i < enumNames.Length; i++)
				{
					logger.Log((LogType)i,"Test " + enumNames[i]);
				}
			}*/

			GUILayout.FlexibleSpace();
			showLog = GUILayout.Toggle(showLog, GitGUI.GetTempContent(logCount.ToString(),styles.logIconSmall.image),EditorStyles.toolbarButton);
			showWarnings = GUILayout.Toggle(showWarnings, GitGUI.GetTempContent(warningCount.ToString(),warningCount > 0 ? styles.warningIconSmall.image : styles.warningIconSmallInactive.image),EditorStyles.toolbarButton);
			showError = GUILayout.Toggle(showError, GitGUI.GetTempContent(errorCount.ToString(),errorCount > 0 ? styles.errorIconSmall.image : styles.errorIconSmallInactive.image),EditorStyles.toolbarButton);
			EditorGUILayout.EndHorizontal();

			Rect toolbarRect = new Rect(0,0,position.width,EditorStyles.toolbarButton.fixedHeight);
			Rect logInfoRect = new Rect(0,position.height - 100,position.width,100);
			Rect scrollPos = new Rect(0,toolbarRect.height,position.width, position.height-toolbarRect.height-logInfoRect.height);

			float entryHeight = styles.entryInfoStyle.fixedHeight;
			
			Rect viewRect = new Rect(0,0,position.width,visibleCount * entryHeight);
			GUI.Box(scrollPos,GUIContent.none,styles.consoleBox);
			scroll = GUI.BeginScrollView(scrollPos,scroll,viewRect,GUIStyle.none, GUI.skin.verticalScrollbar);
			Event current = Event.current;
			float lastY = 0;
			for (int i = 0; i < gitLog.Count; i++)
			{
				var entry = gitLog[i];
				if (IsLogTypeShown(entry.LogType))
				{
					var entryStyle = i % 2 == 1 ? styles.entryStyleEven : styles.entryStyleOdd;
					var entryInfoIconStyle = GetLogTypeStyle(entry.LogType);
					Rect rect = new Rect(0,lastY,viewRect.width,entryHeight);
					if (rect.y <= scrollPos.height + scroll.y && rect.y + rect.height > scroll.y)
					{
						if (current.type == EventType.Repaint)
						{
							bool selectedFlag = i == selected;
							entryStyle.Draw(rect,GUIContent.none,false,selectedFlag,selectedFlag,false);

							styles.entryInfoStyle.Draw(new Rect(rect.x,rect.y,rect.width,EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(entry.Message),selectedFlag,selectedFlag,selectedFlag,selectedFlag);
							styles.entryInfoStyle.Draw(new Rect(rect.x,rect.y + 12,rect.width,EditorGUIUtility.singleLineHeight), GitGUI.GetTempContent(GitGUI.FormatRemainningTime(entry.Time)),selectedFlag,selectedFlag,selectedFlag,selectedFlag);

							entryInfoIconStyle.Draw(rect,GitGUI.GetTempContent(entry.Message),selectedFlag,selectedFlag,selectedFlag,selectedFlag);
						}
					}

					if (current.button == 0 && rect.Contains(current.mousePosition))
					{
						if (current.type == EventType.MouseUp)
						{
							selected = i;
							GUI.FocusControl(null);
							Repaint();
						}
						else if(current.type == EventType.MouseDown && current.clickCount == 2)
						{
							gitLog.OpenLine(entry.StackTrace,2);
						}
					}

					lastY += entryHeight;
				}
			}

			GUI.EndScrollView();
			
			GUI.Box(logInfoRect,GUIContent.none,styles.consoleBox);

			if (selected < gitLog.Count)
			{
				var selectedEntry =  gitLog[selected];
				string finalMsg = selectedEntry.Message + "\n" + selectedEntry.StackTrace;
				string[] lines = finalMsg.Split('\n');

				float maxLineWidth = 0;
				for (int i = 0; i < lines.Length; i++)
				{
					GUIContent content = GitGUI.GetTempContent(lines[i]);
					maxLineWidth = Mathf.Max(maxLineWidth, EditorStyles.label.CalcSize(content).x);
				}

				Rect logInfoViewRect = new Rect(0, 0, maxLineWidth, lines.Length * EditorGUIUtility.singleLineHeight);
				infoScroll = GUI.BeginScrollView(logInfoRect,infoScroll,logInfoViewRect);

				for (int i = 0; i < lines.Length; i++)
				{
					var line = lines[i];
					if (gitLog.CanOpenLine(line))
					{
						Rect buttonRect = new Rect(0,i * EditorGUIUtility.singleLineHeight + 1,21,21);
						if (GUI.Button(buttonRect,GitGUI.IconContent("TimelineContinue"),GitGUI.Styles.IconButton))
						{
							gitLog.OpenLine(line);
						}
						EditorGUIUtility.AddCursorRect(buttonRect,MouseCursor.Link);
						Rect labelPos = new Rect(16,i * EditorGUIUtility.singleLineHeight,viewRect.width - 16,EditorGUIUtility.singleLineHeight);
						EditorGUI.SelectableLabel(labelPos,line);
					}
					else
					{
						Rect labelPos = new Rect(0,i * EditorGUIUtility.singleLineHeight,viewRect.width,EditorGUIUtility.singleLineHeight);
						EditorGUI.SelectableLabel(labelPos,line);
					}
				}
				GUI.EndScrollView();
			}
		}

		private bool IsLogTypeShown(LogType type)
		{
			switch (type)
			{
				case LogType.Error:
				case LogType.Exception:
				case LogType.Assert:
					return showError;
				case LogType.Warning:
					return showWarnings;
				case LogType.Log:
					return showLog;
				default:
					throw new ArgumentOutOfRangeException("type", type, null);
			}
		}

		private GUIStyle GetLogTypeStyle(LogType type)
		{
			switch (type)
			{
				case LogType.Error:
				case LogType.Exception:
				case LogType.Assert:
					return styles.entryError;
				case LogType.Warning:
					return styles.entryWarning;
				case LogType.Log:
					return styles.entryLog;
				default:
					throw new ArgumentOutOfRangeException("type", type, null);
			}
		}

		private void OnDestroy()
		{
			gitCallbacks.OnLogEntry -= OnLogEntry;
		}
	}
}
