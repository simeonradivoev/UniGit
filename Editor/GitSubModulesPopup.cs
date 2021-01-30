using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitSubModulesPopup : PopupWindowContent
	{
		private readonly UniGitData data;
		private readonly GitOverlay gitOverlay;
		private readonly GitManager gitManager;
		private string selectedModule;
		private Vector2 scroll;
		private readonly GUIStyle moduleStyle;

		[UniGitInject]
		public GitSubModulesPopup(UniGitData data,GitOverlay gitOverlay,GitManager gitManager)
		{
			this.data = data;
			this.gitOverlay = gitOverlay;
			this.gitManager = gitManager;
			moduleStyle = new GUIStyle("ProjectBrowserHeaderBgTop") {wordWrap = true,fixedHeight = 0,alignment = TextAnchor.MiddleLeft};
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(320,256);
		}

		public override void OnGUI(Rect rect)
		{
			if(Event.current.type == EventType.MouseMove) editorWindow.Repaint();
			EditorGUILayout.Space();
			scroll = EditorGUILayout.BeginScrollView(scroll);
			foreach (var entry in data.RepositoryStatus.SubModuleEntries)
			{
				var path = entry.Path;
				var elementTextHeight = EditorStyles.label.CalcHeight(GitGUI.GetTempContent(path), rect.width);
				var elementRect = GUILayoutUtility.GetRect(GUIContent.none, moduleStyle,GUILayout.MinHeight(elementTextHeight + EditorGUIUtility.singleLineHeight + 24));

				if (Event.current.type == EventType.Repaint)
				{
					moduleStyle.Draw(elementRect,GUIContent.none, 0);
				}

				var innerRect = new Rect(elementRect.x + 4,elementRect.y + 2,elementRect.width - 8,elementRect.height - 4);

				var nameRect = new Rect(innerRect.x,innerRect.y,innerRect.width - 24,elementTextHeight);
				GUI.Label(nameRect,GitGUI.GetTempContent(path));
				var hashRect = new Rect(innerRect.x,innerRect.y + elementTextHeight,innerRect.width - 24,EditorGUIUtility.singleLineHeight);
				GUI.Label(hashRect,GitGUI.GetTempContent(entry.WorkDirId),EditorStyles.miniLabel);
				var iconRect = new Rect(innerRect.x,innerRect.y + elementTextHeight + EditorGUIUtility.singleLineHeight,21,21);

				var initActive = false;
				if (entry.Status == SubmoduleStatus.InConfig)
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(GitGUI.Textures.WarrningIconSmall,"Module is only in config."),GitGUI.Styles.IconButton);
					initActive = true;
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}
				else if (entry.Status.HasFlag(SubmoduleStatus.WorkDirUninitialized))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(GitGUI.Textures.WarrningIconSmall,"Module is uninitialized."),GitGUI.Styles.IconButton);
					initActive = true;
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}
				else if (entry.Status.HasFlag(SubmoduleStatus.IndexAdded))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(gitOverlay.icons.addedIconSmall.image,"Sub Module is in index but not in head. Commit changes to add module to head."),GitGUI.Styles.IconButton);
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}
				else if (entry.Status.HasFlag(SubmoduleStatus.WorkDirModified))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(GitGUI.Textures.CollabPush,"Sub Module in index and in working directory don't match. Stage module or update it."),GitGUI.Styles.IconButton);
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}

				if (entry.Status.HasFlag(SubmoduleStatus.WorkDirFilesModified))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(gitOverlay.icons.modifiedIconSmall.image,"Sub Module has modified files."),GitGUI.Styles.IconButton);
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}

				if (entry.Status.HasFlag(SubmoduleStatus.WorkDirFilesUntracked))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(gitOverlay.icons.untrackedIconSmall.image,"Sub Module has untracked files."),GitGUI.Styles.IconButton);
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}

				if (entry.Status.HasFlag(SubmoduleStatus.WorkDirFilesIndexDirty))
				{
					GUI.Label(iconRect,GitGUI.GetTempContent(gitOverlay.icons.addedIconSmall.image,"Sub Module has added files to index."),GitGUI.Styles.IconButton);
					iconRect.x += EditorGUIUtility.singleLineHeight;
				}

				var switchContent = GitGUI.GetTempContent(GitGUI.Textures.OrbitTool, "Explore");
				var switchRect = new Rect(innerRect.x + innerRect.width - 24,innerRect.y,24,24);
				EditorGUIUtility.AddCursorRect(switchRect,MouseCursor.Link);
				if (GUI.Button(switchRect,switchContent,GitGUI.Styles.IconButton))
				{
					gitManager.SwitchToSubModule(entry.Path);
					editorWindow.Close();
				}
				var optionsRect = new Rect(innerRect.x + innerRect.width - 24,innerRect.y + 24,24,24);
				EditorGUIUtility.AddCursorRect(optionsRect,MouseCursor.Link);
				if (GUI.Button(optionsRect, GitGUI.IconContent("UnityEditor.SceneHierarchyWindow", string.Empty, "Options"),GitGUI.Styles.IconButton))
				{
					var menu = new GenericMenu();
					BuildOptions(menu, path, initActive);
					menu.DropDown(optionsRect);
				}

				if (Event.current.type == EventType.ContextClick && elementRect.Contains(Event.current.mousePosition))
				{
					var menu = new GenericMenu();
					BuildOptions(menu, path, initActive);
					menu.ShowAsContext();
				}
			}
			EditorGUILayout.EndScrollView();
            if (!gitManager.InSubModule) return;
            if (GUILayout.Button(GitGUI.GetTempContent("Return")))
            {
                gitManager.SwitchToMainRepository();
            }
        }

		private void BuildOptions(GenericMenu menu, string path,bool initActive)
		{
			if (initActive)
			{
				menu.AddItem(new GUIContent("Init/Init"),false, () =>
				{
					gitManager.Repository.Submodules.Init(path,false);
					gitManager.MarkDirty(true);
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Init/Init"));
			}
					
			menu.AddItem(new GUIContent("Init/Force"),false, () =>
			{
				gitManager.Repository.Submodules.Init(path,true);
				gitManager.MarkDirty(true);
			});
			menu.AddItem(new GUIContent("Update\\Info"),false, () =>
			{
				var window = UniGitLoader.GetWindow<GitSubModuleOptionsWizard>(true);
				window.Init(path);
			});
		}
	}
}
