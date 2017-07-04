using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitSecuritySettingsTab : GitSettingsTab
	{
		private Rect addCredentialsRect;
		private Vector2 scroll;

		internal override void OnGUI(Rect rect, Event current)
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
			if (GUILayout.Button(GitGUI.GetTempContent("Remove"), "minibutton", GUILayout.Width(64)))
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
				EditorGUILayout.HelpBox("No Git Credentials", MessageType.Warning);
				return;
			}

			scroll = EditorGUILayout.BeginScrollView(scroll);
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
					GitCredentialsManager.SetNewPassword(gitCredential.URL, gitCredential.Username, gitCredential.NewPassword);
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
			EditorGUILayout.EndScrollView();

			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(GitGUI.IconContent("ol plus","Add Credentials"), "AC Button"))
			{
				PopupWindow.Show(addCredentialsRect, new AddCredentialPopup());
			}
			if (current.type == EventType.Repaint)
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
						EditorWindow.GetWindow<GitSettingsWindow>().Focus();
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