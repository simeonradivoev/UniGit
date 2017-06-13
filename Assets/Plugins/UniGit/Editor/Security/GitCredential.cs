using System;
using UnityEngine;

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
		private string username;
		[SerializeField]
		private string password;
		[SerializeField]
		private bool isToken;
		[SerializeField]
		private string newPassword;
		[SerializeField]
		private bool hasPassword;

		public GitCredential()
		{
		}

		public GitCredential(GitCredentials.Entry c)
		{
			this.name = c.name;
			this.url = c.url;
			this.username = c.username;
			this.password = c.password;
			this.isToken = c.isToken;
			this.newPassword = c.newPassword;
			this.hasPassword = c.hasPassword;
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

		public bool IsToken
		{
			get { return isToken; }
			set { isToken = value; }
		}

		public string NewPassword
		{
			get { return newPassword; }
			set { newPassword = value; }
		}

		public string Username
		{
			get { return username; }
		}

		public bool HasPassword
		{
			get
			{
				if (!hasPassword)
				{
					return !string.IsNullOrEmpty(password);
				}
				return true;
			}
		}

		internal void SetHasPassword(bool hasPassword)
		{
			this.hasPassword = hasPassword;
		}

		public void SetUsername(string username)
		{
			this.username = username;
		}

		public void EncryptPassword(string password)
		{
			this.password = DPAPI.Encrypt(DPAPI.KeyType.UserKey, password, Application.dataPath);
		}

		public void ClearPassword()
		{
			this.password = string.Empty;
		}

		public string DecryptPassword()
		{
			string decrypredPassword;
			if (string.IsNullOrEmpty(password)) return "";
			return DPAPI.Decrypt(password, Application.dataPath, out decrypredPassword);
		}
	}
}