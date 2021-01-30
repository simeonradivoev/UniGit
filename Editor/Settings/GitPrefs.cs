using System;
using System.Collections.Generic;
using UniGit.Utils;

namespace UniGit.Settings
{
	public class GitPrefs : IGitPrefs, IDisposable
	{
		private readonly GitCallbacks gitCallbacks;
		private readonly Dictionary<string, bool> bools;
		private readonly Dictionary<string, float> floats;
		private readonly Dictionary<string, int> ints;
		private readonly Dictionary<string,string> strings;
		private bool dirty;

		[UniGitInject]
		public GitPrefs(GitCallbacks gitCallbacks)
		{
			this.gitCallbacks = gitCallbacks;
			bools = new Dictionary<string, bool>();
			floats = new Dictionary<string, float>();
			ints = new Dictionary<string, int>();
			strings = new Dictionary<string, string>();
			gitCallbacks.EditorUpdate += OnEditorUpdate;
		}

		public void OnEditorUpdate()
        {
            if (!dirty) return;
            dirty = false;
            gitCallbacks.IssueOnPrefsChange(this);
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
            return bools.TryGetValue(key,out var value) && value;
        }

		public bool GetBool(string key, bool def)
        {
            return bools.TryGetValue(key, out var value) ? value : def;
        }

		public float GetFloat(string key)
        {
            floats.TryGetValue(key, out var value);
            return value;
        }

		public float GetFloat(string key, float def)
        {
            return floats.TryGetValue(key, out var value) ? value : def;
        }

		public int GetInt(string key)
        {
            ints.TryGetValue(key, out var value);
            return value;
        }

		public int GetInt(string key, int def)
        {
            return ints.TryGetValue(key, out var value) ? value : def;
        }

		public string GetString(string key, string def)
        {
            return strings.TryGetValue(key, out var value) ? value : def;
        }

		public string GetString(string key)
        {
            return strings.TryGetValue(key, out var value) ? value : null;
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
			dirty = true;
		}

		public void SetFloat(string key, float value)
		{
			floats[key] = value;
			dirty = true;
		}

		public void SetInt(string key, int value)
		{
			ints[key] = value;
			dirty = true;
		}

		public void SetString(string key, string value)
		{
			strings[key] = value;
			dirty = true;
		}

		public void Dispose()
		{
			gitCallbacks.EditorUpdate -= OnEditorUpdate;
		}
	}
}