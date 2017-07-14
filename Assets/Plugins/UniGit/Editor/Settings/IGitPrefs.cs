namespace UniGit.Settings
{
	public interface IGitPrefs
	{
		void DeleteAll();
		void DeleteKey(string key);
		bool GetBool(string key);
		bool GetBool(string key, bool def);
		float GetFloat(string key);
		float GetFloat(string key, float def);
		int GetInt(string key);
		int GetInt(string key, int def);
		string GetString(string key);
		string GetString(string key, string def);
		bool HasKey(string key);
		void SetBool(string key, bool value);
		void SetFloat(string key, float value);
		void SetInt(string key, int value);
		void SetString(string key, string value);
	}
}