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
		private SerializedObject serializedSettings;

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
			serializedSettings = new SerializedObject(GitManager.Settings);
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
			value = GUILayout.Toggle(tab == SettingTabEnum.Externals, GitGUI.GetTempContent("Externals"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Externals;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Remotes, GitGUI.GetTempContent("Remotes"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Remotes;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Branches, GitGUI.GetTempContent("Branches"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Branches;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.LFS, GitGUI.GetTempContent("LFS"), "toolbarbutton");
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
			//todo cache general settings to reduce lookup
			GUILayout.Box(new GUIContent("Unity Settings"), "ProjectBrowserHeaderBgTop");

			if (serializedSettings != null)
			{
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("AutoStage"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("AutoFetch"));
				serializedSettings.ApplyModifiedProperties();
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("MaxCommits"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("ProjectStatusOverlayDepth"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("ShowEmptyFolders"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("GitStatusMultithreaded"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("UseGavatar"));
				EditorGUILayout.PropertyField(serializedSettings.FindProperty("MaxCommitTextAreaSize"));
				if (serializedSettings.ApplyModifiedProperties())
				{
					GitManager.MarkDirty();
				}
			}

			GUILayout.Box(GitGUI.GetTempContent("Git Settings"), "ProjectBrowserHeaderBgMiddle");

			EditorGUILayout.LabelField(GitGUI.GetTempContent("User"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringField(GitGUI.GetTempContent("Name"), "user.name", "");
			DoConfigStringField(GitGUI.GetTempContent("Email"), "user.email", "");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Core"),EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1; 
			DoConfigToggle(GitGUI.GetTempContent("Auto LF line endings"), "core.autocrlf", true);
			DoConfigToggle(GitGUI.GetTempContent("Bare"), "core.bare",false);
			DoConfigToggle(GitGUI.GetTempContent("Symlinks"), "core.symlinks",false);
			DoConfigToggle(GitGUI.GetTempContent("Ignore Case"), "core.ignorecase",true);
			DoConfigToggle(GitGUI.GetTempContent("Logal Reference Updates"), "core.logallrefupdates",true);
			DoConfigIntSlider(GitGUI.GetTempContent("Compression"), -1,9, "core.compression",-1);
			DoConfigStringField(GitGUI.GetTempContent("Big File Threshold"), "core.bigFileThreshold","512m");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Branch"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringsField(GitGUI.GetTempContent("Auto Setup Rebase"), "branch.autoSetupRebase", autoRebaseOptions, "never");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("Diff"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(GitGUI.GetTempContent("Renames"), "diff.renames", true);
			DoConfigIntField(GitGUI.GetTempContent("Rename Limit"), "diff.renameLimit", -1);
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(GitGUI.GetTempContent("HTTP"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(GitGUI.GetTempContent("Verify SSL Crtificate"), "http.sslVerify",true);
			string oldPath = GitManager.Repository.Config.GetValueOrDefault<string>("http.sslCAInfo");
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("SSL Certificate File"));
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
			if (serializedSettings != null)
			{
				SerializedProperty credentialsManagerProperty = serializedSettings.FindProperty("CredentialsManager");
				int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("Credentials Manager", "The name of the External program to use"), GitCredentialsManager.SelectedAdapterIndex, GitCredentialsManager.AdapterNames);
				credentialsManagerProperty.stringValue = newSelectedIndex >= 0 && newSelectedIndex < GitCredentialsManager.AdapterIds.Length ? GitCredentialsManager.AdapterIds[newSelectedIndex] : "";
				if (serializedSettings.ApplyModifiedPropertiesWithoutUndo())
				{
					GitCredentialsManager.SetSelectedAdapter(newSelectedIndex);
					AssetDatabase.SaveAssets();
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

			if (GitManager.GitCredentials == null)
			{
				EditorGUILayout.HelpBox("No Git Credentials",MessageType.Warning);
				if (GUILayout.Button("Create Credentials File"))
				{
					GitManager.GitCredentials = CreateInstance<GitCredentials>();
					if(!Directory.Exists("Assets/Editor Default Resources/UniGit")) AssetDatabase.CreateFolder("Assets/Editor Default Resources", "UniGit");
					AssetDatabase.CreateAsset(GitManager.GitCredentials, "Assets/Editor Default Resources/UniGit/Git-Credentials.asset");
					AssetDatabase.SaveAssets();
				}
				return;
			}

			foreach (var gitCredential in GitManager.GitCredentials)
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
				GUILayout.Label(GUIContent.none, "ShurikenLine");
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

					if (!gitCredential.HasPassword)
					{
						EditorGUILayout.HelpBox("Credential has no set Password", MessageType.Warning);
					}
				}
				
				if (EditorGUI.EndChangeCheck())
				{
					EditorUtility.SetDirty(GitManager.GitCredentials);
				}

				GUI.enabled = !string.IsNullOrEmpty(gitCredential.NewPassword);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(GitGUI.GetTempContent("Change Password"), "minibuttonleft"))
				{
					GitCredentialsManager.SetNewPassword(gitCredential.URL,gitCredential.Username,gitCredential.NewPassword);
					gitCredential.NewPassword = "";
					EditorUtility.SetDirty(GitManager.GitCredentials);
					GUI.FocusControl("");
					EditorUtility.DisplayDialog("Password Changed", "Password successfully changed", "Ok");
				}
				GUI.enabled = gitCredential.HasPassword;
				if (GUILayout.Button(GitGUI.GetTempContent("Clear Password"), "minibuttonmid"))
				{
					GitCredentialsManager.ClearCredentialPassword(gitCredential.URL);
					EditorUtility.SetDirty(GitManager.GitCredentials);
					AssetDatabase.SaveAssets();
				}
				GUI.enabled = true;
				if (GUILayout.Button(GitGUI.GetTempContent("Save"), "minibuttonmid"))
				{
					EditorUtility.SetDirty(GitManager.GitCredentials);
					AssetDatabase.SaveAssets();
				}
				if (GUILayout.Button(GitGUI.GetTempContent("Remove"), "minibuttonright"))
				{
					GitCredentialsManager.DeleteCredentials(gitCredential.URL);
					GUIUtility.ExitGUI();
					return;
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
		}

		private void DoBranches(Event current)
		{
			foreach (var branch in branches)
			{
				GUILayout.Label(GitGUI.GetTempContent(branch.FriendlyName), "ShurikenModuleTitle");
				int selectedRemote = Array.IndexOf(remoteCacheList, branch.Remote);
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("Remote"));
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
				EditorGUILayout.TextField(GitGUI.GetTempContent("Upstream Branch"), branch.UpstreamBranchCanonicalName);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Save","minibuttonleft"))
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
					Debug.LogException(new NotImplementedException("Branch Checkout not implemented"));
					//todo implement branch checkout
				}
				GUI.enabled = !branch.IsRemote;
				if (GUILayout.Button("Reset", "minibuttonright"))
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
		}

		#region Externals

		private void DoExternals(Event current)
		{
			if (serializedSettings == null) return;
			SerializedProperty externalTypesProperty = serializedSettings.FindProperty("ExternalsType");
			if (externalTypesProperty != null)
			{
				externalTypesProperty.intValue = (int) (GitSettings.ExternalsTypeEnum) EditorGUILayout.EnumMaskField(GitGUI.GetTempContent("External Program Uses", "Use an external program for more advanced features like pushing, pulling, merging and so on"), (GitSettings.ExternalsTypeEnum) externalTypesProperty.intValue);
				if (serializedSettings.ApplyModifiedProperties())
				{
					AssetDatabase.SaveAssets();
				}
			}

			SerializedProperty externalProgramProperty = serializedSettings.FindProperty("ExternalProgram");
			if (externalProgramProperty != null)
			{
				int newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("External Program", "The name of the External program to use"), GitExternalManager.SelectedAdapterIndex, GitExternalManager.AdapterNames);
				externalProgramProperty.stringValue = GitExternalManager.AdapterNames[newSelectedIndex].text;
				if (serializedSettings.ApplyModifiedPropertiesWithoutUndo())
				{
					GitExternalManager.SetSelectedAdapter(newSelectedIndex);
					AssetDatabase.SaveAssets();
				}
			}
		}

		#endregion

		#region LFS

		private Rect trackFileRect;

		private void DoLFS(Event current)
		{
			if (!GitLfsManager.Installed)
			{
				EditorGUILayout.HelpBox("Git LFS not installed",MessageType.Warning);
				if (GUILayout.Button(GitGUI.GetTempContent("Download")))
				{
					Application.OpenURL("https://git-lfs.github.com/");
				}
				return;
			}

			if (!GitLfsManager.CheckInitialized())
			{
				EditorGUILayout.HelpBox("Git LFS not Initialized", MessageType.Info);
				if (GUILayout.Button(GitGUI.GetTempContent("Initialize")))
				{
					GitLfsManager.Initialize();
				}
				return;
			}

			GUILayout.Label(GitGUI.GetTempContent("Settings"), "ProjectBrowserHeaderBgTop");


			string url = GitManager.Repository.Config.GetValueOrDefault("lfs.url", "");
			if (string.IsNullOrEmpty(url))
			{
				EditorGUILayout.HelpBox("You should specify a LFS server URL",MessageType.Warning);
			}

			DoConfigStringField(GitGUI.GetTempContent("URL"), "lfs.url", "");

			EditorGUILayout.Space();

			foreach (var info in GitLfsManager.TrackedInfo)
			{
				GUILayout.Label(GitGUI.GetTempContent(info.Extension),"ShurikenModuleTitle");
				GUI.SetNextControlName(info.GetHashCode() + " Extension");
				info.Extension = EditorGUILayout.DelayedTextField(GitGUI.GetTempContent("Extension"),info.Extension);
				GUI.SetNextControlName(info.GetHashCode() + " Type");
				info.Type = (GitLfsTrackedInfo.TrackType)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Type"), info.Type);

				if (info.IsDirty)
				{
					GitLfsManager.SaveTracking();
					break;
				}
			}

			if (GUILayout.Button("Track File"))
			{
				PopupWindow.Show(trackFileRect,new GitLfsTrackPopupWindow(this));
			}
			if (current.type == EventType.Repaint)
			{
				trackFileRect = GUILayoutUtility.GetLastRect();
			}
		}
		#endregion

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
			Security
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
				return new Vector2(300, 80);
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
						EditorUtility.SetDirty(GitManager.GitCredentials);
						AssetDatabase.SaveAssets();
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
		#endregion
	}
}