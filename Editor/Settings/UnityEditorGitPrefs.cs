using System;
using UniGit.Utils;
using UnityEditor;

namespace UniGit.Settings
{
	public class UnityEditorGitPrefs : IGitPrefs, IDisposable
	{
		public const string DisablePostprocess = "UniGit_DisablePostprocess";
		private readonly GitCallbacks gitCallbacks;
		private bool dirty;

		[UniGitInject]
		public UnityEditorGitPrefs(GitCallbacks gitCallbacks)
		{
			this.gitCallbacks = gitCallbacks;
			gitCallbacks.EditorUpdate += OnEditorUpdate;
		}

		public void OnEditorUpdate()
        {
            if (!dirty) return;
            dirty = false;
            gitCallbacks.IssueOnPrefsChange(this);
        }

		public void DeleteAll() => EditorPrefs.DeleteAll();

        public void DeleteKey(string key) => EditorPrefs.DeleteKey(key);

        public bool GetBool(string key) => EditorPrefs.GetBool(key);

        public bool GetBool(string key, bool def) => EditorPrefs.GetBool(key, def);

        public float GetFloat(string key) => EditorPrefs.GetFloat(key);

        public float GetFloat(string key, float def) => EditorPrefs.GetFloat(key, def);

        public int GetInt(string key) => EditorPrefs.GetInt(key);

        public int GetInt(string key, int def) => EditorPrefs.GetInt(key, def);

        public string GetString(string key) => EditorPrefs.GetString(key);

        public string GetString(string key, string def) => EditorPrefs.GetString(key, def);

        public bool HasKey(string key) => EditorPrefs.HasKey(key);

        public void SetBool(string key, bool value)
		{
			EditorPrefs.SetBool(key, value);
			dirty = true;
		}

		public void SetFloat(string key, float value)
		{
			EditorPrefs.SetFloat(key, value);
			dirty = true;
		}

		public void SetInt(string key, int value)
		{
			EditorPrefs.SetInt(key, value);
			dirty = true;
		}

		public void SetString(string key, string value)
		{
			EditorPrefs.SetString(key, value);
			dirty = true;
		}

		public void Dispose()
		{
			gitCallbacks.EditorUpdate -= OnEditorUpdate;
		}
	}
}