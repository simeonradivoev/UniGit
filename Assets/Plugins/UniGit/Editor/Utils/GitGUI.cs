using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public static class GitGUI
	{
		private static GUIContent tmpContent = new GUIContent();
		private static Stack<bool> enableStack = new Stack<bool>(); 
		private static Stack<Matrix4x4> matrixStack = new Stack<Matrix4x4>(); 

		public static GUIContent GetTempContent(Texture tex)
		{
			tmpContent.text = string.Empty;
			tmpContent.tooltip = string.Empty;
			tmpContent.image = tex;
			return tmpContent;
		}

		public static Texture IconContentTex(string name)
		{
			return EditorGUIUtility.IconContent(name).image;
		}

		public static GUIContent IconContent(string name)
		{
			var original = EditorGUIUtility.IconContent(name);
			return original;
		}

		public static GUIContent IconContent(string name, string text)
		{
			var original = EditorGUIUtility.IconContent(name);
			return new GUIContent(original) {text = text};
		}

		public static GUIContent IconContent(string name, string text,string tooltip)
		{
			var original = EditorGUIUtility.IconContent(name);
			return new GUIContent(original) { text = text,tooltip = tooltip};
		}

		public static GUIContent GetTempContent(string label)
		{
			tmpContent.text = label;
			tmpContent.tooltip = string.Empty;
			tmpContent.image = null;
			return tmpContent;
		}

		public static GUIContent GetTempContent(string label, string tooltip)
		{
			tmpContent.text = label;
			tmpContent.tooltip = tooltip;
			tmpContent.image = null;
			return tmpContent;
		}

		public static GUIContent GetTempContent(Texture tex, string label, string tooltip)
		{
			tmpContent.text = label;
			tmpContent.tooltip = tooltip;
			tmpContent.image = tex;
			return tmpContent;
		}

		public static GUIContent GetTempContent(Texture tex, string label)
		{
			tmpContent.text = label;
			tmpContent.tooltip = String.Empty;
			tmpContent.image = tex;
			return tmpContent;
		}

		public static void StartEnable(bool enabled)
		{
			StartEnable();
			GUI.enabled = enabled;
		}

		public static void StartEnable()
		{
			enableStack.Push(GUI.enabled);
		}

		public static void EndEnable()
		{
			GUI.enabled = enableStack.Pop();
		}

		public static void PushMatrix()
		{
			if (matrixStack.Count > 1000)
			{
				Debug.LogError("Matrix Stack Overflow");
				matrixStack.Clear();
			}
			matrixStack.Push(GUI.matrix);
		}

		public static void PopMatrix()
		{
			GUI.matrix = matrixStack.Pop();
		}

		public static void DrawLoading(Rect rect,GUIContent loadinContent)
		{
			const float loadinCricleSize = 24;
			Vector2 loadingLabelWidth = EditorStyles.largeLabel.CalcSize(loadinContent);
			float totalWidth = loadinCricleSize + loadingLabelWidth.x + 8;
			float totalHeight = Mathf.Max(loadingLabelWidth.y, loadinCricleSize);

			GitGUI.PushMatrix();
			Rect loadinCircleRect = new Rect(rect.x + rect.width / 2 - totalWidth / 2, rect.y + rect.height / 2 - totalHeight / 2, loadinCricleSize, loadinCricleSize);
			GUIUtility.RotateAroundPivot((float)EditorApplication.timeSinceStartup * 300, loadinCircleRect.center);
			GUI.DrawTexture(loadinCircleRect, EditorGUIUtility.FindTexture("CollabProgress"));
			GitGUI.PopMatrix();

			GUI.Label(new Rect(loadinCircleRect.x + loadinCircleRect.width + 8, loadinCircleRect.y + ((loadinCricleSize - loadingLabelWidth.y) / 2), loadingLabelWidth.x, loadingLabelWidth.y), loadinContent, EditorStyles.largeLabel);
		}

		public static void ShowNotificationOnWindow<T>(GUIContent content,bool createIfMissing) where T : EditorWindow
		{
			T window = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
			if (window == null)
			{
				if (createIfMissing)
					window = EditorWindow.GetWindow<T>();
				else
					return;
			}

			window.ShowNotification(content);
		}

		#region Config Fields
		internal static void DoConfigStringsField(Configuration configuration,GUIContent content, string key, string[] options, string def)
		{
			string oldValue = configuration.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(content);
			GUI.SetNextControlName(key + " Config Popup");
			int newValueIndex = EditorGUILayout.Popup(Array.IndexOf(options, oldValue), options);
			string newValue;
			if (newValueIndex >= 0 && newValueIndex < options.Length)
			{
				newValue = options[newValueIndex];
			}
			else
			{
				newValue = def;
			}
			EditorGUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				configuration.Set(key, newValue);
			}
		}

		internal static void DoConfigStringField(Configuration configuration, GUIContent content, string key, string def)
		{
			string oldValue = configuration.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config String");
			string newValue = EditorGUILayout.DelayedTextField(content, oldValue);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				configuration.Set(key, newValue);
			}
		}

		internal static void DoConfigIntField(Configuration configuration, GUIContent content, string key, int def)
		{
			int oldValue = configuration.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config Int");
			int newValue = EditorGUILayout.DelayedIntField(content, oldValue);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				configuration.Set(key, newValue);
			}
		}

		internal static void DoConfigIntSlider(Configuration configuration, GUIContent content, int min, int max, string key, int def)
		{
			int oldValue = configuration.GetValueOrDefault(key, def);
			EditorGUI.BeginChangeCheck();
			GUI.SetNextControlName(key + " Config Int");
			int newValue = EditorGUILayout.IntSlider(content, oldValue, min, max);
			if (EditorGUI.EndChangeCheck() && oldValue != newValue)
			{
				configuration.Set(key, newValue);
			}
		}

		internal static void DoConfigToggle(Configuration configuration, GUIContent content, string key, bool def)
		{
			bool oldValue = configuration.GetValueOrDefault(key, def);
			GUI.SetNextControlName(key + " Config Toggle");
			bool newValue = EditorGUILayout.Toggle(content, oldValue);
			if (oldValue != newValue)
			{
				configuration.Set(key, newValue);
			}
		}
		#endregion
	}
}