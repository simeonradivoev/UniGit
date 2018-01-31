using System.IO;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitGeneralSettingsTab : GitSettingsTab, IHasCustomMenu
	{
		private readonly string[] autoRebaseOptions = { "never", "local", "remote", "always" };
		private readonly GitLfsManager lfsManager;
		private Vector2 scroll;

		[UniGitInject]
		public GitGeneralSettingsTab(GitManager gitManager, 
			GitSettingsWindow settingsWindow,
			GitLfsManager lfsManager,
			UniGitData data,
			GitSettingsJson gitSettings,
			GitCallbacks gitCallbacks,
			GitInitializer initializer) 
			: base(new GUIContent("General"), gitManager, settingsWindow,data,gitSettings,gitCallbacks,initializer)
		{
			this.lfsManager = lfsManager;
		}

		internal override void OnGUI(Rect rect, Event current)
		{
			scroll = EditorGUILayout.BeginScrollView(scroll);
			//todo cache general settings to reduce lookup
			GUILayout.BeginHorizontal(GitGUI.Styles.BigTitle, GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			GUILayout.Label(GitGUI.IconContent("SceneAsset Icon", "Unity Settings"), GUILayout.Height(18));
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.Contents.Help,GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.SettingsUniGitSettings);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			GUILayout.EndHorizontal();
			

			if (gitSettings != null)
			{
				bool save = false;
				bool updateGit = false;

				EditorGUI.BeginChangeCheck();
				gitSettings.AutoStage = EditorGUILayout.Toggle(GitGUI.GetTempContent("Auto Stage", "Auto stage changes for committing when an asset is modified"), gitSettings.AutoStage);
				gitSettings.AutoFetch = EditorGUILayout.Toggle(GitGUI.GetTempContent("Auto Fetch", "Auto fetch repository changes when possible. This will tell you about changes to the remote repository without having to pull. This only works with the Credentials Manager."), gitSettings.AutoFetch);
				save |= EditorGUI.EndChangeCheck();

				EditorGUI.BeginChangeCheck();
				gitSettings.ProjectStatusOverlayDepth = EditorGUILayout.DelayedIntField(GitGUI.GetTempContent("Project Status Overlay Depth", "The maximum depth at which overlays will be shown in the Project Window. This means that folders at levels higher than this will not be marked as changed. -1 indicates no limit"), gitSettings.ProjectStatusOverlayDepth);
				gitSettings.ShowEmptyFolders = EditorGUILayout.Toggle(GitGUI.GetTempContent("Show Empty Folders", "Show status for empty folder meta files and auto stage them, if 'Auto stage' option is enabled."), gitSettings.ShowEmptyFolders);
				GUIContent threadingContent = GitGUI.GetTempContent("Use Threading", "When Should Threading be used. In staging, unstaging or status retrival.");
				if ((gitSettings.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Stage) || gitSettings.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Unstage)) && lfsManager.Installed && lfsManager.CheckInitialized())
				{
					threadingContent.image = GitGUI.Textures.WarrningIconSmall;
					threadingContent.tooltip = "Threaded 'Stage' and 'Unstage' are disabled when Git LFS is enabled.";
				}
				gitSettings.Threading = (GitSettingsJson.ThreadingType)EditorGUILayout.EnumFlagsField(threadingContent, gitSettings.Threading);
				
				gitSettings.UseGavatar = EditorGUILayout.Toggle(GitGUI.GetTempContent("Use Gavatar", "Load Gavatars based on the committer's email address."), gitSettings.UseGavatar);
				gitSettings.MaxCommitTextAreaSize = EditorGUILayout.DelayedFloatField(GitGUI.GetTempContent("Max Commit Text Area Size", "The maximum height the commit text area can expand to."), gitSettings.MaxCommitTextAreaSize);
				GUIContent detectRenamesContent = GitGUI.GetTempContent("Detect Renames", "Detect Renames. This will make UniGit detect rename changes of files. Note that this feature is not always working as expected do the the modular updating and how Git itself works.");
				if (gitSettings.LazyMode)
				{
					detectRenamesContent.image = GitGUI.Textures.WarrningIconSmall;
					detectRenamesContent.tooltip = "Rename Detection will not work properly in preview with lazy update, altho they WILL still be detected by Git internally.";
				}
				gitSettings.DetectRenames = (GitSettingsJson.RenameTypeEnum)EditorGUILayout.EnumFlagsField(detectRenamesContent,gitSettings.DetectRenames);
				if (EditorGUI.EndChangeCheck())
				{
					save = true;
					updateGit = true;
				}

				EditorGUI.BeginChangeCheck();
				gitSettings.UseSimpleContextMenus = EditorGUILayout.Toggle(GitGUI.GetTempContent("Use Simple Context Menus", "Use Unity's default context menu on Diff window, instead of the UniGit one (with icons and animations)."), gitSettings.UseSimpleContextMenus);
				gitSettings.LazyMode = EditorGUILayout.Toggle(GitGUI.GetTempContent("Lazy Update Mode", "Without lazy mode, git status is updated on each assembly reload, leaving and entering play mode, staging, unstaging and any asset change."), gitSettings.LazyMode);
				gitSettings.TrackSystemFiles = EditorGUILayout.Toggle(GitGUI.GetTempContent("Track System Files", "Should files and folders be tracked that are outside the 'Assets' folder? This should definitely be used if lazy mode is on."), gitSettings.TrackSystemFiles);
				gitSettings.UseUnityConsole = EditorGUILayout.Toggle(GitGUI.GetTempContent("Use Unity's Console", "Show Info, Warning and Error messages in Unity's builtin console instead of the Git Log."), gitSettings.UseUnityConsole);
				gitSettings.AnimationType = (GitSettingsJson.AnimationTypeEnum)EditorGUILayout.EnumFlagsField(GitGUI.GetTempContent("Animation Types", "Which animation are allowed?"), gitSettings.AnimationType);
				gitSettings.CreateFoldersForDriftingMeta = EditorGUILayout.Toggle(GitGUI.GetTempContent("Create Missing Meta Folders","Create Missing Folders for .meta files. If a .meta folder is merged but the folder is not present, create it."), gitSettings.CreateFoldersForDriftingMeta);

				save |= EditorGUI.EndChangeCheck();

				if (save) gitSettings.MarkDirty();
				if(updateGit) gitManager.MarkDirty();
			}

			GUILayout.BeginHorizontal(GitGUI.Styles.BigTitle, GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			GUILayout.Label(GitGUI.IconContent("UnityEditor.SceneHierarchyWindow", "Git Settings"),GUILayout.Height(18));
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.Contents.Help,GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.SettingsGitSettings);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			GUILayout.EndHorizontal();
			

			using (Configuration c = Configuration.BuildFrom(gitManager.RepoPath))
			{
				EditorGUILayout.LabelField(GitGUI.GetTempContent("User"), EditorStyles.boldLabel);
				EditorGUI.indentLevel = 1;
				GitGUI.DoConfigStringField(c, GitGUI.GetTempContent("Name", "Your full name to be recorded in any newly created commits."), "user.name", "");
				GitGUI.DoConfigStringField(c, GitGUI.GetTempContent("Email", "Your email address to be recorded in any newly created commits."), "user.email", "");
				EditorGUI.indentLevel = 0;

				EditorGUILayout.LabelField(GitGUI.GetTempContent("Core"), EditorStyles.boldLabel);
				EditorGUI.indentLevel = 1;
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Auto LF line endings", "Setting this variable to 'true' is the same as setting the text attribute to 'auto' on all files and core.eol to 'crlf'. Set to true if you want to have CRLF line endings in your working directory and the repository has LF line endings. "), "core.autocrlf", true);
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Bare", "If true this repository is assumed to be bare and has no working directory associated with it. If this is the case a number of commands that require a working directory will be disabled, such as git-add[1] or git-merge[1]."), "core.bare", false);
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Symlinks", "If false, symbolic links are checked out as small plain files that contain the link text. git-update-index[1] and git-add[1] will not change the recorded type to regular file. Useful on filesystems like FAT that do not support symbolic links."), "core.symlinks", false);
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Ignore Case", "If true, this option enables various workarounds to enable Git to work better on filesystems that are not case sensitive, like FAT. For example, if a directory listing finds 'makefile' when Git expects 'Makefile', Git will assume it is really the same file, and continue to remember it as 'Makefile'."), "core.ignorecase", true);
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Logal Reference Updates", "Enable the reflog."), "core.logallrefupdates", true);
				GitGUI.DoConfigIntSlider(c, GitGUI.GetTempContent("Compression", "An integer -1..9, indicating a default compression level. -1 is the zlib default. 0 means no compression, and 1..9 are various speed/size tradeoffs, 9 being slowest."), -1, 9, "core.compression", -1);
				GitGUI.DoConfigStringField(c, GitGUI.GetTempContent("Big File Threshold", "Files larger than this size are stored deflated, without attempting delta compression. Storing large files without delta compression avoids excessive memory usage, at the slight expense of increased disk usage. Additionally files larger than this size are always treated as binary."), "core.bigFileThreshold", "512m");
				EditorGUI.indentLevel = 0;

				EditorGUILayout.LabelField(GitGUI.GetTempContent("Branch"), EditorStyles.boldLabel);
				EditorGUI.indentLevel = 1;
				GitGUI.DoConfigStringsField(c, GitGUI.GetTempContent("Auto Setup Rebase", "When a new branch is created with git branch or git checkout that tracks another branch, this variable tells Git to set up pull to rebase instead of merge."), "branch.autoSetupRebase", autoRebaseOptions, "never");
				EditorGUI.indentLevel = 0;

				EditorGUILayout.LabelField(GitGUI.GetTempContent("Diff"), EditorStyles.boldLabel);
				EditorGUI.indentLevel = 1;
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Renames", "Whether and how Git detects renames. If set to 'false', rename detection is disabled. If set to 'true', basic rename detection is enabled. "), "diff.renames", true);
				GitGUI.DoConfigIntField(c, GitGUI.GetTempContent("Rename Limit", "The number of files to consider when performing the copy/rename detection. Use -1 for unlimited"), "diff.renameLimit", -1);
				EditorGUI.indentLevel = 0;

				EditorGUILayout.LabelField(GitGUI.GetTempContent("HTTP"), EditorStyles.boldLabel);
				EditorGUI.indentLevel = 1;
				GitGUI.DoConfigToggle(c, GitGUI.GetTempContent("Verify SSL Crtificate", "Whether to verify the SSL certificate when fetching or pushing over HTTPS."), "http.sslVerify", true);
				string oldPath = c.GetValueOrDefault<string>("http.sslCAInfo");
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("SSL Certificate File", "File containing the certificates to verify the peer with when fetching or pushing over HTTPS."));
				if (GUILayout.Button(GitGUI.GetTempContent(oldPath), "TE ToolbarDropDown"))
				{
					EditorGUI.BeginChangeCheck();
					string newPath = EditorUtility.OpenFilePanelWithFilters("Certificate", string.IsNullOrEmpty(oldPath) ? Application.dataPath : Path.GetFullPath(oldPath), new string[] {"", "cer", "", "pom", "", "crt"});
					if (oldPath != newPath)
					{
						c.Set("http.sslCAInfo", newPath);
					}
				}

				EditorGUILayout.EndHorizontal();
				EditorGUI.indentLevel = 0;
			}

			GUILayout.BeginHorizontal(GitGUI.Styles.BigTitle,GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			GUILayout.Label(GitGUI.IconContent("IN LockButton on", "Git Ignore"), GUILayout.Height(18));
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.Contents.Help, GitGUI.Styles.IconButton))
			{
				GitLinks.GoTo(GitLinks.SettingsGitIgnoreHelp);
			}
			EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
			GUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("IN LockButton on", "Open Git Ignore File")))
			{
				OpenGitIgnore();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndScrollView();
		}

		private void OpenGitIgnore()
		{
			Application.OpenURL(UniGitPath.Combine(gitManager.RepoPath, ".gitignore"));
		}

		public void AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Open Git Ignore File"), false, OpenGitIgnore);
		}
	}
}