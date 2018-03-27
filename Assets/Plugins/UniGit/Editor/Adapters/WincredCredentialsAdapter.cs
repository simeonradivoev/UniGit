using System;
using System.Security;
using CredentialManagement;
using UniGit.Attributes;
using UnityEngine;

namespace UniGit.Adapters
{
#if UNITY_EDITOR_WIN
	
	[CredentialsAdapter("wincred","Windows Credentials Manager")]
	public class WincredCredentialsAdapter : ICredentialsAdapter
	{
		private readonly CredentialType credentialType;
		private readonly PersistanceType persistanceType;

		public WincredCredentialsAdapter()
		{
			credentialType = CredentialType.Generic;
			persistanceType = PersistanceType.LocalComputer;
		}

		public void DeleteCredentials(string url)
		{
			var credentialSet = new CredentialSet(url);
			foreach (var c in credentialSet)
			{
				c.Delete();
			}
		}

		public string FormatUrl(string url)
		{
			Uri uri = new Uri(url);
			return "git:" + uri.GetLeftPart(UriPartial.Authority);
		}

		public bool SaveUsername(string url, string username)
		{
			using (var credential = new Credential(null, null, url) {PersistanceType = persistanceType})
			{
				if (credential.Load())
				{
					credential.Username = username;
					return credential.Save();
				}
				//Debug.LogErrorFormat("Could not load credential with url: {0} from Windows Creadentials.", url);
			}
			return false;
		}

		public bool LoadPassword(string url, out SecureString password)
		{
			using (var credential = new Credential(null, null, url))
			{
				if (credential.Load())
				{
					password = credential.SecurePassword;
					return true;
				}
			}
			password = null;
			return false;
		}

		public bool LoadPassword(string url,string username, out SecureString password)
		{
			using (var credential = new Credential(username, null, url))
			{
				if (credential.Load())
				{
					password = credential.SecurePassword;
					return true;
				}
			}
			password = null;
			return false;
		}

		public bool LoadUsername(string url, out string username)
		{
			using (var credential = new Credential(null, null, url))
			{
				if (credential.Load())
				{
					username = credential.Username;
					return true;
				}
			}
			username = null;
			return false;
		}

		public bool SavePassword(string url, string username, SecureString password, bool createMissing)
		{
			using (var credential = new Credential(username, null, url,credentialType) {PersistanceType = persistanceType})
			{
				if (credential.Load() || createMissing)
				{
					credential.SecurePassword = password;
					return credential.Save();
				}
			}
			return false;
		}

		public bool Exists(string url)
		{
			if (string.IsNullOrEmpty(url)) return false;
			using (var credentialSet = new Credential(null,null,url))
			{
				credentialSet.Load();
				return credentialSet.Exists();
			}
		}

		public bool Exists(string url,string username)
		{
			using (var credentialSet = new Credential(username,null,url))
			{
				return credentialSet.Load();
			}
		}
	}
#endif
}