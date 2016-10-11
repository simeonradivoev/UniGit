using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
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
			OnGitUpdate(null);
		}

		[UsedImplicitly]
		private void OnUnfocus()
		{
			LoseFocus();
		}

		protected override void OnInitialize()
		{
			serializedSettings = new SerializedObject(GitManager.Settings);
		}

		protected override void OnGitUpdate(RepositoryStatus status)
		{
			UpdateRemotes();
			UpdateBranches();
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
			bool value = GUILayout.Toggle(tab == SettingTabEnum.General, new GUIContent("General"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.General;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Externals, new GUIContent("Externals"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Externals;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Remotes, new GUIContent("Remotes"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Remotes;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Branches, new GUIContent("Branches"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Branches;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.LFS, new GUIContent("LFS"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.LFS;
			}
			value = GUILayout.Toggle(tab == SettingTabEnum.Security, new GUIContent("Security"), "toolbarbutton");
			if (value)
			{
				tab = SettingTabEnum.Security;
			}
			if(EditorGUI.EndChangeCheck()) LoseFocus();
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();

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
			GUILayout.Box(new GUIContent("Unity Settings"), "ProjectBrowserHeaderBgTop");

			EditorGUILayout.PropertyField(serializedSettings.FindProperty("AutoStage"));
			EditorGUILayout.PropertyField(serializedSettings.FindProperty("AutoFetch"));
			serializedSettings.ApplyModifiedProperties();
			EditorGUILayout.PropertyField(serializedSettings.FindProperty("MaxCommits"));
			if (serializedSettings.ApplyModifiedProperties())
			{
				GitManager.Update();
			}

			GUILayout.Box(new GUIContent("Git Settings"), "ProjectBrowserHeaderBgMiddle");

			EditorGUILayout.LabelField(new GUIContent("User"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringField(new GUIContent("Name"), "user.name", "");
			DoConfigStringField(new GUIContent("Email"), "user.email", "");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(new GUIContent("Core"),EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1; 
			DoConfigToggle(new GUIContent("Auto LF line endings"), "core.autocrlf", true);
			DoConfigToggle(new GUIContent("Bare"), "core.bare",false);
			DoConfigToggle(new GUIContent("Symlinks"), "core.symlinks",false);
			DoConfigToggle(new GUIContent("Ignore Case"), "core.ignorecase",true);
			DoConfigToggle(new GUIContent("Logal Reference Updates"), "core.logallrefupdates",true);
			DoConfigIntSlider(new GUIContent("Compression"), -1,9, "core.compression",-1);
			DoConfigStringField(new GUIContent("Big File Threshold"), "core.bigFileThreshold","512m");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(new GUIContent("Branch"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigStringsField(new GUIContent("Auto Setup Rebase"), "branch.autoSetupRebase", autoRebaseOptions, "never");
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(new GUIContent("Diff"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(new GUIContent("Renames"), "diff.renames", true);
			DoConfigIntField(new GUIContent("Rename Limit"), "diff.renameLimit", -1);
			EditorGUI.indentLevel = 0;

			EditorGUILayout.LabelField(new GUIContent("HTTP"), EditorStyles.boldLabel);
			EditorGUI.indentLevel = 1;
			DoConfigToggle(new GUIContent("Verify SSL Crtificate"), "http.sslVerify",true);
			string oldPath = GitManager.Repository.Config.GetValueOrDefault<string>("http.sslCAInfo");
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(new GUIContent("SSL Certificate File"));
			if (GUILayout.Button(new GUIContent(oldPath), "TE ToolbarDropDown"))
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
			SerializedProperty credentialsManagerProperty = serializedSettings.FindProperty("CredentialsManager");
			int newSelectedIndex = EditorGUILayout.Popup(new GUIContent("Credentials Manager", "The name of the External program to use"), GitCredentialsManager.SelectedAdapterIndex, GitCredentialsManager.AdapterNames);
			credentialsManagerProperty.stringValue = newSelectedIndex >= 0 && newSelectedIndex < GitCredentialsManager.AdapterIds.Length ? GitCredentialsManager.AdapterIds[newSelectedIndex] : "";
			if (serializedSettings.ApplyModifiedPropertiesWithoutUndo())
			{
				GitCredentialsManager.SetSelectedAdapter(newSelectedIndex);
				AssetDatabase.SaveAssets();
			}
			GUI.enabled = newSelectedIndex >= 0;
			if (GUILayout.Button(new GUIContent("Remove"),"minibutton",GUILayout.Width(64)))
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
				GUILayout.Label(new GUIContent(gitCredential.Name), "ShurikenModuleTitle");
				EditorGUILayout.Space();
				EditorGUILayout.BeginVertical("ShurikenModuleBg");
				EditorGUI.BeginChangeCheck();
				GUI.SetNextControlName(gitCredential.URL + " Credential Name");
				gitCredential.Name = EditorGUILayout.TextField(new GUIContent("Name"), gitCredential.Name);
				GUI.enabled = false;
				GUI.SetNextControlName(gitCredential.URL + " Credential URL");
				EditorGUILayout.TextField(new GUIContent("URL"), gitCredential.URL);
				GUI.enabled = true;
				EditorGUILayout.Space();
				GUILayout.Label(GUIContent.none, "ShurikenLine");
				EditorGUILayout.Space();
				bool newIsToken = gitCredential.IsToken;
				newIsToken = EditorGUILayout.Toggle(new GUIContent("Is Token", "Are credentials used as a token, like in GitHub."), newIsToken);
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
					string newUsername = EditorGUILayout.DelayedTextField(new GUIContent("Token"), gitCredential.Username);
					if (newUsername != gitCredential.Username)
					{
						GitCredentialsManager.SetNewUsername(gitCredential.URL, newUsername);
					}
				}
				else
				{
					GUI.SetNextControlName(gitCredential.URL + " Credential username");
					string newUsername = EditorGUILayout.DelayedTextField(new GUIContent("Username"), gitCredential.Username);
					if (newUsername != gitCredential.Username)
					{
						GitCredentialsManager.SetNewUsername(gitCredential.URL, newUsername);
					}
					GUI.SetNextControlName(gitCredential.URL + " Credential New Password");
					gitCredential.NewPassword = EditorGUILayout.PasswordField(new GUIContent("New Password"), gitCredential.NewPassword);

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
				if (GUILayout.Button(new GUIContent("Change Password"), "minibuttonleft"))
				{
					GitCredentialsManager.SetNewPassword(gitCredential.URL,gitCredential.Username,gitCredential.NewPassword);
					gitCredential.NewPassword = "";
					EditorUtility.SetDirty(GitManager.GitCredentials);
					GUI.FocusControl("");
					EditorUtility.DisplayDialog("Password Changed", "Password successfully changed", "Ok");
				}
				GUI.enabled = gitCredential.HasPassword;
				if (GUILayout.Button(new GUIContent("Clear Password"), "minibuttonmid"))
				{
					GitCredentialsManager.ClearCredentialPassword(gitCredential.URL);
					EditorUtility.SetDirty(GitManager.GitCredentials);
					AssetDatabase.SaveAssets();
				}
				GUI.enabled = true;
				if (GUILayout.Button(new GUIContent("Save"), "minibuttonmid"))
				{
					EditorUtility.SetDirty(GitManager.GitCredentials);
					AssetDatabase.SaveAssets();
				}
				if (GUILayout.Button(new GUIContent("Remove"), "minibuttonright"))
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
			if (GUILayout.Button(new GUIContent("Add Credentials")))
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
				GUILayout.Label(new GUIContent(branch.FriendlyName), "ShurikenModuleTitle");
				int selectedRemote = Array.IndexOf(remoteCacheList, branch.Remote);
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(new GUIContent("Remote"));
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
				EditorGUILayout.TextField(new GUIContent("Upstream Branch"), branch.UpstreamBranchCanonicalName);
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
			SerializedProperty externalTypesProperty = serializedSettings.FindProperty("ExternalsType");
			externalTypesProperty.intValue = (int)(GitSettings.ExternalsTypeEnum)EditorGUILayout.EnumMaskField(new GUIContent("External Program Uses", "Use an external program for more advanced features like pushing, pulling, merging and so on"), (GitSettings.ExternalsTypeEnum)externalTypesProperty.intValue);
			if (serializedSettings.ApplyModifiedProperties())
			{
				AssetDatabase.SaveAssets();
			}

			SerializedProperty externalProgramProperty = serializedSettings.FindProperty("ExternalProgram");
			int newSelectedIndex = EditorGUILayout.Popup(new GUIContent("External Program", "The name of the External program to use"), GitExternalManager.SelectedAdapterIndex, GitExternalManager.AdapterNames);
			externalProgramProperty.stringValue = GitExternalManager.AdapterNames[newSelectedIndex].text;
			if (serializedSettings.ApplyModifiedPropertiesWithoutUndo())
			{
				GitExternalManager.SetSelectedAdapter(newSelectedIndex);
				AssetDatabase.SaveAssets();
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
				if (GUILayout.Button(new GUIContent("Download")))
				{
					Application.OpenURL("https://git-lfs.github.com/");
				}
				return;
			}

			if (!GitLfsManager.CheckInitialized())
			{
				EditorGUILayout.HelpBox("Git LFS not Initialized", MessageType.Info);
				if (GUILayout.Button(new GUIContent("Initialize")))
				{
					GitLfsManager.Initialize();
				}
				return;
			}

			GUILayout.Label(new GUIContent("Settings"), "ProjectBrowserHeaderBgTop");


			string url = GitManager.Repository.Config.GetValueOrDefault("lfs.url", "");
			if (string.IsNullOrEmpty(url))
			{
				EditorGUILayout.HelpBox("You should specify a LFS server URL",MessageType.Warning);
			}

			DoConfigStringField(new GUIContent("URL"), "lfs.url", "");

			EditorGUILayout.Space();

			foreach (var info in GitLfsManager.TrackedInfo)
			{
				GUILayout.Label(new GUIContent(info.Extension),"ShurikenModuleTitle");
				GUI.SetNextControlName(info.GetHashCode() + " Extension");
				info.Extension = EditorGUILayout.DelayedTextField(new GUIContent("Extension"),info.Extension);
				GUI.SetNextControlName(info.GetHashCode() + " Type");
				info.Type = (GitLfsTrackedInfo.TrackType)EditorGUILayout.EnumPopup(new GUIContent("Type"), info.Type);

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
				GUILayout.Label(new GUIContent(remote.Name), "ShurikenModuleTitle");
				EditorGUILayout.Space();
				EditorGUILayout.BeginVertical();
				EditorGUI.BeginChangeCheck();
				GUI.enabled = false;
				GUI.SetNextControlName(remote.GetHashCode() + " Remote Name");
				EditorGUILayout.TextField(new GUIContent("Name"), remote.Name);
				GUI.enabled = true;
				GUI.SetNextControlName(remote.GetHashCode() + " Remote URL");
				remote.Url = EditorGUILayout.DelayedTextField(new GUIContent("URL"), remote.Url);
				//remote.PushUrl = EditorGUILayout.DelayedTextField(new GUIContent("Push URL"), remote.PushUrl, "ShurikenValue");
				remote.TagFetchMode = (TagFetchMode)EditorGUILayout.EnumPopup(new GUIContent("Tag Fetch Mode"), remote.TagFetchMode);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(new GUIContent("Save"), "minibuttonleft"))
				{
					remote.Update(remotes);
					UpdateRemotes();
					GUI.FocusControl("");
				}
				if (GUILayout.Button(new GUIContent("Open","Show the repository in browser."), "minibuttonmid"))
				{
					Application.OpenURL(remote.Url);
				}
				if (GUILayout.Button(new GUIContent("Remove"), "minibuttonright"))
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
			if (GUILayout.Button(new GUIContent("Add Remote"), "LargeButton"))
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
				name = EditorGUILayout.TextField(new GUIContent("Name"),name);
				url = EditorGUILayout.TextField(new GUIContent("URL"), url);
				GUI.enabled = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url);
				if (GUILayout.Button(new GUIContent("Add Remote")))
				{
					remoteCollection.Add(name, url);
					GitManager.Update();
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
				name = EditorGUILayout.TextField(new GUIContent("Name"), name);
				url = EditorGUILayout.TextField(new GUIContent("URL"), url);
				password = EditorGUILayout.PasswordField(new GUIContent("Password"), password);
				GUI.enabled = !string.IsNullOrEmpty(url);
				if (GUILayout.Button(new GUIContent("Add Credential")))
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
						editorWindow.ShowNotification(new GUIContent("URL already exists"));
					}
				}
				GUI.enabled = true;
			}
		}
		#endregion
	}
}