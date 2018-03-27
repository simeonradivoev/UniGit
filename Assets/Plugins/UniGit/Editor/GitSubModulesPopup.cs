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
		private GUIStyle moduleStyle;

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
				string path = entry.Path;
				GUILayout.BeginHorizontal(moduleStyle,GUILayout.Height(32));
				bool initActive = false;
				if (entry.Status == SubmoduleStatus.InConfig)
				{
					GUILayout.Label(GitGUI.GetTempContent(GitGUI.Textures.WarrningIconSmall,"Module is only in config."),GitGUI.Styles.IconButton,GUILayout.Width(16),GUILayout.Height(16));
					initActive = true;
				}
				else if (entry.Status.HasFlag(SubmoduleStatus.WorkDirUninitialized))
				{
					GUILayout.Label(GitGUI.GetTempContent(GitGUI.Textures.WarrningIconSmall,"Module is uninitialized."),GitGUI.Styles.IconButton,GUILayout.Width(16),GUILayout.Height(16));
					initActive = true;
				}
				else if (entry.Status.HasFlag(SubmoduleStatus.IndexAdded))
				{
					GUILayout.Label(GitGUI.GetTempContent(gitOverlay.icons.addedIconSmall.image,"Sub Module is in index but not in head. Commit changes to add module to head."),GitGUI.Styles.IconButton,GUILayout.Width(16),GUILayout.Height(16));
				}
				EditorGUILayout.BeginVertical();
				GUILayout.Label(GitGUI.GetTempContent(entry.Path));
				EditorGUILayout.EndVertical();
				GUIContent switchContent = GitGUI.GetTempContent(GitGUI.Textures.OrbitTool, "Explore");
				Rect switchRect = GUILayoutUtility.GetRect(switchContent, GitGUI.Styles.IconButton);
				EditorGUIUtility.AddCursorRect(switchRect,MouseCursor.Link);
				if (GUI.Button(switchRect,switchContent,GitGUI.Styles.IconButton))
				{
					gitManager.SwitchToSubModule(entry.Path);
					editorWindow.Close();
				}
				GUILayout.EndHorizontal();
				Rect lastRect = GUILayoutUtility.GetLastRect();
				if (Event.current.type == EventType.ContextClick && lastRect.Contains(Event.current.mousePosition))
				{
					GenericMenu menu = new GenericMenu();

					if (initActive)
					{
						menu.AddItem(new GUIContent("Init/Init"),false, () =>
						{
							gitManager.Repository.Submodules.Init(path,false);
						});
					}
					else
					{
						menu.AddDisabledItem(new GUIContent("Init/Init"));
					}
					
					menu.AddItem(new GUIContent("Init/Force"),false, () =>
					{
						gitManager.Repository.Submodules.Init(path,true);
					});
					menu.AddItem(new GUIContent("Update\\Info"),false, () =>
					{
						var window = UniGitLoader.GetWindow<GitSubModuleOptionsWizard>(true);
						window.Init(path);
					});
					menu.ShowAsContext();
				}
			}
			EditorGUILayout.EndScrollView();
			if (gitManager.InSubModule)
			{
				if (GUILayout.Button(GitGUI.GetTempContent("Return")))
				{
					gitManager.SwitchToMainRepository();
				}
			}
		}
	}
}
