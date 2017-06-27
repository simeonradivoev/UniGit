using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitSettingsWindow : GitUpdatableWindow
	{
		private RemoteCollection remotes;
		private BranchCollection branches;
		private RemoteEntry[] remoteCacheList = new RemoteEntry[0];
		private string[] remoteNames;
		private Rect addRepositoryButtonRect;
		[SerializeField] private SettingTabEnum tab;

		[MenuItem("Window/GIT Settings")]
		public static void CreateEditor()
		{
			GetWindow(true);
		}

		public static GitSettingsWindow GetWindow(bool focus)
		{
			return GetWindow<GitSettingsWindow>("Git Settings",focus);
		}

		protected override void OnFocus()
		{
			base.OnFocus();
			LoseFocus();
			if (!GitManager.IsValidRepo) return;
			OnGitUpdate(null,null);
		}

		[UsedImplicitly]
		private void OnUnfocus()
		{
			LoseFocus();
		}

		protected override void OnInitialize()
		{
			OnGitUpdate(null,null);
		}

		protected override void OnRepositoryLoad(Repository repository)
		{
			Repaint();
		}

		protected override void OnGitUpdate(GitRepoStatus status,string[] paths)
		{
			if(GitManager.Repository == null) return;
			UpdateRemotes();
			UpdateBranches();
		}

		protected override void OnEditorUpdate()
		{
			
		}

		private void UpdateBranches()
		{
			branches = GitManager.Repository.Branches;
		}

		private void UpdateRemotes()
		{
			remotes = GitManager.Repository.Network.Remotes;
			remoteCacheList = remotes.Select(r => new RemoteEntry(r)).ToArray();
			remoteNames = remotes.Select(r => r.Name).ToArray();
		}

		[UsedImplicitly]
		private void OnGUI()
		{
			if (!GitManager.IsValidRepo)
			{
				GitHistoryWindow.InvalidRepoGUI();
				return;
			}

			Event current = Event.current;

			EditorGUILayout.BeginHorizontal("Toolbar");
			EditorGUI.BeginChangeCheck();
			bool value = GUILayout.Toggle(tab == SettingTabEnum.General, GitGUI.GetTempContent("General"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.General;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Externals, GitGUI.GetTempContent("Externals","External Programs Helpers"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Externals;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Remotes, GitGUI.GetTempContent("Remotes","Remote Repositories"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Remotes;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Branches, GitGUI.GetTempContent("Branches"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Branches;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.LFS, GitGUI.GetTempContent("LFS","Git Large File Storage (beta)"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.LFS;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Security, GitGUI.GetTempContent("Security"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Security;
			}
			if(EditorGUI.EndChangeCheck()) LoseFocus();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(EditorGUIUtility.IconContent("_Help"), "IconButton"))
			{
				Application.OpenURL("https://github.com/simeonradivoev/UniGit/wiki/Setup#configuration");
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			if (GitManager.Repository != null)
			{
				switch (tab)
				{
					case SettingTabEnum.Remotes:
						DoRemotes(current);
						break;
					case SettingTabEnum.Externals:
						DoExternals(current);
						break;
					case SettingTabEnum.Branches:
						DoBranches(current);
						break;
					case SettingTabEnum.LFS:
						DoLFS(current);
						break;
					case SettingTabEnum.Security:
						DoSecurity(current);
						break;
					default:
						DoGeneral(current);
						break;
				}
			}

			EditorGUILayout.Space();

			if (current.type == EventType.MouseDown)
			{
				LoseFocus();
			}
		}

		#region General

		private readonly string[] autoRebaseOptions = {"never", "local", "remote", "always"};

		private void DoGeneral(Event current)
		{
			GitSettingsJson settings = GitManager.Settings;

			//todo cache general settings to reduce lookup
			GUILayout.Box(new GUIContent("Unity Settings"), "IN BigTitle",GUILayout.ExpandWidth(true));

			if (settings != null)
			{
				bool save = false;

				EditorGUI.BeginChangeCheck();
				settings.AutoStage = EditorGUILayout.Toggle(GitGUI.GetTempContent("Auto Stage","Auto stage changes for committing when an asset is modified"),settings.AutoStage);
				settings.AutoFetch = EditorGUILayout.Toggle(GitGUI.GetTempContent("Auto Fetch","Auto fetch repository changes when possible. This will tell you about changes to the remote repository without having to pull. This only works with the Credentials Manager."),settings.AutoFetch);
				save = EditorGUI.EndChangeCheck();
				EditorGUI.BeginChangeCheck();
				settings.MaxCommits = EditorGUILayout.DelayedIntField(GitGUI.GetTempContent("Max Commits","The Maximum amount of commits show in the Git History Window. Use -1 for infinite commits."),settings.MaxCommits);
				settings.ProjectStatusOverlayDepth = EditorGUILayout.DelayedIntField(GitGUI.GetTempContent("Project Status Overlay Depth", "The maximum depth at which overlays will be shown in the Project Window. This means that folders at levels higher than this will not be marked as changed. -1 indicates no limit"),settings.ProjectStatusOverlayDepth);
				settings.ShowEmptyFolders = EditorGUILayout.Toggle(new GUIContent("Show Empty Folders","Show status for empty folder meta files and auto stage them, if 'Auto stage' option is enabled."),settings.ShowEmptyFolders);
				settings.GitStatusMultithreaded = EditorGUILayout.Toggle(GitGUI.GetTempContent("Git Status Multithreaded","Should Git status retrieval be multithreaded."),settings.GitStatusMultithreaded);
				settings.UseGavatar = EditorGUILayout.Toggle(GitGUI.GetTempContent("Use Gavatar","Load Gavatars based on the committer's email address."),settings.UseGavatar);
				settings.MaxCommitTextAreaSize = EditorGUILayout.DelayedFloatField(GitGUI.GetTempContent("Max Commit Text Area Size","The maximum height the commit text area can expand to."),settings.MaxCommitTextAreaSize);
				settings.DetectRenames = EditorGUILayout.Toggle(GitGUI.GetTempContent("Detect Renames","Detect Renames. This will make UniGit detect rename changes of files. Note that this feature is not always working as expected do the the modular updating and how Git itself works."),settings.DetectRenames);
				settings.UseSimpleContextMenus = EditorGUILayout.Toggle(GitGUI.GetTempContent("Use Simple Context Menus", "Use Unity's default context menu on Diff window, instead of the UniGit one (with icons)"), settings.UseSimpleContextMenus);
				if (EditorGUI.EndChangeCheck())
				{
					save = true;
					GitManager.MarkDirty();
				}

				if (save)
				{
					settings.MarkDirty();
				}
			}

			GUILayout.Box(GitGUI.GetTempContent("Git Settings"), "IN BigTitle",GUILayout.ExpandWidth(true));

			EditorGUILayout.LabelField(GitGUI.GetTempContent("User"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringField(GitGUI.GetTempContent("Name", "Your full name to be recorded in any newly created commits."), "user.name", "");
			DoConfigStringField(GitGUI.GetTempContent("Email", "Your email address to be recorded in any newly created commits."), "user.email", "");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Core"),EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1; 
			DoConfigToggle(GitGUI.GetTempContent("Auto LF line endings", "Setting this variable to 'true' is the same as setting the text attribute to 'auto' on all files and core.eol to 'crlf'. Set to true if you want to have CRLF line endings in your working directory and the repository has LF line endings. "), "core.autocrlf", true);
			DoConfigToggle(GitGUI.GetTempContent("Bare", "If true this repository is assumed to be bare and has no working directory associated with it. If this is the case a number of commands that require a working directory will be disabled, such as git-add[1] or git-merge[1]."), "core.bare",false);
			DoConfigToggle(GitGUI.GetTempContent("Symlinks", "If false, symbolic links are checked out as small plain files that contain the link text. git-update-index[1] and git-add[1] will not change the recorded type to regular file. Useful on filesystems like FAT that do not support symbolic links."), "core.symlinks",false);
			DoConfigToggle(GitGUI.GetTempContent("Ignore Case", "If true, this option enables various workarounds to enable Git to work better on filesystems that are not case sensitive, like FAT. For example, if a directory listing finds 'makefile' when Git expects 'Makefile', Git will assume it is really the same file, and continue to remember it as 'Makefile'."), "core.ignorecase",true);
			DoConfigToggle(GitGUI.GetTempContent("Logal Reference Updates", "Enable the reflog."), "core.logallrefupdates",true);
			DoConfigIntSlider(GitGUI.GetTempContent("Compression", "An integer -1..9, indicating a default compression level. -1 is the zlib default. 0 means no compression, and 1..9 are various speed/size tradeoffs, 9 being slowest."), -1,9, "core.compression",-1);
			DoConfigStringField(GitGUI.GetTempContent("Big File Threshold", "Files larger than this size are stored deflated, without attempting delta compression. Storing large files without delta compression avoids excessive memory usage, at the slight expense of increased disk usage. Additionally files larger than this size are always treated as binary."), "core.bigFileThreshold","512m");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Branch"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringsField(GitGUI.GetTempContent("Auto Setup Rebase", "When a new branch is created with git branch or git checkout that tracks another branch, this variable tells Git to set up pull to rebase instead of merge."), "branch.autoSetupRebase", autoRebaseOptions, "never");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Diff"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(GitGUI.GetTempContent("Renames", "Whether and how Git detects renames. If set to 'false', rename detection is disabled. If set to 'true', basic rename detection is enabled. "), "diff.renames", true);
			DoConfigIntField(GitGUI.GetTempContent("Rename Limit", "The number of files to consider when performing the copy/rename detection. Use -1 for unlimited"), "diff.renameLimit", -1);
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("HTTP"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(GitGUI.GetTempContent("Verify SSL Crtificate", "Whether to verify the SSL certificate when fetching or pushing over HTTPS."), "http.sslVerify",true);
			string oldPath = GitManager.Repository.Config.GetValueOrDefault<string>("http.sslCAInfo");
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("SSL Certificate File", "File containing the certificates to verify the peer with when fetching or pushing over HTTPS."));
			if (GUILayout.Button(GitGUI.GetTempContent(oldPath), "TE ToolbarDropDown"))
			{
				EditorGUI.BeginChangeCheck();
				string newPath = EditorUtility.OpenFilePanelWithFilters("Certificate", string.IsNullOrEmpty(oldPath) ? Application.dataPath : Path.GetFullPath(oldPath), new string[] { "","cer","","pom","","crt" });
				if (oldPath != newPath)
				{
					GitManager.Repository.Config.Set("http.sslCAInfo", newPath);
				}
			}
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel = 0;

			GUILayout.Box(new GUIContent("Git Ignore"), "IN BigTitle", GUILayout.ExpandWidth(true));

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Open Git Ignore File"))
			{
				Application.OpenURL(Path.Combine(GitManager.RepoPath,".gitignore"));
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		private void DoConfigStringsField(GUIContent content, string key,string[] options, string def)
		{
			string oldValue = GitManager.Repository.Config.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(content);
			GUI.SetNextControlName(key + " Config Popup");
			int newValueIndex = EditorGUILayout.Popup(Array.IndexOf(options, oldValue), options);
			string newValue;
			if (newValueIndex >= 0 && newValueIndex < options.Length)
			{
				newValue = options[newValueIndex];
			}
			else
			{
				newValue = def;
			}
			EditorGUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				GitManager.Repository.Config.Set(key, newValue);
			}
		}

		private void DoConfigStringField(GUIContent content, string key, string def)
		{
			string oldValue = GitManager.Repository.Config.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config String");
			string newValue = EditorGUILayout.DelayedTextField(content, oldValue);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				GitManager.Repository.Config.Set(key, newValue);
			}
		}

		private void DoConfigIntField(GUIContent content, string key,int def)
		{
			int oldValue = GitManager.Repository.Config.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config Int");
			int newValue = EditorGUILayout.DelayedIntField(content, oldValue);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				GitManager.Repository.Config.Set(key, newValue);
			}
		}

		private void DoConfigIntSlider(GUIContent content,int min,int max, string key,int def)
		{
			int oldValue = GitManager.Repository.Config.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config Int");
			int newValue = EditorGUILayout.IntSlider(content, oldValue,min,max);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				GitManager.Repository.Config.Set(key, newValue);
			}
		}

		private void DoConfigToggle(GUIContent content,string key,bool def)
		{
			bool oldValue = GitManager.Repository.Config.GetValueOrDefault(key, def);
			GUI.SetNextControlName(key + " Config Toggle");
			bool newValue = EditorGUILayout.Toggle(content, oldValue);
			if (oldValue != newValue)
			{
				GitManager.Repository.Config.Set(key,newValue);
			}
		}

		#endregion

		private Rect addCredentialsRect;

		private void DoSecurity(Event current)
		{
			EditorGUILayout.BeginHorizontal();
			GitSettingsJson settings = GitManager.Settings;
			if (settings != null)
			{
				EditorGUI.BeginChangeCheck();
				int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("Credentials Manager", "The name of the External program to use"), GitCredentialsManager.SelectedAdapterIndex, GitCredentialsManager.AdapterNames);
				settings.CredentialsManager = newSelectedIndex >= 0 && newSelectedIndex < GitCredentialsManager.AdapterIds.Length ? GitCredentialsManager.AdapterIds[newSelectedIndex] : "";
				if (EditorGUI.EndChangeCheck())
				{
					GitCredentialsManager.SetSelectedAdapter(newSelectedIndex);
					settings.MarkDirty();
				}
				GUI.enabled = newSelectedIndex >= 0;
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Remove"),"minibutton",GUILayout.Width(64)))
			{
				if (EditorUtility.DisplayDialog("Remove Credentials Manager", "This will remove all stored passwords in the Manager. Usernames and URLs will be kept in Unity", "Remove", "Cancel"))
				{
					GitCredentialsManager.SetSelectedAdapter(-1);
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			if (GitCredentialsManager.GitCredentials == null)
			{
				EditorGUILayout.HelpBox("No Git Credentials",MessageType.Warning);
				return;
			}

			foreach (var gitCredential in GitCredentialsManager.GitCredentials)
			{
				GUILayout.Label(GitGUI.GetTempContent(gitCredential.Name), "ShurikenModuleTitle");
				EditorGUILayout.Space();
				EditorGUILayout.BeginVertical("ShurikenModuleBg");
				EditorGUI.BeginChangeCheck();
				GUI.SetNextControlName(gitCredential.URL + " Credential Name");
				gitCredential.Name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), gitCredential.Name);
				GUI.enabled = false;
				GUI.SetNextControlName(gitCredential.URL + " Credential URL");
				EditorGUILayout.TextField(GitGUI.GetTempContent("URL"), gitCredential.URL);
				GUI.enabled = true;
				EditorGUILayout.Space();
				GUILayout.Label(GUIContent.none, "sv_iconselector_sep");
				EditorGUILayout.Space();
				bool newIsToken = gitCredential.IsToken;
				newIsToken = EditorGUILayout.Toggle(GitGUI.GetTempContent("Is Token", "Are credentials used as a token, like in GitHub."), newIsToken);
				if (newIsToken != gitCredential.IsToken)
				{
					gitCredential.IsToken = newIsToken;
					if (gitCredential.IsToken)
					{
						GitCredentialsManager.ClearCredentialPassword(gitCredential.URL);
					}
				}

				if (gitCredential.IsToken)
				{
					GUI.SetNextControlName(gitCredential.URL + " Credential Token");
					string newUsername = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Token"), gitCredential.Username);
					if (newUsername != gitCredential.Username)
					{
						GitCredentialsManager.SetNewUsername(gitCredential.URL, newUsername);
					}
				}
				else
				{
					GUI.SetNextControlName(gitCredential.URL + " Credential username");
					string newUsername = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Username"), gitCredential.Username);
					if (newUsername != gitCredential.Username)
					{
						GitCredentialsManager.SetNewUsername(gitCredential.URL, newUsername);
					}
					GUI.SetNextControlName(gitCredential.URL + " Credential New Password");
					gitCredential.NewPassword = EditorGUILayout.PasswordField(GitGUI.GetTempContent("New Password"), gitCredential.NewPassword);
					if (GitCredentialsManager.IsAdapterSelected && GUI.GetNameOfFocusedControl() == gitCredential.URL + " Credential New Password")
					{
						EditorGUILayout.HelpBox("Password will be set in the current credentials manager: " + GitCredentialsManager.SelectedAdapterName, MessageType.Info);
					}

					if (!gitCredential.HasPassword && !GitCredentialsManager.IsAdapterSelected)
					{
						EditorGUILayout.HelpBox("Credential has no set Password", MessageType.Warning);
					}
				}
				
				if (EditorGUI.EndChangeCheck())
				{
					GitCredentialsManager.GitCredentials.MarkDirty();
				}

				GUI.enabled = !string.IsNullOrEmpty(gitCredential.NewPassword);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(GitGUI.GetTempContent("Set Password"), "minibuttonleft"))
				{
					GitCredentialsManager.SetNewPassword(gitCredential.URL,gitCredential.Username,gitCredential.NewPassword);
					gitCredential.NewPassword = "";
					GitCredentialsManager.GitCredentials.MarkDirty();
					GUI.FocusControl("");
					EditorUtility.DisplayDialog("Password Changed", "Password successfully changed", "Ok");
				}
				GUI.enabled = gitCredential.HasPassword;
				if (GUILayout.Button(GitGUI.GetTempContent("Clear Password"), "minibuttonmid"))
				{
					GitCredentialsManager.ClearCredentialPassword(gitCredential.URL);
					GitCredentialsManager.GitCredentials.MarkDirty();
				}
				GUI.enabled = true;
				if (GUILayout.Button(GitGUI.GetTempContent("Save"), "minibuttonmid"))
				{
					GitCredentialsManager.GitCredentials.MarkDirty();
				}
				if (GUILayout.Button(GitGUI.GetTempContent("Remove"), "minibuttonright"))
				{
					GitCredentialsManager.GitCredentials.MarkDirty();
					GUIUtility.ExitGUI();
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.GetTempContent("Add Credentials")))
			{
				PopupWindow.Show(addCredentialsRect,new AddCredentialPopup());
			}
			if (current.type == EventType.Repaint)
			{
				addCredentialsRect = GUILayoutUtility.GetLastRect();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.HelpBox("UniGit does encrypts passwords but, a Security Credentials Manager is always recommended. As it provides more security.", MessageType.Info);
		}

		private void DoBranches(Event current)
		{
			DoBranch(GitManager.Repository.Head);

			EditorGUILayout.Space();
			GUILayout.Label(GUIContent.none, "sv_iconselector_sep");
			EditorGUILayout.Space();

			if (branches != null)
			{
				foreach (var branch in branches)
				{
					if(branch.IsCurrentRepositoryHead) continue;
					DoBranch(branch);
				}
			}

			EditorGUILayout.Space();
			Rect createBranchRect = GUILayoutUtility.GetRect(GitGUI.GetTempContent("Create Branch"), GUI.skin.button);
			if (GUI.Button(createBranchRect, GitGUI.GetTempContent("Create Branch")))
			{
				PopupWindow.Show(createBranchRect, new CreateBranchPopup(this,GitManager.Repository.Commits.FirstOrDefault(),()=> { branches = null; }));
			}
		}

		private void DoBranch(Branch branch)
		{
			GUILayout.Label(GitGUI.GetTempContent(branch.FriendlyName), branch.IsCurrentRepositoryHead ? "IN BigTitle" : "ShurikenModuleTitle",GUILayout.ExpandWidth(true));
			int selectedRemote = Array.IndexOf(remoteCacheList, branch.Remote);
			if (remoteNames != null)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("Remote"));
				EditorGUI.BeginChangeCheck();
				int newSelectedRemote = EditorGUILayout.Popup(selectedRemote, remoteNames);
				EditorGUILayout.EndHorizontal();
				if (EditorGUI.EndChangeCheck() && selectedRemote != newSelectedRemote)
				{
					branches.Update(branch, (u) =>
					{
						u.Remote = remoteCacheList[newSelectedRemote].Name;
						u.UpstreamBranch = branch.CanonicalName;
					});
				}
			}

			EditorGUILayout.TextField(GitGUI.GetTempContent("Upstream Branch"), branch.UpstreamBranchCanonicalName);
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = remoteCacheList != null && remoteCacheList.Length < selectedRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Save", "Send branch changes to selected remote."), "minibuttonleft"))
			{
				branches.Update(branch, (u) =>
				{
					u.Remote = remoteCacheList[selectedRemote].Name;
					u.UpstreamBranch = branch.CanonicalName;
				});
			}
			GUI.enabled = !branch.IsRemote && branch.IsCurrentRepositoryHead;
			if (GUILayout.Button("Switch", "minibuttonmid"))
			{
				if (GitExternalManager.TakeSwitch())
				{
					AssetDatabase.Refresh();
					GitManager.MarkDirty();
				}
				else
				{
					Debug.LogException(new NotImplementedException("Branch Checkout not implemented. Use External program for branch switching."));
					//todo implement branch checkout
				}
			}
			GUI.enabled = !branch.IsCurrentRepositoryHead;
			if (GUILayout.Button(GitGUI.GetTempContent("Delete", branch.IsCurrentRepositoryHead ? "Can not delete head branch" : ""), "minibuttonmid"))
			{
				if (EditorUtility.DisplayDialog("Delete Branch", "Are you sure you want do delete a branch? This action can not be undone.", "Delete", "Cancel"))
				{
					try
					{
						GitManager.Repository.Branches.Remove(branch);
						branches = null;
						GitManager.MarkDirty(true);
					}
					catch (Exception e)
					{
						Debug.Log("Could not delete branch: " + branch.CanonicalName);
						Debug.LogException(e);
					}
				}
			}
			GUI.enabled = !branch.IsRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Reset", "Reset branch properties."), "minibuttonright"))
			{
				branches.Update(branch, (u) =>
				{
					u.Remote = "";
					u.UpstreamBranch = "";
				});
			}
			GUI.enabled = true;
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		#region Externals

		private void DoExternals(Event current)
		{
			GitSettingsJson settings = GitManager.Settings;
			if (settings == null) return;

			EditorGUI.BeginChangeCheck();
			settings.ExternalsType = (GitSettings.ExternalsTypeEnum)EditorGUILayout.EnumMaskField(GitGUI.GetTempContent("External Program Uses", "Use an external program for more advanced features like pushing, pulling, merging and so on"), settings.ExternalsType);
			if (EditorGUI.EndChangeCheck())
			{
				settings.MarkDirty();
			}

			EditorGUI.BeginChangeCheck();
			int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("External Program", "The name of the External program to use"), GitExternalManager.SelectedAdapterIndex, GitExternalManager.AdapterNames);
			settings.ExternalProgram = GitExternalManager.AdapterNames[newSelectedIndex].text;
			if (EditorGUI.EndChangeCheck())
			{
				GitExternalManager.SetSelectedAdapter(newSelectedIndex);
				settings.MarkDirty();
			}

			EditorGUILayout.HelpBox("Using external programs is always recommended as UniGit is still in development.",MessageType.Info);
		}

		#endregion

		#region LFS

		private Rect trackFileRect;

		private void DoLFS(Event current)
		{
			if (!GitLfsManager.Installed)
			{
				EditorGUILayout.HelpBox("Git LFS not installed", MessageType.Warning);
				if (GUILayout.Button(GitGUI.GetTempContent("Download")))
				{
					Application.OpenURL("https://git-lfs.github.com/");
				}
			}
			else
			{
				if (!GitLfsManager.CheckInitialized())
				{
					EditorGUILayout.HelpBox("Git LFS not Initialized", MessageType.Info);
					if (GUILayout.Button(GitGUI.GetTempContent("Initialize")))
					{
						GitLfsManager.Initialize();
					}
				}
				else
				{
					GUILayout.Label(GitGUI.GetTempContent("Settings"), "ProjectBrowserHeaderBgTop");


					string url = GitManager.Repository.Config.GetValueOrDefault("lfs.url", "");
					if (string.IsNullOrEmpty(url))
					{
						EditorGUILayout.HelpBox("You should specify a LFS server URL", MessageType.Warning);
					}

					DoConfigStringField(GitGUI.GetTempContent("URL"), "lfs.url", "");

					EditorGUILayout.Space();

					foreach (var info in GitLfsManager.TrackedInfo)
					{
						GUILayout.Label(GitGUI.GetTempContent(info.Extension), "ShurikenModuleTitle");
						GUI.SetNextControlName(info.GetHashCode() + " Extension");
						info.Extension = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Extension"), info.Extension);
						GUI.SetNextControlName(info.GetHashCode() + " Type");
						info.Type = (GitLfsTrackedInfo.TrackType) EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Type"), info.Type);

						if (info.IsDirty)
						{
							GitLfsManager.SaveTracking();
							break;
						}
					}

					if (GUILayout.Button("Track File"))
					{
						PopupWindow.Show(trackFileRect, new GitLfsTrackPopupWindow(this));
					}
					if (current.type == EventType.Repaint)
					{
						trackFileRect = GUILayoutUtility.GetLastRect();
					}
				}
			}

			EditorGUILayout.HelpBox("Git LFS is still in development, and is recommended to use an external program for handling it.",MessageType.Info);
		}
		#endregion

		#region Remotes
		private void DoRemotes(Event current)
		{
			int remoteCount = remotes.Count();
			if (remoteCount <= 0)
			{
				EditorGUILayout.HelpBox("No Remotes", MessageType.Info);
			}

			foreach (var remote in remoteCacheList)
			{
				GUILayout.Label(GitGUI.GetTempContent(remote.Name), "ShurikenModuleTitle");
				EditorGUILayout.Space();
				EditorGUILayout.BeginVertical();
				EditorGUI.BeginChangeCheck();
				GUI.enabled = false;
				GUI.SetNextControlName(remote.GetHashCode() + " Remote Name");
				EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), remote.Name);
				GUI.enabled = true;
				GUI.SetNextControlName(remote.GetHashCode() + " Remote URL");
				remote.Url = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("URL"), remote.Url);
				//remote.PushUrl = EditorGUILayout.DelayedTextField(new GUIContent("Push URL"), remote.PushUrl, "ShurikenValue");
				remote.TagFetchMode = (TagFetchMode)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Tag Fetch Mode"), remote.TagFetchMode);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(GitGUI.GetTempContent("Save"), "minibuttonleft"))
				{
					remote.Update(remotes);
					UpdateRemotes();
					GUI.FocusControl("");
				}
				if (GUILayout.Button(GitGUI.GetTempContent("Open","Show the repository in browser."), "minibuttonmid"))
				{
					Application.OpenURL(remote.Url);
				}
				if (GUILayout.Button(GitGUI.GetTempContent("Remove"), "minibuttonright"))
				{
					remotes.Remove(remote.Name);
					UpdateRemotes();
					GUI.FocusControl("");
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.GetTempContent("Add Remote"), "LargeButton"))
			{
				PopupWindow.Show(addRepositoryButtonRect, new AddRepositoryPopup(remotes));
			}
			if (current.type == EventType.Repaint) addRepositoryButtonRect = GUILayoutUtility.GetLastRect();
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
		#endregion

		private class RemoteEntry
		{
			private readonly Remote remote;
			private string name;
			public string Url { get; set; }
			public string PushUrl { get; set; }
			public TagFetchMode TagFetchMode { get; set; }

			public RemoteEntry(Remote remote)
			{
				this.remote = remote;
				Update();
			}

			private void Update()
			{
				name = remote.Name;
				Url = remote.Url;
				PushUrl = remote.PushUrl;
				TagFetchMode = remote.TagFetchMode;
			}

			public void Update(RemoteCollection remotes)
			{
				remotes.Update(remote, UpdateAction);
				Update();
			}

			private void UpdateAction(RemoteUpdater updater)
			{
				updater.Url = Url;
				updater.PushUrl = Url;
				updater.TagFetchMode = TagFetchMode;
			}

			public override bool Equals(object obj)
			{
				if (obj is Remote)
				{
					return remote.Equals(obj);
				}
				return base.Equals(obj);
			}

			public override int GetHashCode()
			{
				return remote.GetHashCode();
			}

			public Remote Remote
			{
				get { return remote; }
			}

			public string Name
			{
				get { return name; }
			}
		}

		[SerializeField]
		private enum SettingTabEnum
		{
			General,
			Externals,
			Remotes,
			Branches,
			LFS,
			Security,
			Ignore
		}

		#region Popup Windows
		private class AddRepositoryPopup : PopupWindowContent
		{
			private RemoteCollection remoteCollection;
			private string name = "origin";
			private string url;

			public AddRepositoryPopup(RemoteCollection remoteCollection)
			{
				this.remoteCollection = remoteCollection;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(300,80);
			}

			public override void OnGUI(Rect rect)
			{
				EditorGUILayout.Space();
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"),name);
				url = EditorGUILayout.TextField(GitGUI.GetTempContent("URL"), url);
				GUI.enabled = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url);
				if (GUILayout.Button(GitGUI.GetTempContent("Add Remote")))
				{
					remoteCollection.Add(name, url);
					GitManager.MarkDirty();
					GetWindow<GitSettingsWindow>().Focus();
				}
				GUI.enabled = true;
				EditorGUILayout.Space();
			}
		}

		private class AddCredentialPopup : PopupWindowContent
		{
			private string name;
			private string url;
			private string username;
			private string password;

			public override Vector2 GetWindowSize()
			{
				return new Vector2(300, 100);
			}

			public override void OnGUI(Rect rect)
			{
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
				url = EditorGUILayout.TextField(GitGUI.GetTempContent("URL"), url);
				username = EditorGUILayout.TextField(GitGUI.GetTempContent("Username"), username);
				password = EditorGUILayout.PasswordField(GitGUI.GetTempContent("Password"), password);
				GUI.enabled = !string.IsNullOrEmpty(url);
				if (GUILayout.Button(GitGUI.GetTempContent("Add Credential")))
				{
					var entry = GitCredentialsManager.CreatEntry(url, username, password);
					if (entry != null)
					{
						entry.Name = name;
						GitCredentialsManager.GitCredentials.MarkDirty();
						GetWindow<GitSettingsWindow>().Focus();
					}
					else
					{
						editorWindow.ShowNotification(GitGUI.GetTempContent("URL already exists"));
					}
				}
				GUI.enabled = true;
			}
		}

		public class CreateBranchPopup : PopupWindowContent
		{
			private string name = "";
			private Commit commit;
			private EditorWindow parentWindow;
			private Action onCreated;

			public CreateBranchPopup(EditorWindow parentWindow,Commit commit,Action onCreated)
			{
				this.parentWindow = parentWindow;
				this.commit = commit;
				this.onCreated = onCreated;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(300, 80);
			}

			public override void OnGUI(Rect rect)
			{
				EditorGUILayout.Space();
				if (commit != null)
				{
					name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
					EditorGUILayout.LabelField(GitGUI.GetTempContent("Commit SHA"), new GUIContent(commit.Sha));
				}
				else
				{
					EditorGUILayout.HelpBox("No selected commit.",MessageType.Warning);
				}
				
				GitGUI.StartEnable(!string.IsNullOrEmpty(name) && commit != null);
				if (GUILayout.Button(GitGUI.GetTempContent("Create Branch")))
				{
					try
					{
						var branch = GitManager.Repository.CreateBranch(name, commit);
						editorWindow.Close();
						parentWindow.ShowNotification(new GUIContent(string.Format("Branch '{0}' created",branch.CanonicalName)));
						if (onCreated != null)
						{
							onCreated.Invoke();
						}
						GitManager.MarkDirty(true);
					}
					catch (Exception e)
					{
						Debug.LogError("Could not create branch!");
						Debug.LogException(e);
					}
				}
				GitGUI.EndEnable();
			}
		}
		#endregion
	}
}