using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitDiffInspector : EditorWindow, IHasCustomMenu
	{
		private const float AnimationDuration = 0.4f;

		[SerializeField] private float scrollVertical;
		[SerializeField] private float scrollHorizontalNormal;
		[SerializeField] private string path;
		[SerializeField] private string commitSha;

		private Styles styles;
		private int maxLines;
		private float totalLinesHeight;
		private float maxLineWidth;
		private float maxLineNumWidth;
		private List<ChangeSection> changeSections;
		private FileType selectedFile;
		private int selectedIndexFileLine = -1;
		private int selectedOtherFileLine = -1;
		private bool isBinary;
		private float otherFileWindowWidth = 0.5f;
		private bool isResizingFileWindow;
		private bool synataxHighlight;
		private GitManager gitManager;
		private GitOverlay gitOverlay;
		private ILogger logger;
		private GitAnimation gitAnimation;
		private double animationTime;
		private GitAnimation.GitTween animationTween;

		private class Styles
		{
			public GUIStyle NormalLine;
			public GUIStyle LineNum;
		}

		private const string keywords = @"\b(?<Keywords>public|private|protected|virtual|abstract|partial|static|namespace|class|struct|enum|using|void|foreach|internal|in|return|new|const|try|catch|finally|get|set|if|else|override)\b";
		private const string types = @"\b(?<Types>int|float|string|bool|double|Vector3|Vector2|Rect|Matrix4x4|Quaternion|var|object|byte)\b";
		private const string values = @"\b(?<Values>false|true|null)\b";
		private const string comments = @"\/\/.+?$|\/\*.+?\*\/";
		private const string strings = "\".+?\"";
		private const string methods = @"\w+\s*?(?=\()";
		private const string attributes = @"(?<=\[)[^][]*(?=\])";
		private const string defines = @"#.*";
		private const string numbers = @"[\d]+\.?f?";
		private UberRegex uberRegex;
		private CompareOptions compareOptions;
		private ExplicitPathsOptions explicitPathsOptions;
		private UnityEngine.Object asset;

        [UniGitInject]
		private void Construct(GitManager gitManager,GitOverlay gitOverlay,ILogger logger,GitAnimation gitAnimation)
		{
			this.gitManager = gitManager;
			this.gitOverlay = gitOverlay;
			this.logger = logger;
			this.gitAnimation = gitAnimation;
		}

		private void OnEnable()
		{
            GitWindows.AddWindow(this);
			compareOptions = new CompareOptions()
			{
				Algorithm = DiffAlgorithm.Myers,
				ContextLines = 0
			};

			explicitPathsOptions = new ExplicitPathsOptions();

			titleContent = new GUIContent("Diff Inspector", GitGUI.Textures.ZoomTool);
			uberRegex = new UberRegex(new ColoredRegex[]
			{
				new ColoredRegex("Comments",comments, "green"),
				new ColoredRegex("Strings",strings, "brown"),
				new ColoredRegex("Keywords",keywords, "blue"),
				new ColoredRegex("Types",types, "blue"),
				new ColoredRegex("Methods",methods, "teal"),
				new ColoredRegex("Attributes",attributes,"blue"), 
				new ColoredRegex("Defines",defines, "olive"),
				new ColoredRegex("Values",values, "brown"),
				new ColoredRegex("NUmbers",numbers,"brown"), 
			});
		}

	    private void OnDisable()
	    {
	        GitWindows.RemoveWindow(this);
	    }

		public void Init(string path)
		{
			InitStyles();
			this.path = path;
			synataxHighlight = path.EndsWith(".cs");
			BuildChangeSections(null);
			LoadAsset(path);
			ScrollToFirstChange();
			animationTween = null;
		}

		public void Init(string path,Commit commit)
		{
			InitStyles();
			this.path = path;
			synataxHighlight = path.EndsWith(".cs");
			commitSha = commit.Sha;
			BuildChangeSections(commit);
			LoadAsset(path);
			ScrollToFirstChange();
			animationTween = null;
		}

		public void Init(string path, Commit oldCommit,Commit newCommit)
		{
			InitStyles();
			this.path = path;
			synataxHighlight = path.EndsWith(".cs");
			commitSha = oldCommit.Sha;
			BuildChangeSections(oldCommit, newCommit);
			LoadAsset(path);
			ScrollToFirstChange();
			animationTween = null;
		}

		private void LoadAsset(string path)
		{
			asset = AssetDatabase.LoadMainAssetAtPath(path);
		}

		private string[] GetLines(Commit commit)
		{
			PatchEntryChanges changes = null;

			if (commit != null)
			{
				var patch = gitManager.Repository.Diff.Compare<Patch>(commit.Tree, DiffTargets.WorkingDirectory | DiffTargets.Index, new [] { path }, explicitPathsOptions, compareOptions);
				changes = patch[path];
			}
			else if(gitManager.Repository.Head != null && gitManager.Repository.Head.Tip != null)
			{
				var patch = gitManager.Repository.Diff.Compare<Patch>(gitManager.Repository.Head.Tip.Tree, DiffTargets.WorkingDirectory | DiffTargets.Index, new[] { path }, explicitPathsOptions, compareOptions);
				changes = patch[path];
			}


			if (changes != null)
			{
				isBinary = changes.IsBinaryComparison;
				return changes.Patch.Split('\n');
			}

			return new string[0];
		}

		private string[] GetLines(Commit oldTree, Commit newTree)
		{
			var patch = gitManager.Repository.Diff.Compare<Patch>(oldTree.Tree, newTree.Tree, new[] { path }, explicitPathsOptions,compareOptions);
			var changes = patch[path];
			isBinary = changes.IsBinaryComparison;
			return changes.Patch.Split(UniGitPath.NewLineChar);
		}

		private void BuildChangeSections(Commit commit)
		{
			int lastIndexFileLine = 0;
			Stream indexFileContent;
			string indexFilePath = UniGitPath.Combine(gitManager.RepoPath, path);
			if (File.Exists(indexFilePath))
			{
				indexFileContent = File.OpenRead(indexFilePath);
			}
			else
			{
				indexFileContent = new MemoryStream();
			}

			StreamReader indexFileReader = new StreamReader(indexFileContent);

			var lines = GetLines(commit);
			try
			{
				ProcessChanges(lines, indexFileReader, ref lastIndexFileLine);
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"There was a problem while loading changes");
				logger.LogException(e);
			}
			finally
			{
				indexFileContent.Dispose();
				indexFileReader.Dispose();
			}
		}

		private void BuildChangeSections(Commit oldCommit,Commit newCommit)
		{
			int lastIndexFileLine = 0;
			Stream indexFileContent = ((Blob)newCommit.Tree[path].Target).GetContentStream();
			StreamReader indexFileReader = new StreamReader(indexFileContent);

			var lines = GetLines(oldCommit, newCommit);
			try
			{
				ProcessChanges(lines, indexFileReader, ref lastIndexFileLine);
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"There was a problem while loading changes");
				logger.LogException(e);
			}
			finally
			{
				indexFileContent.Dispose();
				indexFileReader.Dispose();
			}
		}

		private void ProcessChanges(string[] lines, StreamReader indexFileReader,ref int lastIndexFileLine)
		{
			changeSections = new List<ChangeSection>();
			ChangeSection currentSection = null;
			IChangeBlob currentBlob = null;

			LineChangeType lastLineChangeType = LineChangeType.Normal;

			for (int i = 0; i < lines.Length - 1; i++)
			{
				if (lines[i].StartsWith("@@"))
				{
					var newSection = CreateSection(lines[i]);
					lastLineChangeType = LineChangeType.Normal;

					if (currentSection == null)
					{
						var prevNormalSection = new ChangeSection(false);
						prevNormalSection.addedStartLine = 1;
						prevNormalSection.removedStartLine = 1;
						var b = new NormalBlob();

						for (int j = 0; j < newSection.addedStartLine - 1; j++)
						{
							b.lines.Add(ColorizeLine(indexFileReader.ReadLine()));
							lastIndexFileLine++;
						}
						prevNormalSection.changeBlobs.Add(b);
						changeSections.Add(prevNormalSection);
					}
					else
					{
						var prevNormalSection = new ChangeSection(false);
						var b = new NormalBlob();

						int start = (currentSection.addedStartLine + currentSection.addedLineCount) - 1;
						int count = (newSection.addedStartLine - (currentSection.addedStartLine + currentSection.addedLineCount));

						prevNormalSection.addedStartLine = currentSection.addedStartLine + currentSection.addedLineCount;
						prevNormalSection.removedStartLine = currentSection.removedStartLine + currentSection.removedLineCount;

						while (lastIndexFileLine < start)
						{
							indexFileReader.ReadLine();
							lastIndexFileLine++;
						}

						for (int j = 0; j < count; j++)
						{
							b.lines.Add(ColorizeLine(indexFileReader.ReadLine()));
							lastIndexFileLine++;
						}

						prevNormalSection.addedLineCount = prevNormalSection.changeBlobs.Count;
						prevNormalSection.removedLineCount = prevNormalSection.changeBlobs.Count;

						prevNormalSection.changeBlobs.Add(b);
						changeSections.Add(prevNormalSection);
					}

					changeSections.Add(newSection);
					currentSection = newSection;
				}
				else if (currentSection != null)
				{
					LineChangeType lineChangeType = LineChangeType.Normal;
					if (lines[i].StartsWith("+"))
						lineChangeType = LineChangeType.Added;
					else if (lines[i].StartsWith("-"))
						lineChangeType = LineChangeType.Removed;

					if (lastLineChangeType == LineChangeType.Normal && lineChangeType != LineChangeType.Normal)
					{
						if (currentBlob != null) currentBlob.Finish();
						currentBlob = CreateNewBlob(lineChangeType, currentSection);
					}
					else if (lastLineChangeType != LineChangeType.Normal && lineChangeType == LineChangeType.Normal)
					{
						if (currentBlob != null) currentBlob.Finish();
						currentBlob = CreateNewBlob(lineChangeType, currentSection);
					}

					if (currentBlob != null)
					{
						currentBlob.AddLine(lineChangeType, ColorizeLine(lines[i]));
					}

					lastLineChangeType = lineChangeType;
				}
			}

			if (currentBlob != null)
			{
				currentBlob.Finish();
			}

			if (currentSection != null)
			{
				var lastNormalBlob = new NormalBlob();
				while (lastIndexFileLine < currentSection.addedStartLine + currentSection.addedLineCount - 1)
				{
					indexFileReader.ReadLine();
					lastIndexFileLine++;
				}

				while (!indexFileReader.EndOfStream)
				{
					lastNormalBlob.lines.Add(ColorizeLine(indexFileReader.ReadLine()));
					lastIndexFileLine++;
				}
				currentSection.changeBlobs.Add(lastNormalBlob);

				maxLines = changeSections.Sum(s => s.changeBlobs.Sum(b => b.Lines));
				totalLinesHeight = maxLines * EditorGUIUtility.singleLineHeight;
				foreach (var changeSection in changeSections)
				{
					foreach (var blob in changeSection.changeBlobs)
					{
						if (blob is NormalBlob)
						{
							var normalBlob = ((NormalBlob)blob);
							foreach (var line in normalBlob.lines)
							{
								maxLineWidth = Mathf.Max(maxLineWidth, GetLineWidth(line));
							}
						}
						else if (blob is AddRemoveBlob)
						{
							var addRemoveBlob = ((AddRemoveBlob)blob);
							if (addRemoveBlob.addedLines != null)
							{
								foreach (var addedLine in addRemoveBlob.addedLines)
								{
									maxLineWidth = Mathf.Max(maxLineWidth, GetLineWidth(addedLine));
								}
							}
							if (addRemoveBlob.removedLines != null)
							{
								foreach (var removedLine in addRemoveBlob.removedLines)
								{
									maxLineWidth = Mathf.Max(maxLineWidth, GetLineWidth(removedLine));
								}
							}
						}
					}
				}
				maxLineNumWidth = styles.LineNum.CalcSize(GitGUI.GetTempContent(maxLines.ToString())).x;
			}
		}

		private ChangeSection CreateSection(string line)
		{
			var addedMatch = Regex.Match(line, @"(?<lineStart>(?<=\+)\d+)(?:\,)?(?<lineCount>\d*)");
			var removedMatch = Regex.Match(line, @"(?<lineStart>(?<=\-)\d+)(?:\,)?(?<lineCount>\d*)");
			var newSection = new ChangeSection(true);
			if (addedMatch.Success)
			{
				int.TryParse(addedMatch.Groups["lineStart"].Value, out newSection.addedStartLine);
				if (!int.TryParse(addedMatch.Groups["lineCount"].Value, out newSection.addedLineCount))
				{
					newSection.addedLineCount = 1;
				}
			}
			if (removedMatch.Success)
			{
				int.TryParse(removedMatch.Groups["lineStart"].Value, out newSection.removedStartLine);
				if (!int.TryParse(removedMatch.Groups["lineCount"].Value, out newSection.removedLineCount))
				{
					newSection.removedLineCount = 1;
				}
				newSection.removedLineCount = Mathf.Max(newSection.removedLineCount, 0);
			}
			return newSection;
		}

		private IChangeBlob CreateNewBlob(LineChangeType type,ChangeSection section)
		{
			IChangeBlob blob;
			if (type == LineChangeType.Normal)
				blob = new NormalBlob();
			else
				blob = new AddRemoveBlob();
			section.changeBlobs.Add(blob);
			return blob;
		}

		private void ScrollToFirstChange()
		{
			if(changeSections == null) return;
			foreach (var changeSection in changeSections)
			{
				AddRemoveBlob firstChange = (AddRemoveBlob)changeSection.changeBlobs.FirstOrDefault(b => b is AddRemoveBlob);
				if (firstChange != null)
				{
					scrollVertical = Mathf.Max((changeSection.addedStartLine - 8), 0) * EditorGUIUtility.singleLineHeight;
					return;
				}
			}
		}

		private float GetLineWidth(string line)
		{
			if (string.IsNullOrEmpty(line)) return 0;
			return styles.NormalLine.CalcSize(GitGUI.GetTempContent(line)).x;
		}

		private string ColorizeLine(string line)
		{
			if(synataxHighlight)
				return uberRegex.Process(line);
			return line;
		}

		private void InitStyles()
		{
			if (styles == null)
			{
				styles = new Styles();
				styles.NormalLine = new GUIStyle(EditorStyles.label) {padding = {left = 6},normal = new GUIStyleState() { background = Texture2D.whiteTexture },onNormal = new GUIStyleState() { background = Texture2D.whiteTexture },richText = true};
				styles.LineNum = new GUIStyle(EditorStyles.label) { padding = { left = 6,right = 6},normal = { background = Texture2D.whiteTexture } };
			}
		}

		private void OnGUI()
		{
			InitStyles();

			if (isBinary)
			{
				EditorGUILayout.HelpBox("File Is Binary", MessageType.Warning);
				return;
			}

			if (changeSections == null)
			{
				if (gitManager.Repository != null)
				{
					var commit = string.IsNullOrEmpty(commitSha) ? null : gitManager.Repository.Lookup<Commit>(commitSha);
					BuildChangeSections(commit);
				}
				GitGUI.DrawLoading(new Rect(0,0, position.width, position.height), GitGUI.GetTempContent("Loading Changes"));
				Repaint();
				return;
			}

			if(changeSections.Count <= 0)
			{
				EditorGUILayout.HelpBox("No Difference",MessageType.Info);
				return;
			}

			if (animationTween == null)
			{
				animationTween = gitAnimation.StartManualAnimation(AnimationDuration,this,out animationTime,GitSettingsJson.AnimationTypeEnum.DiffInspector);
			}

			float toolbarHeight = EditorStyles.toolbar.fixedHeight;
			float difHeight = position.height - toolbarHeight;

			DrawToolbar();

			Rect resizeRect = new Rect(position.width * otherFileWindowWidth - 3, toolbarHeight,6, difHeight);
			Rect indexFileRect = new Rect(position.width * otherFileWindowWidth + (resizeRect.width/2), toolbarHeight, position.width * (1 - otherFileWindowWidth) - (resizeRect.width / 2), difHeight);
			Rect otherFileScrollRect = new Rect(0, toolbarHeight, position.width * otherFileWindowWidth - (resizeRect.width / 2), difHeight);

			gitAnimation.Update(animationTween,ref animationTime);
			float animTime = GitAnimation.ApplyEasing(animationTween.Percent);

			if (Event.current.type == EventType.MouseDown && otherFileScrollRect.Contains(Event.current.mousePosition))
			{
				selectedFile = FileType.OtherFile;
				Repaint();
			}
			else if (Event.current.type == EventType.MouseDown && indexFileRect.Contains(Event.current.mousePosition))
			{
				selectedFile = FileType.IndexFile;
				Repaint();
			}

			GUI.Box(resizeRect,GUIContent.none);

			if (Event.current.type == EventType.MouseUp)
				isResizingFileWindow = false;
			if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
				isResizingFileWindow = true;

			if (isResizingFileWindow)
			{
				otherFileWindowWidth = Mathf.Clamp(Event.current.mousePosition.x / position.width,0.1f,0.9f);
				EditorGUIUtility.AddCursorRect(position,MouseCursor.ResizeHorizontal);
				Repaint();	
			}
			else
			{
				EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);
			}

			GUI.Box(otherFileScrollRect,GUIContent.none, EditorStyles.textArea);
			GUI.Box(indexFileRect, GUIContent.none, EditorStyles.textArea);

			DrawBlobs(false, otherFileScrollRect, indexFileRect);
			DrawBlobs(true, indexFileRect, otherFileScrollRect);

			if(selectedFile == FileType.IndexFile)
				GUI.Box(indexFileRect,GUIContent.none, GitGUI.Styles.SelectionBoxGlow);
			else
				GUI.Box(otherFileScrollRect, GUIContent.none, GitGUI.Styles.SelectionBoxGlow);

			GUI.color = new Color(1,1,1,Mathf.Lerp(0,1,animTime));
			GUI.Box(new Rect(0,0,position.width,position.height - toolbarHeight), GUIContent.none);
			GUI.color = Color.white;
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			Rect goToLineRect = GUILayoutUtility.GetRect(GitGUI.GetTempContent("Go To Line"), EditorStyles.toolbarButton);
			if (GUI.Button(goToLineRect, GitGUI.GetTempContent("Go To Line"), EditorStyles.toolbarButton))
			{
				PopupWindow.Show(goToLineRect, new GoToLinePopup(GoToLine));
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Previous Change"), EditorStyles.toolbarButton))
			{
				GoToPreviousChange();
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Next Change"), EditorStyles.toolbarButton))
			{
				GoToNextChange();
			}
			GUILayout.FlexibleSpace();
			GUILayout.Label(GitGUI.GetTempContent(path));
			if (GitGUI.LinkButtonLayout(gitOverlay.icons.donateSmall, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.Donate);
			}
			if (GitGUI.LinkButtonLayout(GitGUI.Contents.Help, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.DiffInspectorHelp);
			}
			EditorGUILayout.EndHorizontal();
		}

		private void GoToPreviousChange()
		{
			for (int i = changeSections.Count-1; i >= 0; i--)
			{
				var changeSection = changeSections[i];
				if (changeSection.hasChanges && changeSection.GetStartLine(selectedFile) < GetSelectedLine(selectedFile))
				{
					GoToLine(changeSection.GetStartLine(selectedFile));
					return;
				}
			}

			var firstChangeSection = changeSections.Last(s => s.hasChanges);
			if (firstChangeSection != null)
			{
				GoToLine(firstChangeSection.GetStartLine(selectedFile));
			}
		}

		private void GoToNextChange()
		{
			foreach (var changeSection in changeSections)
			{
				if (changeSection.hasChanges && changeSection.GetStartLine(selectedFile) > GetSelectedLine(selectedFile))
				{
					GoToLine(changeSection.GetStartLine(selectedFile));
					return;
				}
			}

			var firstChangeSection = changeSections.First(s => s.hasChanges);
			if (firstChangeSection != null)
			{
				GoToLine(firstChangeSection.GetStartLine(selectedFile));
			}
		}

		private void GoToLine(int line)
		{
			SetSelectedLine(selectedFile, line);
			scrollVertical = GetLineHeight(selectedFile,line - 8);

			Repaint();
		}

		private int GetSelectedLine(FileType fileType)
		{
			if (fileType == FileType.IndexFile)
				return selectedIndexFileLine;
			return selectedOtherFileLine;
		}

		private void SetSelectedLine(FileType fileType,int line)
		{
			if (fileType == FileType.IndexFile)
				selectedIndexFileLine = line;
			else
				selectedOtherFileLine = line;
		}

		private float GetLineHeight(FileType fileType,int line)
		{
			int realLineCount = 0;
			foreach (var changeSection in changeSections)
			{
				int startLine = changeSection.GetStartLine(fileType);
				int lineCount = changeSection.GetLineCout(fileType);
				if (startLine + lineCount > line)
				{
					realLineCount += line - startLine;
					break;
				}
				else
				{
					realLineCount += changeSection.changeBlobs.Sum(b => b.Lines);
				}
			}

			return realLineCount * EditorGUIUtility.singleLineHeight;
		}

		private void DrawBlobs(bool showAdd,Rect rect,Rect otherRect)
		{
			float maxLineWidth = Mathf.Max(this.maxLineWidth, rect.width - maxLineNumWidth);
			float totalLineWidth = maxLineNumWidth + maxLineWidth + GUI.skin.verticalScrollbar.fixedWidth;
			float scrollMaxHorizontal = Mathf.Max(1, Mathf.Max(rect.width, totalLineWidth) - Mathf.Min(rect.width, totalLineWidth));
			bool isRapaint = Event.current.type == EventType.Repaint;

			float height = 0;
			Rect viewRect = new Rect(0,0, totalLineWidth, totalLinesHeight);
			Rect screenRect = new Rect(scrollHorizontalNormal * viewRect.width, scrollVertical, rect.width,rect.height);
			var newScroll = GUI.BeginScrollView(rect, new Vector2(scrollHorizontalNormal * scrollMaxHorizontal, scrollVertical), viewRect);
			scrollHorizontalNormal = Mathf.Clamp01(newScroll.x / scrollMaxHorizontal);
			scrollVertical = newScroll.y;

			if (Event.current.type == EventType.MouseDrag && (Event.current.button == 2 || (Event.current.button == 0 && Event.current.shift)))
			{
				Vector2 scrollDelta = new Vector2(Event.current.delta.x / scrollMaxHorizontal * 0.5f, Event.current.delta.y * 0.5f);
				scrollHorizontalNormal -= scrollDelta.x;
				scrollVertical -= scrollDelta.y;
				Repaint();
			}

			GUI.color = new Color(0, 0, 0, 0.2f);
			GUI.DrawTexture(new Rect((scrollHorizontalNormal * (totalLineWidth - rect.width)), 0, 1, totalLinesHeight), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(otherRect.width + (scrollHorizontalNormal * (totalLineWidth - otherRect.width)) - 1, 0, 1, totalLinesHeight), Texture2D.whiteTexture);
			GUI.color = Color.white;

			GUI.Box(new Rect(0,0,maxLineNumWidth,totalLinesHeight),GUIContent.none, GitGUI.Styles.GroupBox);

			foreach (var changeSection in changeSections)
			{
				int line = showAdd ? changeSection.addedStartLine : changeSection.removedStartLine;
				for (int b = 0; b < changeSection.changeBlobs.Count; b++)
				{
					var blob = changeSection.changeBlobs[b];
					if (blob is NormalBlob)
					{
						var normalBlob = ((NormalBlob) blob);
						for (int i = 0; i < normalBlob.lines.Count; i++)
						{
							Rect lineRect = new Rect(maxLineNumWidth, height, maxLineWidth, EditorGUIUtility.singleLineHeight);
							if (IsRectVisible(lineRect, screenRect))
							{
								Rect lineNumRect = new Rect(0, height, maxLineNumWidth, EditorGUIUtility.singleLineHeight);
								GUI.backgroundColor = new Color(0, 0, 0, 0);
								GUI.Label(lineNumRect, line.ToString(), styles.LineNum);
								GUI.Label(lineRect, normalBlob.lines[i], styles.NormalLine);
								GUI.backgroundColor = Color.white;

								DoLineEvents(lineRect, showAdd, line);
								
								if (showAdd ? line == selectedIndexFileLine : line == selectedOtherFileLine)
								{
									GUI.Box(new Rect(0, height, Mathf.Max(totalLineWidth, rect.width), EditorGUIUtility.singleLineHeight), GUIContent.none, GitGUI.Styles.LightmapEditorSelectedHighlight);
								}
							}
							line++;
							height += EditorGUIUtility.singleLineHeight;
						}
					}
					else if (blob is AddRemoveBlob)
					{
						var addRemoveBlob = (AddRemoveBlob)blob;
						for (int i = 0; i < addRemoveBlob.maxCount; i++)
						{
							List<string> lines = showAdd ? addRemoveBlob.addedLines : addRemoveBlob.removedLines;
							Rect lineRect = new Rect(maxLineNumWidth, height, Mathf.Max(maxLineWidth,rect.width - maxLineNumWidth), EditorGUIUtility.singleLineHeight);
							if (IsRectVisible(lineRect, screenRect))
							{
								if (i < lines.Count)
								{
									if (isRapaint)
									{
										Rect lineNumRect = new Rect(0, height, maxLineNumWidth, EditorGUIUtility.singleLineHeight);
										GUI.backgroundColor = showAdd ? new Color(0, 0.8f, 0, 0.2f) : new Color(1, 0, 0, 0.15f);
										styles.LineNum.Draw(lineNumRect,GitGUI.GetTempContent(line.ToString()),-1);
										GUI.backgroundColor = showAdd ? new Color(0, 1, 0, 0.1f) : new Color(1, 0, 0, 0.1f);
										styles.NormalLine.Draw(lineRect,GitGUI.GetTempContent(lines[i]),-1);
										GUI.backgroundColor = Color.white;
									}

									DoLineEvents(lineRect, showAdd, line);

									if (showAdd ? line == selectedIndexFileLine : line == selectedOtherFileLine)
									{
										if(isRapaint) (GitGUI.Styles.LightmapEditorSelectedHighlight).Draw(new Rect(0, height, Mathf.Max(totalLineWidth, rect.width), EditorGUIUtility.singleLineHeight), GUIContent.none,-1);
									}
								}
								else if(isRapaint)
								{
									GUI.backgroundColor = new Color(0, 0, 0, 0.05f);
									styles.NormalLine.Draw(new Rect(0, height, Mathf.Max(totalLineWidth, rect.width), EditorGUIUtility.singleLineHeight), GUIContent.none,-1);
									GUI.backgroundColor = Color.white;
								}
							}
							if(i < lines.Count)
								line++;
							height += EditorGUIUtility.singleLineHeight;
						}
					}
				}
			}
			GUI.EndScrollView();
		}

		private void DoLineEvents(Rect lineRect,bool showAdd, int line)
		{
			if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !Event.current.shift && lineRect.Contains(Event.current.mousePosition))
			{
				if (showAdd)
					selectedIndexFileLine = line;
				else
					selectedOtherFileLine = line;
			}
			else if (Event.current.type == EventType.ContextClick && lineRect.Contains(Event.current.mousePosition))
			{
				GenericMenu genericMenu = new GenericMenu();
				BuildLineContextMenu(genericMenu, line);
				genericMenu.ShowAsContext();
				Event.current.Use();
			}
		}

		private void BuildLineContextMenu(GenericMenu menu,int line)
		{
			if (asset != null)
			{
				menu.AddItem(new GUIContent("Open Line in Editor"), false, () => { AssetDatabase.OpenAsset(asset, line); });
			}
		}

		private bool IsRectVisible(Rect rect,Rect screenRect)
		{
			return rect.y <= screenRect.y + screenRect.height && rect.y + rect.height >= screenRect.y;
		}

		#region Custom Menu

		public void AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Donate"),false,()=>{GitLinks.GoTo(GitLinks.Donate);});
			menu.AddItem(new GUIContent("Help"),false,()=>{GitLinks.GoTo(GitLinks.DiffInspectorHelp);});
		}

		#endregion

		public class GoToLinePopup : PopupWindowContent
		{
			private int line;
			private readonly Action<int> gotoLineAction;

			public GoToLinePopup(Action<int> gotoLineAction)
			{
				this.gotoLineAction = gotoLineAction;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(256,64);
			}

			public override void OnGUI(Rect rect)
			{
				line = EditorGUILayout.IntField(GitGUI.GetTempContent("Line"), line);
				if (GUILayout.Button(GitGUI.GetTempContent("Go To Line")))
				{
					gotoLineAction.Invoke(line);
					editorWindow.Close();
				}
			}
		}

		public class ColoredRegex
		{
			public string Name;
			public string Pattern;
			public string Color;

			public ColoredRegex(string name,string pattern, string color)
			{
				Name = name;
				Pattern = pattern;
				Color = color;
			}

			public string Replace(string value)
			{
				return "<color=" + Color + ">" + value + "</color>";
			}
		}

		public class UberRegex
		{
			private readonly Regex regex;
			private readonly List<Func<string, string>> groupReplaces = new List<Func<string, string>>();

			public UberRegex(ColoredRegex[] regexes)
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < regexes.Length; i++)
				{
					stringBuilder.AppendFormat(i == 0 ? "(?<{0}>{1})" : "|(?<{0}>{1})", regexes[i].Name, regexes[i].Pattern);
					groupReplaces.Add(regexes[i].Replace);
				}
				regex = new Regex(stringBuilder.ToString(),RegexOptions.Compiled);
			}

			public string Process(string line)
			{
				return regex.Replace(line, Replace);
			}

			private string Replace(Match match)
			{
				for (int i = 0; i < groupReplaces.Count; i++)
				{
					var group = match.Groups[i + 1];
					if (group.Success)
					{
						return match.Result(groupReplaces[i].Invoke(group.Value));
					}
				}

				return match.Value;
			}
		}

		public enum FileType
		{
			IndexFile,
			OtherFile
		}

		public enum LineChangeType
		{
			Added,
			Removed,
			Normal
		}

		public class ChangeSection
		{
			public int addedStartLine;
			public int addedLineCount;
			public int removedStartLine;
			public int removedLineCount;
			public List<IChangeBlob> changeBlobs = new List<IChangeBlob>();
			public bool hasChanges;

			public int GetStartLine(FileType fileType)
			{
				if (fileType == FileType.IndexFile)
					return addedStartLine;
				return removedStartLine;
			}

			public int GetLineCout(FileType fileType)
			{
				if (fileType == FileType.IndexFile)
					return addedLineCount;
				return addedLineCount;
			}

			public ChangeSection(bool hasChanges)
			{
				this.hasChanges = hasChanges;
			}
		}

		public interface IChangeBlob
		{
			void AddLine(LineChangeType type, string line);
			void Finish();
			int Lines { get; }
		}

		public class NormalBlob : IChangeBlob
		{
			public readonly List<string> lines = new List<string>();

			public void AddLine(LineChangeType type, string line)
			{
				lines.Add(line);
			}

			public void Finish()
			{

			}

			public int Lines
			{
				get { return lines.Count; }
			}
		}

		public class AddRemoveBlob : IChangeBlob
		{
			public readonly List<string> removedLines = new List<string>();
			public readonly List<string> addedLines = new List<string>();
			public int maxCount;

			public void AddLine(LineChangeType type, string line)
			{
				if (type == LineChangeType.Added)
				{
					addedLines.Add(line);
				}
				else
				{
					removedLines.Add(line);
				}
			}

			public void Finish()
			{
				maxCount = Mathf.Max(removedLines.Count, addedLines.Count);
			}

			public int Lines
			{
				get { return maxCount; }
			}
		}
	}
}