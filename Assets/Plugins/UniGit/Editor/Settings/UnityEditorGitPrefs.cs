using UnityEditor;

namespace UniGit.Settings
{
	public class UnityEditorGitPrefs : IGitPrefs
	{
		public void DeleteAll()
		{
			EditorPrefs.DeleteAll();
		}

		public void DeleteKey(string key)
		{
			EditorPrefs.DeleteKey(key);
		}

		public bool GetBool(string key)
		{
			return EditorPrefs.GetBool(key);
		}

		public bool GetBool(string key, bool def)
		{
			return EditorPrefs.GetBool(key, def);
		}

		public float GetFloat(string key)
		{
			return EditorPrefs.GetFloat(key);
		}

		public float GetFloat(string key, float def)
		{
			return EditorPrefs.GetFloat(key, def);
		}

		public int GetInt(string key)
		{
			return EditorPrefs.GetInt(key);
		}

		public int GetInt(string key, int def)
		{
			return EditorPrefs.GetInt(key, def);
		}

		public string GetString(string key)
		{
			return EditorPrefs.GetString(key);
		}

		public string GetString(string key, string def)
		{
			return EditorPrefs.GetString(key, def);
		}

		public bool HasKey(string key)
		{
			return EditorPrefs.HasKey(key);
		}

		public void SetBool(string key, bool value)
		{
			EditorPrefs.SetBool(key, value);
		}

		public void SetFloat(string key, float value)
		{
			EditorPrefs.SetFloat(key, value);
		}

		public void SetInt(string key, int value)
		{
			EditorPrefs.SetInt(key, value);
		}

		public void SetString(string key, string value)
		{
			EditorPrefs.SetString(key, value);
		}
	}
}