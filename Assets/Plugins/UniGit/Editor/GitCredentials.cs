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
			return entries.FirstOrDefault(r => r.URL.Equals(url,StringComparison.InvariantCultureIgnoreCase));
		}


		public Entry CreateEntry()
		{
			Entry entry = new Entry();
			entries.Add(entry);
			return entry;
		}

		public void RemoveEntry(Entry entry)
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
			[SerializeField] private string token;
			private string newPassword;

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

			public string Username
			{
				get { return username; }
				set { username = value; }
			}

			public string NewPassword
			{
				get { return newPassword; }
				set { newPassword = value; }
			}

			public bool IsToken
			{
				get { return isToken; }
				set { isToken = value; }
			}

			public string Token
			{
				get { return token; }
				set { token = value; }
			}

			public void EncryptPassword(string password)
			{
				this.password = DPAPI.Encrypt(DPAPI.KeyType.UserKey, password, Application.dataPath);
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