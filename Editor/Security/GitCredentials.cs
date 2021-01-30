using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using JetBrains.Annotations;
using UnityEngine;

namespace UniGit
{
	[Obsolete("Use 'GitCredentialsJson' instead")]
	public class GitCredentials : ScriptableObject, IEnumerable<GitCredentials.Entry>
	{
		[SerializeField] private List<Entry> entries;

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
            return entries?.FirstOrDefault(r => r.URL.Equals(url,StringComparison.InvariantCultureIgnoreCase));
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

		public List<Entry> Entries => entries;

        [Serializable]
		public class Entry
		{
			[SerializeField] internal string name;
			[SerializeField] internal string url;
			[SerializeField] internal string username;
			[SerializeField] internal string password;
			[SerializeField] internal bool isToken;
			[SerializeField] internal string newPassword;
			[SerializeField] internal bool hasPassword;

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

			public bool IsToken
			{
				get => isToken;
                set => isToken = value;
            }

			public string NewPassword
			{
				get => newPassword;
                set => newPassword = value;
            }

			public string Username => username;

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
}