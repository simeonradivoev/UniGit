using System.Linq;
using System.Security;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitSecuritySettingsTab : GitSettingsTab
	{
		private Rect addCredentialsRect;
		private Vector2 scroll;
		private readonly GitCredentialsManager credentialsManager;
		private readonly GitOverlay gitOverlay;

		[UniGitInject]
		public GitSecuritySettingsTab(GitManager gitManager, 
			GitSettingsWindow settingsWindow,
			GitCredentialsManager credentialsManager,
			UniGitData data,
			GitSettingsJson gitSettings,
			GitCallbacks gitCallbacks,
			GitInitializer initializer,
			GitOverlay gitOverlay) 
			: base(new GUIContent("Security"), gitManager, settingsWindow,data,gitSettings,gitCallbacks,initializer)
		{
			this.credentialsManager = credentialsManager;
			this.gitOverlay = gitOverlay;
		}

		internal override void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();
			if (gitSettings != null)
			{
				EditorGUI.BeginChangeCheck();
				var newSelectedIndex = EditorGUILayout.Popup(GitGUI.GetTempContent("Credentials Manager", "The name of the External program to use"), credentialsManager.SelectedAdapterIndex, credentialsManager.AdapterNames);
				gitSettings.CredentialsManager = newSelectedIndex >= 0 && newSelectedIndex < credentialsManager.AdapterIds.Length ? credentialsManager.AdapterIds[newSelectedIndex] : "";
				if (EditorGUI.EndChangeCheck())
				{
					credentialsManager.SetSelectedAdapter(newSelectedIndex,false);

					gitSettings.MarkDirty();
				}
				GUI.enabled = newSelectedIndex >= 0;
			}
			if (GUILayout.Button(GitGUI.GetTempContent("Remove"), EditorStyles.miniButton, GUILayout.Width(64)))
            {
                var choice = EditorUtility.DisplayDialogComplex("Remove Credentials Manager", "Disable the current external manager only or delete passwords win external manager.", "Disable Only", "Cancel", "Disable and Delete");
                switch (choice)
                {
                    case 0:
                        credentialsManager.SetSelectedAdapter(-1,false);
                        break;
                    case 2:
                        credentialsManager.SetSelectedAdapter(-1,true);
                        break;
                }
            }
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			if (credentialsManager.GitCredentials == null)
			{
				EditorGUILayout.HelpBox("No Git Credentials", MessageType.Warning);
				return;
			}

			scroll = EditorGUILayout.BeginScrollView(scroll);
			foreach (var gitCredential in credentialsManager.GitCredentials)
			{
				var hasPassword = credentialsManager.HasPassword(gitCredential);
				var currentusername = credentialsManager.LoadUsername(gitCredential);

				Texture nameIcon = null;
				var nameTooltip = "";

				if (data.RepositoryStatus.SubModuleEntries.Any(m => m.Url == gitCredential.URL))
				{
					nameIcon = gitOverlay.icons.submoduleIconSmall.image;
					nameTooltip = "These credentials are automatically used by a sub module";
				}
				else if (data.RepositoryStatus.RemoteEntries.Any(r => r.Url == gitCredential.URL))
				{
					nameIcon = GitGUI.IconContentTex("ToolHandleGlobal");
					nameTooltip = "These credentials are automatically used when dealing with a remote";

				}

				GUILayout.Label(GitGUI.GetTempContent(gitCredential.Name,nameIcon,nameTooltip), GitGUI.Styles.ShurikenModuleTitle);
				EditorGUILayout.Space();
				EditorGUILayout.BeginVertical(GitGUI.Styles.ShurikenModuleBg);
				EditorGUI.BeginChangeCheck();
				GUI.SetNextControlName(gitCredential.URL + " Credential Name");
				gitCredential.Name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name","Display name. Serves no purpose other then visual."), gitCredential.Name);
				if (credentialsManager.IsAdapterSelected)
				{
					var managerUrlContent = GitGUI.GetTempContent("Manager URL");
					if (!hasPassword)
					{
						managerUrlContent.image = GitGUI.Textures.WarrningIconSmall;
						managerUrlContent.tooltip = "No credentials with specified url found in external manager.";
					}
					EditorGUILayout.BeginHorizontal();
					gitCredential.ManagerUrl = EditorGUILayout.TextField(managerUrlContent, gitCredential.ManagerUrl);
					if (GUILayout.Button(GitGUI.IconContent("UnityEditor.SceneHierarchyWindow", string.Empty, "Options"), GitGUI.Styles.IconButton,GUILayout.Width(20)))
					{
						var c = gitCredential;
						var genericMenu = new GenericMenu();
						genericMenu.AddItem(new GUIContent("Automatic fill"), false, () =>
						{
							c.ManagerUrl = credentialsManager.GetFormatedUrl(c.URL); 
							credentialsManager.GitCredentials.MarkDirty();
						});
						genericMenu.AddItem(new GUIContent("Clear"), false, () =>
						{
							c.ManagerUrl = ""; 
							credentialsManager.GitCredentials.MarkDirty();
						});
						genericMenu.ShowAsContext();
					}
					EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(),MouseCursor.Link);
					EditorGUILayout.EndHorizontal();
				}
				
				EditorGUILayout.Space();
				GUILayout.Label(GUIContent.none, "sv_iconselector_sep");
				EditorGUILayout.Space();
				var newIsToken = gitCredential.IsToken;

				if (credentialsManager.IsAdapterSelected)
				{
					GUI.enabled = false;
					EditorGUILayout.TextField(GitGUI.GetTempContent(newIsToken ? "Manager Token" : "Manager Username"),currentusername);
					GUI.enabled = true;
				}
				else
				{
					GUI.enabled = false;
					EditorGUILayout.TextField(GitGUI.GetTempContent(newIsToken ? "Current Token" : "Current Username"),currentusername);
					GUI.enabled = true;
				}

				newIsToken = EditorGUILayout.Toggle(GitGUI.GetTempContent("Is Token", "Are credentials used as a token, like in GitHub."), newIsToken);
				if (newIsToken != gitCredential.IsToken)
				{
					gitCredential.IsToken = newIsToken;
					if (gitCredential.IsToken)
					{
						credentialsManager.ClearCredentialPassword(gitCredential);
					}
				}

				GUI.SetNextControlName(gitCredential.URL + " Credential username");
				gitCredential.NewUsername = EditorGUILayout.DelayedTextField(newIsToken ? GitGUI.GetTempContent("New Token") : GitGUI.GetTempContent("New Username"), gitCredential.NewUsername);

				if (!gitCredential.IsToken)
				{
					GUI.SetNextControlName(gitCredential.URL + " Credential New Password");
					GitGUI.SecurePasswordFieldLayout(GitGUI.GetTempContent("New Password"),gitCredential.NewPassword);
					if (credentialsManager.IsAdapterSelected && GUI.GetNameOfFocusedControl() == gitCredential.URL + " Credential New Password")
					{
						EditorGUILayout.HelpBox("Password will be set in the current credentials manager: " + credentialsManager.SelectedAdapterName, MessageType.Info);
					}
				}

				if (EditorGUI.EndChangeCheck())
				{
					credentialsManager.GitCredentials.MarkDirty();
				}

				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUI.enabled = !string.IsNullOrEmpty(gitCredential.NewUsername) && (!credentialsManager.IsAdapterSelected || hasPassword);
				if (GUILayout.Button(GitGUI.GetTempContent("Set Username"), EditorStyles.miniButtonLeft))
				{
					credentialsManager.SetNewUsername(gitCredential,gitCredential.NewUsername);
					gitCredential.NewUsername = "";
					EditorUtility.DisplayDialog("Username Changed", "Username successfully changed", "Ok");
				}

				GUI.enabled = gitCredential.NewPassword != null && gitCredential.NewPassword.Length > 0 && (!credentialsManager.IsAdapterSelected || hasPassword);
				if (GUILayout.Button(GitGUI.GetTempContent("Set Password"), EditorStyles.miniButtonMid))
				{
					credentialsManager.SetNewPassword(gitCredential, gitCredential.NewPassword);
					gitCredential.NewPassword.Dispose();
					gitCredential.NewPassword = new SecureString();
					GUI.FocusControl("");
					EditorUtility.DisplayDialog("Password Changed", "Password successfully changed", "Ok");
				}

				if (credentialsManager.IsAdapterSelected)
				{
					GUI.enabled = !hasPassword && !string.IsNullOrEmpty(gitCredential.ManagerUrl) && !string.IsNullOrEmpty(gitCredential.NewUsername) && gitCredential.NewPassword != null && gitCredential.NewPassword.Length > 0;

					if (GUILayout.Button(GitGUI.GetTempContent("New External","Create new entry in external password manager with provided new password, username and url."), EditorStyles.miniButtonMid))
					{
						credentialsManager.CreateNewExternal(gitCredential.ManagerUrl,gitCredential.NewUsername,gitCredential.NewPassword);
						gitCredential.NewUsername = "";
						gitCredential.NewPassword.Dispose();
						gitCredential.NewPassword = new SecureString();
					}

					GUI.enabled = true;
				}
				else
				{
					GUI.enabled = hasPassword;
					if (GUILayout.Button(GitGUI.GetTempContent("Clear Password"), EditorStyles.miniButtonMid))
					{
						if (EditorUtility.DisplayDialog("Are you sure?", "Are you sure you want to clear the stored password.", "Clear","Cancel"))
						{
							credentialsManager.ClearCredentialPassword(gitCredential);
						}
					}
					GUI.enabled = true;
				}

				if (GUILayout.Button(GitGUI.GetTempContent("Remove"), EditorStyles.miniButtonRight))
				{
					var choice = EditorUtility.DisplayDialogComplex("Remove Credential Entry", "Removing credential from UniGit only or from external manager as well?","Remove From UniGit","Cancel","Remove From Both");
					switch (choice)
                    {
                        case 0:
                            credentialsManager.RemoveCredentials(gitCredential,false);
                            break;
                        case 1:
                            credentialsManager.RemoveCredentials(gitCredential,true);
                            break;
                    }

					credentialsManager.GitCredentials.MarkDirty();
					GUIUtility.ExitGUI();
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndScrollView();
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("ol plus","Add Credentials"), GitGUI.Styles.AddComponentBtn))
			{
				PopupWindow.Show(addCredentialsRect, new AddCredentialPopup(credentialsManager));
			}
			if (Event.current.type == EventType.Repaint)
			{
				addCredentialsRect = GUILayoutUtility.GetLastRect();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.HelpBox("UniGit does encrypts passwords but, a Security Credentials Manager is always recommended. As it provides more security.", MessageType.Info);
		}

		private class AddCredentialPopup : PopupWindowContent
		{
			private string name;
			private string url;
			private string username;
			private SecureString password;
			private readonly GitCredentialsManager credentialsManager;

			public AddCredentialPopup(GitCredentialsManager credentialsManager)
			{
				this.credentialsManager = credentialsManager;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(300, 100);
			}

			public override void OnGUI(Rect rect)
			{
				name = EditorGUILayout.TextField(GitGUI.GetTempContent("Name"), name);
				url = EditorGUILayout.TextField(GitGUI.GetTempContent("URL"), url);
				username = EditorGUILayout.TextField(GitGUI.GetTempContent("Username"), username);
				GUI.enabled = !string.IsNullOrEmpty(url);
				if (GUILayout.Button(GitGUI.GetTempContent("Add Credential")))
				{
					var entry = credentialsManager.CreateEntry(url, username);
					if (entry != null)
					{
						entry.Name = name;
						credentialsManager.GitCredentials.MarkDirty();
					}
					else
					{
						editorWindow.ShowNotification(GitGUI.GetTempContent("URL already exists"));
					}
				}
				GUI.enabled = true;
			}
		}
	}
}