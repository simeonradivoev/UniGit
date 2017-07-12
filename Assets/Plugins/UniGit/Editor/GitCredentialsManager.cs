using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UniGit.Security;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public static class GitCredentialsManager
	{
		private static ICredentialsAdapter[] adapters;
		private static GUIContent[] adapterNames;
		private static string[] adapterIds;
		private static ICredentialsAdapter selectedAdapter;
		private static int selectedAdapterIndex = -1;
		private static bool initiazlitedSelected;
		private static GitCredentialsJson gitCredentials;
		private static GitManager gitManager;

		internal static void Load(GitManager gitManager)
		{
			GitCredentialsManager.gitManager = gitManager;
			adapters = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => typeof(ICredentialsAdapter).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)).Select(t => Activator.CreateInstance(t)).Cast<ICredentialsAdapter>().ToArray();
			adapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
			adapterIds = adapters.Select(GetAdapterId).ToArray();

			EditorApplication.update += EditorUpdate;

			LoadGitCredentials();
		}

		private static void EditorUpdate()
		{
			if (gitCredentials.IsDirty)
			{
				gitCredentials.ResetDirty();
				SaveCredentialsToFile(gitCredentials);
			}
		}

		private static void LoadGitCredentials()
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

		private static void ImportFromOldCredentials()
		{
			var oldCredentialsFile = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
			if (oldCredentialsFile != null)
			{
				gitCredentials.Copy(oldCredentialsFile);
				Debug.Log("Old Git Credentials transferred to new json credentials file. Old credentials file can now safely be removed.");
			}
			SaveCredentialsToFile(gitCredentials);
		}

		private static void SaveCredentialsToFile(GitCredentialsJson credentials)
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

		private static void ValidateCredentialsPath()
		{
			string settingsFileDirectory = Path.Combine(gitManager.GitFolderPath, "UniGit");
			if (!Directory.Exists(settingsFileDirectory))
			{
				Directory.CreateDirectory(settingsFileDirectory);
			}
		}

		#region Selection
		//using lazy initialization
		private static ICredentialsAdapter SeletedAdapter
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

		public static int SelectedAdapterIndex
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

		public static string SelectedAdapterName
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

		public static bool IsAdapterSelected
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

		private static void InitializeSelectedAdapter()
		{
			SetSelectedAdapter(Array.IndexOf(adapters, adapters.FirstOrDefault(a => IsValid(a))));
			initiazlitedSelected = true;
		}

		internal static void SetSelectedAdapter(int index)
		{
			if (index >= adapters.Length || index < 0 || selectedAdapterIndex == index)
			{
				gitManager.Repository.Config.Set("credential.helper","");
				ResetSelectedAdapter(selectedAdapter);
				selectedAdapterIndex = -1;
				selectedAdapter = null;
				return;
			}
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
			gitManager.Repository.Config.Set("credential.helper",GetAdapterId(selectedAdapter));
		}

		private static void ResetSelectedAdapter(ICredentialsAdapter lastAdapter)
		{
			if(lastAdapter == null || gitCredentials == null) return;
			foreach (var credential in gitCredentials)
			{
				lastAdapter.DeleteCredentials(credential.URL);
				credential.SetHasPassword(false);
			}
		}

		private static void UpdateSelectedAdaptor(ICredentialsAdapter adapter)
		{
			foreach (var credential in GitCredentials)
			{
				credential.ClearPassword();
				credential.SetHasPassword(false);
			}

		}
		#endregion

		private static bool IsValid(ICredentialsAdapter adapter)
		{
			CredentialsAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			if (attribute == null) return false;
			return attribute.Id.Equals(gitManager.Settings.CredentialsManager,StringComparison.InvariantCultureIgnoreCase);
		}

		private static string GetAdapterName(ICredentialsAdapter adapter)
		{
			CredentialsAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			if (attribute == null) return "";
			return attribute.Name;
		}

		private static string GetAdapterId(ICredentialsAdapter adapter)
		{
			CredentialsAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			if (attribute == null) return "";
			return attribute.Id;
		}

		internal static Credentials FetchChangesAutoCredentialHandler(string url, string user, SupportedCredentialTypes supported)
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

		internal static void LoadCredentials(string url,ref string username, ref string password, bool addEntryIfMissing)
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

		internal static void DeleteCredentials(string url)
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

		internal static void ClearCredentialPassword(string url)
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

		internal static GitCredential CreatEntry(string url,string username,string password)
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

		internal static string LoadPassword(GitCredential entry)
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

		internal static void SetNewUsername(string url, string user)
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

		internal static void SetNewPassword(string url,string user, string password)
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

		public static GitCredentialsJson GitCredentials
		{
			get { return gitCredentials; }
			internal set { gitCredentials = value; }
		}

		public static string CredentialsFilePath
		{
			get
			{
				return Path.Combine(gitManager.GitFolderPath, "UniGit/Credentials.json");
			}
		}

		public static GUIContent[] AdapterNames
		{
			get { return adapterNames; }
		}

		public static string[] AdapterIds
		{
			get { return adapterIds; }
		}
	}
}