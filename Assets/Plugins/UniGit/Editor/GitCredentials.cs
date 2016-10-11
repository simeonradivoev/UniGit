using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace UniGit
{
	public class GitCredentials : ScriptableObject, IEnumerable<GitCredentials.Entry>
	{
		[SerializeField,HideInInspector] private List<Entry> entries;

		[UsedImplicitly]
		private void OnEnable()
		{
			if (entries == null)
			{
				entries = new List<Entry>();
			}
		}

		public Entry GetEntry(string url)
		{
			if (entries == null) return null;
			return entries.FirstOrDefault(r => r.URL.Equals(url,StringComparison.InvariantCultureIgnoreCase));
		}

		internal void AddEntry(Entry entry)
		{
			entries.Add(entry);
		}

		internal void RemoveEntry(Entry entry)
		{
			entries.Remove(entry);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<Entry> GetEnumerator()
		{
			return entries.GetEnumerator();
		}

		[Serializable]
		public class Entry
		{
			[SerializeField] private string name;
			[SerializeField] private string url;
			[SerializeField] private string username;
			[SerializeField] private string password;
			[SerializeField] private bool isToken;
			[SerializeField] private string newPassword;
			[SerializeField] private bool hasPassword;

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
}