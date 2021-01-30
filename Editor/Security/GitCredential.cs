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
			get => name;
            set => name = value;
        }

		public string URL
		{
			get => url;
            set => url = value;
        }

		public string ManagerUrl
		{
			get => managerUrl;
            set => managerUrl = value;
        }

		public bool SpecifyManagerUsername
		{
			get => specifyManagerUsername;
            set => specifyManagerUsername = value;
        }

		public bool IsToken
		{
			get => isToken;
            set => isToken = value;
        }

		public SecureString NewPassword
		{
			get => newPassword;
            set => newPassword = value;
        }

		public string NewUsername
		{
			get => newUsername;
            set => newUsername = value;
        }

		public string Username => username;

        internal bool HasStoredPassword => !string.IsNullOrEmpty(password);

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
            return string.IsNullOrEmpty(password) ? new SecureString() : DPAPI.Decrypt(password, Application.dataPath, out _);
        }
	}
}