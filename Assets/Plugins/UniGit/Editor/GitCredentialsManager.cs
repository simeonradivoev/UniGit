using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UniGit.Security;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitCredentialsManager
	{
		private readonly ICredentialsAdapter[] adapters;
		private readonly GUIContent[] adapterNames;
		private readonly string[] adapterIds;
		private ICredentialsAdapter selectedAdapter;
		private int selectedAdapterIndex;
		private bool initiazlitedSelected;
		private GitCredentialsJson gitCredentials;
		private readonly GitManager gitManager;
		private readonly GitSettingsJson gitSettings;

		[UniGitInject]
		public GitCredentialsManager(GitManager gitManager,GitSettingsJson gitSettings,List<ICredentialsAdapter> adapters)
		{
			this.gitSettings = gitSettings;
			this.gitManager = gitManager;
			this.adapters = adapters.ToArray();
			adapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
			adapterIds = adapters.Select(GetAdapterId).ToArray();

			EditorApplication.update += EditorUpdate;

			LoadGitCredentials();

		}

		private void EditorUpdate()
		{
			if (gitCredentials.IsDirty)
			{
				gitCredentials.ResetDirty();
				SaveCredentialsToFile(gitCredentials);
			}
		}

		private void LoadGitCredentials()
		{
			string credentialsFilePath = CredentialsFilePath;
			GitCredentialsJson credentialsJson = null;
			if (File.Exists(credentialsFilePath))
			{
				try
				{
					credentialsJson = JsonUtility.FromJson<GitCredentialsJson>(File.ReadAllText(credentialsFilePath));
				}
				catch (Exception e)
				{
					Debug.LogError("Could not deserialize git settings. Creating new settings.");
					Debug.LogException(e);
				}
			}

			if (credentialsJson == null)
			{
				credentialsJson = new GitCredentialsJson();
				var oldCredentialsFile = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
				if (oldCredentialsFile != null)
				{
					//must be delayed call for unity to deserialize credentials file properly
					EditorApplication.delayCall += ImportFromOldCredentials;
				}
				else
				{
					SaveCredentialsToFile(credentialsJson);
				}
				
			}

			gitCredentials = credentialsJson;
		}

		private void ImportFromOldCredentials()
		{
			var oldCredentialsFile = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
			if (oldCredentialsFile != null)
			{
				gitCredentials.Copy(oldCredentialsFile);
				Debug.Log("Old Git Credentials transferred to new json credentials file. Old credentials file can now safely be removed.");
			}
			SaveCredentialsToFile(gitCredentials);
		}

		private void SaveCredentialsToFile(GitCredentialsJson credentials)
		{
			ValidateCredentialsPath();
			string credentialsFilePath = CredentialsFilePath;

			try
			{
				string json = JsonUtility.ToJson(credentials);
				File.WriteAllText(credentialsFilePath, json);
			}
			catch (Exception e)
			{
				Debug.LogError("Could not serialize GitCredentialsJson to json file at: " + credentialsFilePath);
				Debug.LogException(e);
			}
		}

		private void ValidateCredentialsPath()
		{
			string settingsFileDirectory = UniGitPath.Combine(gitManager.GitFolderPath, "UniGit");
			if (!Directory.Exists(settingsFileDirectory))
			{
				Directory.CreateDirectory(settingsFileDirectory);
			}
		}

		#region Selection
		//using lazy initialization
		private ICredentialsAdapter SeletedAdapter
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}

				return selectedAdapter;
			}
		}

		public int SelectedAdapterIndex
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}
				return selectedAdapterIndex; 
			}
		}

		public string SelectedAdapterName
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}
				if (selectedAdapterIndex >= 0 && selectedAdapterIndex < adapterNames.Length)
				{
					return adapterNames[selectedAdapterIndex].text;
				}
				return "No Manager";
			}
		}

		public bool IsAdapterSelected
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}
				return selectedAdapterIndex >= 0;
			}
		}

		private void InitializeSelectedAdapter()
		{
			SetSelectedAdapter(Array.IndexOf(adapters, adapters.FirstOrDefault(IsValid)));
			initiazlitedSelected = true;
		}

		internal void SetSelectedAdapter(int index)
		{
			if (index >= adapters.Length || index < 0)
			{
				gitManager.Repository.Config.Set("credential.helper","");
				ResetSelectedAdapter(selectedAdapter);
				selectedAdapterIndex = 0;
				selectedAdapter = null;
				return;
			}
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
			gitManager.Repository.Config.Set("credential.helper",GetAdapterId(selectedAdapter));
		}

		private void ResetSelectedAdapter(ICredentialsAdapter lastAdapter)
		{
			if(lastAdapter == null || gitCredentials == null) return;
			foreach (var credential in gitCredentials)
			{
				lastAdapter.DeleteCredentials(credential.URL);
				credential.SetHasPassword(false);
			}
		}
		#endregion

		private bool IsValid(ICredentialsAdapter adapter)
		{
			string adapterId = GetAdapterId(adapter);
			return adapterId.Equals(gitSettings.CredentialsManager,StringComparison.InvariantCultureIgnoreCase);
		}

		private string GetAdapterName(ICredentialsAdapter adapter)
		{
			if (adapter == null) return "None";
			CredentialsAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			if (attribute == null) return "";
			return attribute.Name;
		}

		private string GetAdapterId(ICredentialsAdapter adapter)
		{
			if (adapter == null) return "";
			CredentialsAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			if (attribute == null) return "";
			return attribute.Id;
		}

		internal Credentials FetchChangesAutoCredentialHandler(string url, string user, SupportedCredentialTypes supported)
		{
			if (supported == SupportedCredentialTypes.UsernamePassword)
			{
				if (gitCredentials != null)
				{
					string username = user;
					string password = string.Empty;

					LoadCredentials(url,ref username,ref password,true);

					return new UsernamePasswordCredentials()
					{
						Username = username,
						Password = password
					};
				}
			}
			return new DefaultCredentials();
		}

		internal void LoadCredentials(string url,ref string username, ref string password, bool addEntryIfMissing)
		{
			var entry = gitCredentials.GetEntry(url);

			if (addEntryIfMissing && entry == null)
			{
				entry = CreatEntry(url,username,"");
				entry.URL = url;
				entry.SetUsername(username);
				entry.Name = url;
				gitCredentials.MarkDirty();
			}
			else if (entry != null)
			{
				username = entry.Username;
				password = LoadPassword(entry);
			}
		}

		internal void DeleteCredentials(string url)
		{
			var entry = gitCredentials.GetEntry(url);

			if (SeletedAdapter != null)
			{
				try
				{
					SeletedAdapter.DeleteCredentials(SeletedAdapter.FormatUrl(url));
				}
				catch (Exception e)
				{
					Debug.LogError("There was an error while trying to remove credentials form " + GetAdapterName(SeletedAdapter));
					Debug.LogException(e);
				}
			}

			if (entry != null)
			{
				gitCredentials.RemoveEntry(entry);
			}
		}

		internal void ClearCredentialPassword(string url)
		{
			var entry = gitCredentials.GetEntry(url);

			if (SeletedAdapter != null)
			{
				try
				{
					SeletedAdapter.DeleteCredentials(SeletedAdapter.FormatUrl(url));
				}
				catch (Exception e)
				{
					Debug.LogError("There was an error while trying to remove credentials form " + GetAdapterName(SeletedAdapter));
					Debug.LogException(e);
				}
			}

			if (entry != null)
			{
				entry.ClearPassword();
				entry.SetHasPassword(false);
			}
		}

		internal GitCredential CreatEntry(string url,string username,string password)
		{
			GitCredential entry = gitCredentials.GetEntry(url);
			if (entry != null) return null;
			entry = new GitCredential() {URL = url};
			entry.SetUsername(username);
			gitCredentials.AddEntry(entry);
			if (!string.IsNullOrEmpty(password))
			{
				SetNewPassword(url, username, password);
			}
			return entry;
		}

		internal string LoadPassword(GitCredential entry)
		{
			string pass = null;

			if (SeletedAdapter != null)
			{
				try
				{
					if (!SeletedAdapter.LoadPassword(SeletedAdapter.FormatUrl(entry.URL), ref pass))
					{
						Debug.LogFormat("Could not load password with URL: {0} from {1}",entry.URL,GetAdapterName(SeletedAdapter));
					}
				}
				catch (Exception e)
				{
					Debug.LogError("There was an error while trying to load credentials from Windows Credentials Manager");
					Debug.LogException(e);
				}
			}

			return pass ?? entry.DecryptPassword();
		}

		internal void SetNewUsername(string url, string user)
		{
			if (SeletedAdapter != null)
			{
				try
				{
					if (!SeletedAdapter.SaveUsername(SeletedAdapter.FormatUrl(url), user))
					{
						Debug.LogErrorFormat("Could not save new Username to {0} with URL: {1}", GetAdapterName(SeletedAdapter), SeletedAdapter.FormatUrl(url));
						return;
					}
				}
				catch (Exception e)
				{
					Debug.LogError("There was a problem while trying to save credentials to " + GetAdapterName(SeletedAdapter));
					Debug.LogException(e);
				}
			}

			var entry = gitCredentials.GetEntry(url);
			if (entry != null)
			{
				entry.SetUsername(user);
				gitCredentials.MarkDirty();
			}
		}

		internal void SetNewPassword(string url,string user, string password)
		{
			var entry = gitCredentials.GetEntry(url);

			if (SeletedAdapter != null)
			{
				try
				{
					if (!SeletedAdapter.SavePassword(SeletedAdapter.FormatUrl(url), user, password, true))
					{
						Debug.LogErrorFormat("Could not save new Password to {0} with URL: {1}", GetAdapterName(SeletedAdapter), SeletedAdapter.FormatUrl(url));
						return;
					}

					if (entry != null)
					{
						entry.SetHasPassword(true);
					}
				}
				catch (Exception e)
				{
					Debug.LogError("There was a problem while trying to save credentials to " + GetAdapterName(SeletedAdapter));
					Debug.LogException(e);
				}
				return;
			}

			
			if (entry != null)
			{
				entry.EncryptPassword(password);
				gitCredentials.MarkDirty();
			}
		}

		public GitCredentialsJson GitCredentials
		{
			get { return gitCredentials; }
			internal set { gitCredentials = value; }
		}

		public string CredentialsFilePath
		{
			get
			{
				return UniGitPath.Combine(gitManager.GitFolderPath, "UniGit", "Credentials.json");
			}
		}

		public GUIContent[] AdapterNames
		{
			get { return adapterNames; }
		}

		public string[] AdapterIds
		{
			get { return adapterIds; }
		}
	}
}