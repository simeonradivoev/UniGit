using System.Collections.Generic;

namespace UniGit.Settings
{
	public class GitPrefs : IGitPrefs
	{
		private Dictionary<string, bool> bools;
		private Dictionary<string, float> floats;
		private Dictionary<string, int> ints;
		private Dictionary<string,string> strings;

		public GitPrefs()
		{
			bools = new Dictionary<string, bool>();
			floats = new Dictionary<string, float>();
			ints = new Dictionary<string, int>();
			strings = new Dictionary<string, string>();
		}

		public void DeleteAll()
		{
			bools.Clear();
			floats.Clear();
			ints.Clear();
			strings.Clear();
		}

		public void DeleteKey(string key)
		{
			bools.Remove(key);
			floats.Remove(key);
			ints.Remove(key);
			strings.Remove(key);
		}

		public bool GetBool(string key)
		{
			bool value;
			if (bools.TryGetValue(key,out value))
			{
				return value;
			}
			return value;
		}

		public bool GetBool(string key, bool def)
		{
			bool value;
			if (bools.TryGetValue(key, out value))
			{
				return value;
			}
			return def;
		}

		public float GetFloat(string key)
		{
			float value;
			if (floats.TryGetValue(key, out value))
			{
				return value;
			}
			return value;
		}

		public float GetFloat(string key, float def)
		{
			float value;
			if (floats.TryGetValue(key, out value))
			{
				return value;
			}
			return def;
		}

		public int GetInt(string key)
		{
			int value;
			if (ints.TryGetValue(key, out value))
			{
				return value;
			}
			return value;
		}

		public int GetInt(string key, int def)
		{
			int value;
			if (ints.TryGetValue(key, out value))
			{
				return value;
			}
			return def;
		}

		public string GetString(string key, string def)
		{
			string value;
			if (strings.TryGetValue(key, out value))
			{
				return value;
			}
			return def;
		}

		public string GetString(string key)
		{
			string value;
			if (strings.TryGetValue(key, out value))
			{
				return value;
			}
			return value;
		}

		public bool HasKey(string key)
		{
			if (bools.ContainsKey(key)) return true;
			if (floats.ContainsKey(key)) return true;
			if (ints.ContainsKey(key)) return true;
			if (strings.ContainsKey(key)) return true;
			return false;
		}

		public void SetBool(string key, bool value)
		{
			bools[key] = value;
		}

		public void SetFloat(string key, float value)
		{
			floats[key] = value;
		}

		public void SetInt(string key, int value)
		{
			ints[key] = value;
		}

		public void SetString(string key, string value)
		{
			strings[key] = value;
		}
	}
}