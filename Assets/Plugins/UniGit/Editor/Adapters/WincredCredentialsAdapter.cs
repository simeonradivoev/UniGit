using System;
using System.Collections.Generic;
using System.Linq;
using UniGit.Attributes;
using UniGit.Security;

namespace UniGit.Adapters
{
#if UNITY_EDITOR_WIN
	using CredentialManagement;

	[CredentialsAdapter("wincred","Windows Credentials Manager")]
	public class WincredCredentialsAdapter : ICredentialsAdapter
	{
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
			using (var credential = new Credential(null, null, url) {PersistanceType = PersistanceType.LocalComputer})
			{
				if (credential.Load())
				{
					credential.Username = username;
					return credential.Save();
				}
			}
			return false;
		}

		public bool LoadPassword(string url, ref string password)
		{
			using (var credential = new Credential(null, null, url))
			{
				if (credential.Load())
				{
					password = credential.Password;
					return true;
				}
			}
			return false;
		}

		public bool SavePassword(string url, string username, string password, bool createMissing)
		{
			using (var credential = new Credential(username, null, url,CredentialType.Generic) {PersistanceType = PersistanceType.LocalComputer})
			{
				if (credential.Load() || createMissing)
				{
					credential.Password = password;
					return credential.Save();
				}
			}
			return false;
		}

		public bool Exists(string url)
		{
			var credentialSet = new CredentialSet(url);
			return credentialSet.Count > 0;
		}
	}
#endif
}