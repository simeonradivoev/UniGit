using System;
using System.Linq;
using System.Security;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UnityEditor;
using UnityEditor.Animations;
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

		internal static void Load()
		{
			adapters = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => typeof(ICredentialsAdapter).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)).Select(t => Activator.CreateInstance(t)).Cast<ICredentialsAdapter>().ToArray();
			adapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
			adapterIds = adapters.Select(a => GetAdapterId(a)).ToArray();
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

		private static void InitializeSelectedAdapter()
		{
			SetSelectedAdapter(Array.IndexOf(adapters, adapters.FirstOrDefault(a => IsValid(a))));
			initiazlitedSelected = true;
		}

		internal static void SetSelectedAdapter(int index)
		{
			if (index >= adapters.Length || index < 0 || selectedAdapterIndex == index)
			{
				GitManager.Repository.Config.Set("credential.helper","");
				ResetSelectedAdapter(selectedAdapter);
				selectedAdapterIndex = -1;
				selectedAdapter = null;
				return;
			}
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
			GitManager.Repository.Config.Set("credential.helper",GetAdapterId(selectedAdapter));
		}

		private static void ResetSelectedAdapter(ICredentialsAdapter lastAdapter)
		{
			if(lastAdapter == null) return;
			foreach (var credential in GitManager.GitCredentials)
			{
				lastAdapter.DeleteCredentials(credential.URL);
				credential.SetHasPassword(false);
			}
		}

		private static void UpdateSelectedAdaptor(ICredentialsAdapter adapter)
		{
			foreach (var credential in GitManager.GitCredentials)
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
			return attribute.Id.Equals(GitManager.Settings.CredentialsManager,StringComparison.InvariantCultureIgnoreCase);
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
				if (GitManager.GitCredentials != null)
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
			var entry = GitManager.GitCredentials.GetEntry(url);

			if (addEntryIfMissing && entry == null)
			{
				entry = CreatEntry(url,username,"");
				entry.URL = url;
				entry.SetUsername(username);
				entry.Name = url;
				EditorUtility.SetDirty(GitManager.GitCredentials);
				AssetDatabase.SaveAssets();
			}
			else if (entry != null)
			{
				username = entry.Username;
				password = LoadPassword(entry);
			}
		}

		internal static void DeleteCredentials(string url)
		{
			var entry = GitManager.GitCredentials.GetEntry(url);

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
				GitManager.GitCredentials.RemoveEntry(entry);
			}
		}

		internal static void ClearCredentialPassword(string url)
		{
			var entry = GitManager.GitCredentials.GetEntry(url);

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

		internal static GitCredentials.Entry CreatEntry(string url,string username,string password)
		{
			GitCredentials.Entry entry = GitManager.GitCredentials.GetEntry(url);
			if (entry != null) return null;
			entry = new GitCredentials.Entry() {URL = url};
			entry.SetUsername(username);
			GitManager.GitCredentials.AddEntry(entry);
			SetNewPassword(url,username,password);
			return entry;
		}

		internal static string LoadPassword(GitCredentials.Entry entry)
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

			var entry = GitManager.GitCredentials.GetEntry(url);
			if (entry != null)
			{
				entry.SetUsername(user);
				EditorUtility.SetDirty(GitManager.GitCredentials);
				AssetDatabase.SaveAssets();
			}
		}

		internal static void SetNewPassword(string url,string user, string password)
		{
			var entry = GitManager.GitCredentials.GetEntry(url);

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
				EditorUtility.SetDirty(GitManager.GitCredentials);
				AssetDatabase.SaveAssets();
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