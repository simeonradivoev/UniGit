using System;
using UniGit.Attributes;

namespace UniGit.Adapters
{
#if UNITY_EDITOR_WIN
	using CredentialManagement;

	[CredentialsAdapter("wincred","Windows Credentials Manager")]
	public class WincredCredentialsAdapter : ICredentialsAdapter
	{
		public void DeleteCredentials(string url)
		{
			using (var credential = new Credential(null, null, url))
			{
				credential.Delete();
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
	}
#endif
}