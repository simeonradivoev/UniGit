using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UniGit.Security;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
#pragma warning disable 618 //GitCredentials obsolete

namespace UniGit
{
	public class GitCredentialsManager : IDisposable
	{
		//codes for file encryption. 
		//This is just a simple protection step to stop ordinary users from snooping around.
		private const string KeyOne = "UniGitPl";
		private const string KeyTwo = "Unity3DS";

		private readonly ICredentialsAdapter[] adapters;
        private ICredentialsAdapter selectedAdapter;
		private int selectedAdapterIndex = -1;
		private bool initiazlitedSelected;
        private readonly GitManager gitManager;
		private readonly GitSettingsJson gitSettings;
		private readonly GitCallbacks gitCallbacks;
		private readonly ILogger logger;
		private readonly UniGitPaths paths;

		[UniGitInject]
		public GitCredentialsManager(GitManager gitManager,
			GitSettingsJson gitSettings,
			List<ICredentialsAdapter> adapters, 
			GitCallbacks gitCallbacks,
			ILogger logger,
			UniGitPaths paths)
		{
			this.paths = paths;
			this.gitSettings = gitSettings;
			this.gitManager = gitManager;
			this.adapters = adapters.ToArray();
			this.gitCallbacks = gitCallbacks;
			this.logger = logger;
			AdapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
			AdapterIds = adapters.Select(GetAdapterId).ToArray();

			gitCallbacks.EditorUpdate += EditorUpdate;

			LoadGitCredentials();

		}

		private void EditorUpdate()
		{
			if (GitCredentials.IsDirty)
			{
				GitCredentials.ResetDirty();
				SaveCredentialsToFile(GitCredentials);
			}
		}

