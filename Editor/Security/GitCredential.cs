using System;
using System.Security;
using UnityEngine;
#pragma warning disable 618	//GitCredentials obsolete

namespace UniGit.Security
{
	[Serializable]
	public class GitCredential
	{
		[SerializeField]
		private string name;
		[SerializeField]
		private string url;
		[SerializeField]
		private string managerUrl;
		[SerializeField]
		private string username;
		[SerializeField]
		private bool specifyManagerUsername;
		[SerializeField]
		private string password;
		[SerializeField]
		private bool isToken;

		private SecureString newPassword;
		private string newUsername;

		public GitCredential()
		{
			newPassword = new SecureString();
		}

		~GitCredential()
		{
			newPassword.Dispose();
		}

		public GitCredential(GitCredentials.Entry c)
		{
			name = c.name;
			url = c.url;
			username = c.username;
			password = c.password;
			isToken = c.isToken;
		}

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		public string URL
		{
			get { return url; }
			set { url = value; }
		}

		public string ManagerUrl
		{
			get { return managerUrl; }
			set { managerUrl = value; }
		}

		public bool SpecifyManagerUsername
		{
			get { return specifyManagerUsername; }
			set { specifyManagerUsername = value; }
		}

		public bool IsToken
		{
			get { return isToken; }
			set { isToken = value; }
		}

		public SecureString NewPassword
		{
			get { return newPassword; }
			set { newPassword = value; }
		}

		public string NewUsername
		{
			get { return newUsername; }
			set { newUsername = value; }
		}

		public string Username
		{
			get { return username; }
		}

		internal bool HasStoredPassword
		{
			get
			{
				return !string.IsNullOrEmpty(password);
			}
		}

		public void SetUsername(string username)
		{
			this.username = username;
		}

		public void EncryptPassword(SecureString password)
		{
			this.password = DPAPI.Encrypt(DPAPI.KeyType.UserKey, password, Application.dataPath);
		}

		public void ClearPassword()
		{
			this.password = string.Empty;
		}

		public SecureString DecryptPassword()
		{
			string description;	//optional
			if (string.IsNullOrEmpty(password)) return new SecureString();
			return DPAPI.Decrypt(password, Application.dataPath, out description);
		}
	}
}