		private void LoadGitCredentials()
		{
			var credentialsFilePath = paths.CredentialsFilePath;

			GitCredentialsJson credentialsJson = null;
			if (File.Exists(credentialsFilePath))
			{
				try
				{
					try
                    {
                        using var fileStream = new FileStream(credentialsFilePath,FileMode.Open,FileAccess.Read);
                        using var p = new DESCryptoServiceProvider();
                        using var dec = p.CreateDecryptor(Encoding.ASCII.GetBytes(KeyOne),Encoding.ASCII.GetBytes(KeyTwo));
                        using var cryptoStream = new CryptoStream(fileStream,dec,CryptoStreamMode.Read);
                        using var streamReader = new StreamReader(cryptoStream);

                        var text = streamReader.ReadToEnd();
                        credentialsJson = JsonUtility.FromJson<GitCredentialsJson>(text);
                    }
					catch
                    {
                        using var fileStream = new FileStream(credentialsFilePath,FileMode.Open,FileAccess.Read);
                        using var streamReader = new StreamReader(fileStream);
                        credentialsJson = JsonUtility.FromJson<GitCredentialsJson>(streamReader.ReadToEnd());
                    }
				}
				catch (Exception e)
				{
					logger.Log(LogType.Error,"Could not deserialize git settings. Creating new settings.");
					logger.LogException(e);
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

			GitCredentials = credentialsJson;
		}

		private void ImportFromOldCredentials()
		{
			var oldCredentialsFile = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
			if (oldCredentialsFile != null)
			{
				GitCredentials.Copy(oldCredentialsFile);
				logger.Log(LogType.Log,"Old Git Credentials transferred to new json credentials file. Old credentials file can now safely be removed.");
			}
			SaveCredentialsToFile(GitCredentials);
		}

		private void SaveCredentialsToFile(GitCredentialsJson credentials)
		{
            if (ValidateCredentialsPath())
            {
	            var credentialsFilePath = paths.CredentialsFilePath;

	            try
	            {
		            var json = JsonUtility.ToJson(credentials);

                    using var fileStream = new FileStream(credentialsFilePath, FileMode.OpenOrCreate, FileAccess.Write);
                    using var p = new DESCryptoServiceProvider();
                    using var dec = p.CreateEncryptor(Encoding.ASCII.GetBytes(KeyOne), Encoding.ASCII.GetBytes(KeyTwo));
                    using var cryptoStream = new CryptoStream(fileStream, dec, CryptoStreamMode.Write);
                    using var streamWriter = new StreamWriter(cryptoStream);
                    streamWriter.Write(json);
                }
	            catch (Exception e)
	            {
		            logger.LogFormat(LogType.Error, "Could not serialize GitCredentialsJson to json file at: {0}", credentialsFilePath);
		            logger.LogException(e);
	            }
            }
		}

		private bool ValidateCredentialsPath()
		{
			var settingsFileDirectory = paths.SettingsFolderPath;
			if (string.IsNullOrEmpty(settingsFileDirectory))
			{
				return true;
			}

            if (Directory.Exists(settingsFileDirectory)) return false;
            Directory.CreateDirectory(settingsFileDirectory);
            return true;
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
				if (selectedAdapterIndex >= 0 && selectedAdapterIndex < AdapterNames.Length)
				{
					return AdapterNames[selectedAdapterIndex].text;
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
			SetSelectedAdapter(Array.IndexOf(adapters, adapters.FirstOrDefault(IsValid)),false);
			initiazlitedSelected = true;
		}

		internal void SetSelectedAdapter(int index,bool deletePasswords)
		{
			if (index >= adapters.Length || index < 0)
			{
				gitManager.Repository.Config.Set("credential.helper","");
				if(deletePasswords) ResetSelectedAdapter(selectedAdapter);
				gitSettings.CredentialsManager = "";
				gitSettings.MarkDirty();
				selectedAdapterIndex = -1;
				selectedAdapter = null;
				return;
			}
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
			gitSettings.CredentialsManager = GetAdapterId(selectedAdapter);
			gitManager.Repository.Config.Set("credential.helper",GetAdapterId(selectedAdapter));
			gitSettings.MarkDirty();
		}

		private void ResetSelectedAdapter(ICredentialsAdapter lastAdapter)
		{
			if(lastAdapter == null || GitCredentials == null) return;
			foreach (var credential in GitCredentials)
			{
				lastAdapter.DeleteCredentials(credential.URL);
			}
		}
		#endregion

		private bool IsValid(ICredentialsAdapter adapter)
		{
			var adapterId = GetAdapterId(adapter);
			return adapterId.Equals(gitSettings.CredentialsManager,StringComparison.InvariantCultureIgnoreCase);
		}

		private string GetAdapterName(ICredentialsAdapter adapter)
		{
			if (adapter == null) return "None";
			var attribute = adapter.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			return attribute == null ? "" : attribute.Name;
        }

		private string GetAdapterId(ICredentialsAdapter adapter)
		{
            var attribute = adapter?.GetType().GetCustomAttributes(typeof(CredentialsAdapterAttribute), false).FirstOrDefault() as CredentialsAdapterAttribute;
			return attribute == null ? "" : attribute.Id;
        }

		internal Credentials FetchChangesAutoCredentialHandler(string url, string user, SupportedCredentialTypes supported)
		{
			return DefaultCredentialsHandler(url, user, null, supported, false);
		}

		internal Credentials DefaultCredentialsHandler(string url, string existingUsername, SecureString existingPassword, SupportedCredentialTypes supported,bool isToken)
		{
			if (supported == SupportedCredentialTypes.UsernamePassword)
			{
				var finalUsername = existingUsername;
				var finalPassword = existingPassword;

				if (GitCredentials != null)
				{
                    var loadedUsername = "";
                    LoadCredentials(url,ref loadedUsername,out var loadedPass,out var loadedIsToken,true);

					if (string.IsNullOrEmpty(finalUsername))
						finalUsername = loadedUsername;
					if (finalPassword == null || finalPassword.Length <= 0)
						finalPassword = loadedPass;

					isToken |= loadedIsToken;
				}

				return new SecureUsernamePasswordCredentials()
				{
					Username = finalUsername,
					Password = isToken ? new SecureString() : finalPassword
				};
			}
			return new DefaultCredentials();
		}

		internal void LoadCredentials(string url,ref string username, out SecureString password,out bool isToken, bool addEntryIfMissing)
		{
			var entry = GitCredentials.GetEntry(url);

			if (addEntryIfMissing && entry == null)
			{
				entry = CreateEntry(url,username);
				entry.URL = url;
				entry.Name = url;
				GitCredentials.MarkDirty();
			}
			else if (entry != null)
			{
				username = LoadUsername(entry);
				password = LoadPassword(entry);
				isToken = entry.IsToken;
				return;
			}
			password = new SecureString();
			isToken = false;
		}

		internal void RemoveCredentials(GitCredential credential,bool removeFromManager)
		{
			if (removeFromManager)
			{
				if (SeletedAdapter != null)
				{
					try
					{

					}
					catch (Exception e)
					{
						logger.LogFormat(LogType.Error,"There was an error while trying to remove credentials form {0}",GetAdapterName(SeletedAdapter));
						logger.LogException(e);
					}
				}
			}
			GitCredentials.RemoveEntry(credential);
			GitCredentials.MarkDirty();
		}

		internal void ClearCredentialPassword(GitCredential entry)
		{
			entry.ClearPassword();
			GitCredentials.MarkDirty();
		}

		internal GitCredential CreateEntry(string url,string username)
		{
			var entry = GitCredentials.GetEntry(url);
			if (entry != null) return null;
			entry = new GitCredential() {URL = url};
			entry.SetUsername(username);
			GitCredentials.AddEntry(entry);
			return entry;
		}

		internal string LoadUsername(GitCredential entry)
		{
            if (SeletedAdapter == null) return entry.Username;
            try
            {
                if (SeletedAdapter.LoadUsername(entry.ManagerUrl, out var username))
                {
                    return username;
                }
            }
            catch (Exception e)
            {
                logger.Log(LogType.Error,"There was an error while trying to load credentials from Windows Credentials Manager");
                logger.LogException(e);
            }

            return "";

        }

		internal SecureString LoadPassword(GitCredential entry)
		{
            if (SeletedAdapter == null) return entry.DecryptPassword();
            try
            {
                if (SeletedAdapter.LoadPassword(entry.ManagerUrl, out var pass))
                {
                    return pass;
                }
            }
            catch (Exception e)
            {
                logger.Log(LogType.Error,"There was an error while trying to load credentials from Windows Credentials Manager");
                logger.LogException(e);
            }

            return new SecureString();

        }

		internal void SetNewUsername(GitCredential entry,string username)
		{
			if (SeletedAdapter != null)
			{
				try
				{
					if (!SeletedAdapter.SaveUsername(entry.ManagerUrl, username))
					{
						logger.LogFormat(LogType.Error,"Could not set new Username to {0} with URL: {1}", GetAdapterName(SeletedAdapter), entry.ManagerUrl);
						return;
					}
				}
				catch (Exception e)
				{
					logger.LogFormat(LogType.Error,"There was a problem while trying to set username to {0}",GetAdapterName(SeletedAdapter));
					logger.LogException(e);
				}
				return;
			}

            if (entry == null) return;
            entry.SetUsername(username);
            GitCredentials.MarkDirty();
        }

		internal void SetNewPassword(GitCredential entry, SecureString password)
		{
			if (SeletedAdapter != null)
			{
				try
				{
					if (!SeletedAdapter.SavePassword(entry.ManagerUrl, null, password, false))
					{
						logger.LogFormat(LogType.Error,"Could not save new Password to {0} with URL: {1}", GetAdapterName(SeletedAdapter), entry.ManagerUrl);
						return;
					}
				}
				catch (Exception e)
				{
					logger.LogFormat(LogType.Error,"There was a problem while trying to save credentials to {0}",GetAdapterName(SeletedAdapter));
					logger.LogException(e);
				}
				return;
			}

            if (entry == null) return;
            entry.EncryptPassword(password);
            GitCredentials.MarkDirty();
        }

		internal void CreateNewExternal(string url, string username, SecureString password)
		{
			try
			{
				if (!SeletedAdapter.SavePassword(url, username, password, true))
				{
					logger.LogFormat(LogType.Error,"Could not create new Entry to with URL: {0} and Username: {1} in {2}",url,username, GetAdapterName(SeletedAdapter));
					return;
				}
			}
			catch (Exception e)
			{
				logger.LogFormat(LogType.Error,"There was a problem while trying to save credentials to {0}",GetAdapterName(SeletedAdapter));
				logger.LogException(e);
			}
        }

		internal bool HasPassword(GitCredential entry)
		{
			if (SeletedAdapter != null)
			{
				if (string.IsNullOrEmpty(entry.Username))
				{
					return SeletedAdapter.Exists(entry.ManagerUrl);
				}
				return SeletedAdapter.Exists(entry.ManagerUrl,entry.Username);
			}

			return entry != null && entry.HasStoredPassword;
		}

		internal string GetFormatedUrl(string url)
        {
            return SeletedAdapter != null ? SeletedAdapter.FormatUrl(url) : url;
        }

		public void Dispose()
		{
			if(gitCallbacks != null) gitCallbacks.EditorUpdate -= EditorUpdate;
		}

		public GitCredentialsJson GitCredentials { get; internal set; }

        public GUIContent[] AdapterNames { get; }

        public string[] AdapterIds { get; }
    }
}