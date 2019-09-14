using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public static class GitGUI
	{
		private static readonly GUIContent tmpContent = new GUIContent();
		private static readonly Stack<bool> enableStack = new Stack<bool>(); 
		private static readonly Stack<Matrix4x4> matrixStack = new Stack<Matrix4x4>();
		private static int _secureTextFieldHash = "EditorSecurePasswordField".GetHashCode();

		private static StylesClass _styles;
		private static TexturesClass _textures;
		private static ContentsClass _contents;
		public static StylesClass Styles
		{
			get
			{
				if (_styles == null)
				{
					_styles = new StylesClass();
					InitStyles(_styles);
				}
				return _styles;
			}
		}
		public static TexturesClass Textures
		{
			get
			{
				if (_textures == null)
				{
					_textures = new TexturesClass();
					InitTextures(_textures);
				}
				return _textures;
			}
		}
		public static ContentsClass Contents
		{
			get
			{
				if (_contents == null)
				{
					_contents = new ContentsClass();
					InitContents(_contents);
				}
				return _contents;
			}
		}
		public class StylesClass
		{
			public GUIStyle BigTitle;
			public GUIStyle BreadcrumMid;
			public GUIStyle SelectionBoxGlow;
			public GUIStyle GroupBox;
			public GUIStyle LightmapEditorSelectedHighlight;
			public GUIStyle IconButton;
			public GUIStyle LargeButton;
			public GUIStyle AddComponentBtn;
			public GUIStyle ShurikenModuleTitle;
			public GUIStyle ProjectBrowserHeaderBgTop;
			public GUIStyle ShurikenModuleBg;
		}

		public class TexturesClass
		{
			public Texture2D CollabPush;
			public Texture2D CollabPull;
			public Texture2D GameView;
			public Texture2D FolderIcon;
			public Texture2D OrbitTool;
			public Texture2D RotateTool;
			public Texture2D AnimationWindow;
			public Texture2D ZoomTool;
			public Texture2D[] SpinTextures;
			public Texture2D Collab;
			public Texture2D CollabNew;
			public Texture2D CollabConflict;
			public Texture2D WarrningIconSmall;
			public Texture2D ErrorIconSmall;
			public Texture2D InfoIconSmall;
		}

		public class ContentsClass
		{
			public GUIContent Help;
		}

		private static void InitStyles(StylesClass styles)
		{
			styles.BigTitle = "IN BigTitle";
			styles.BreadcrumMid = "GUIEditor.BreadcrumbMid";
			styles.SelectionBoxGlow = "TL SelectionButton PreDropGlow";
			styles.GroupBox = "GroupBox";
			styles.LightmapEditorSelectedHighlight = "LightmapEditorSelectedHighlight";
			styles.IconButton = "IconButton";
			styles.LargeButton = "LargeButton";
			styles.AddComponentBtn = "AC Button";
			styles.ShurikenModuleTitle = "ShurikenModuleTitle";
			styles.ProjectBrowserHeaderBgTop = "ProjectBrowserHeaderBgTop";
			styles.ShurikenModuleBg = "ShurikenModuleBg";
		}

		private static void InitTextures(TexturesClass textures)
		{
			textures.CollabPush = EditorGUIUtility.FindTexture("CollabPush");
			textures.CollabPull = EditorGUIUtility.FindTexture("CollabPull");
			textures.GameView = EditorGUIUtility.FindTexture("UnityEditor.GameView");
			textures.FolderIcon = EditorGUIUtility.FindTexture("Folder Icon");
			textures.OrbitTool = EditorGUIUtility.FindTexture("ViewToolOrbit");
			textures.RotateTool = EditorGUIUtility.FindTexture("RotateTool");
			textures.AnimationWindow = EditorGUIUtility.FindTexture("UnityEditor.AnimationWindow");
			textures.ZoomTool = EditorGUIUtility.FindTexture("ViewToolZoom");
			textures.SpinTextures = new Texture2D[12];
			for (int i = 0; i < 12; i++)
			{
				textures.SpinTextures[i] = EditorGUIUtility.FindTexture("WaitSpin" + i.ToString("00"));
			}
			textures.Collab = EditorGUIUtility.FindTexture("Collab");
			textures.CollabNew = EditorGUIUtility.FindTexture("CollabNew");
			textures.CollabConflict = EditorGUIUtility.FindTexture("CollabConflict");
			textures.WarrningIconSmall = EditorGUIUtility.FindTexture("console.warnicon.sml");
			textures.ErrorIconSmall = EditorGUIUtility.FindTexture("console.erroricon.sml");
			textures.InfoIconSmall = EditorGUIUtility.FindTexture( "console.infoicon.sml" );
		}

		private static void InitContents(ContentsClass content)
		{
			content.Help = IconContent("_Help","","Help");
		}

		public static Texture2D GetTempSpinAnimatedTexture()
		{
			int index = Mathf.FloorToInt((float) ((EditorApplication.timeSinceStartup * 0.5) % 1) * 12);
			return Textures.SpinTextures[index];
		}

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

		public static GUIContent GetTempContent(string label, Texture tex, string tooltip)
		{
			tmpContent.text = label;
			tmpContent.tooltip = tooltip;
			tmpContent.image = tex;
			return tmpContent;
		}

		public static GUIContent GetTempContent(string label, Texture tex)
		{
			tmpContent.text = label;
			tmpContent.tooltip = String.Empty;
			tmpContent.image = tex;
			return tmpContent;
		}

		public static GUIContent GetTempContent(Texture tex,string tooltip)
		{
			tmpContent.text = string.Empty;
			tmpContent.tooltip = tooltip;
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

		public static void SecurePasswordFieldLayout(GUIContent content, SecureString value,params GUILayoutOption[] layouts)
		{
			Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.textField,layouts);
			SecurePasswordField(rect,content,value);
		}

		public static void SecurePasswordField(Rect rect, GUIContent content, SecureString value)
		{
			int controlId = GUIUtility.GetControlID(_secureTextFieldHash, FocusType.Keyboard, rect);
			rect = EditorGUI.PrefixLabel(rect, content);
			EditorGUIUtility.AddCursorRect(rect,MouseCursor.Text);
			GUIStyle fieldStyle = EditorStyles.textField;
			Event current = Event.current;

			switch (current.GetTypeForControl(controlId))
			{
				case EventType.MouseDown:
					if (rect.Contains(current.mousePosition))
					{
						GUIUtility.keyboardControl = controlId;
						current.Use();
					}
					break;
				case EventType.KeyDown:
					if (GUIUtility.keyboardControl == controlId)
					{
						if (!char.IsWhiteSpace(current.character) && current.character != '\0')
						{
							value.AppendChar(current.character);
							GUI.changed = true;
						}
						else if (current.keyCode == KeyCode.Backspace)
						{
							if (value.Length > 0)
							{
								value.RemoveAt(value.Length - 1);
								GUI.changed = true;
							}
						}
						else if(current.character == '\n' || current.keyCode == KeyCode.Escape)
						{
							GUIUtility.keyboardControl = 0;
						}
						current.Use();
					}
					break;
				case EventType.ValidateCommand:
					Debug.Log(current.commandName);
					break;
				case EventType.KeyUp:
					if (GUIUtility.keyboardControl == controlId)
					{
						current.Use();
					}
					break;
				case EventType.Repaint:
					fieldStyle.Draw(rect,GetTempContent("".PadRight(value != null ? value.Length : 0,'*')),controlId);
					break;
				case EventType.ContextClick:
					if (rect.Contains(current.mousePosition))
					{
						GenericMenu menu = new GenericMenu();
						if(value != null && value.Length > 0)
							menu.AddItem(new GUIContent("Clear"),false, () =>
							{
								value.Clear();
								GUI.changed = true;
							});
						else menu.AddDisabledItem(new GUIContent("Clear"));
						menu.ShowAsContext();
					}

					break;
			}
		}

		public static void DrawLoading(Rect rect,GUIContent loadinContent)
		{
			const float loadinCricleSize = 24;
			Vector2 loadingLabelWidth = EditorStyles.largeLabel.CalcSize(loadinContent);
			float totalWidth = loadinCricleSize + loadingLabelWidth.x + 8;
			float totalHeight = Mathf.Max(loadingLabelWidth.y, loadinCricleSize);

			PushMatrix();
			Rect loadinCircleRect = new Rect(rect.x + rect.width / 2 - totalWidth / 2, rect.y + rect.height / 2 - totalHeight / 2, loadinCricleSize, loadinCricleSize);
			GUIUtility.RotateAroundPivot((float)EditorApplication.timeSinceStartup * 300, loadinCircleRect.center);
			GUI.DrawTexture(loadinCircleRect, EditorGUIUtility.FindTexture("CollabProgress"));
			PopMatrix();

			GUI.Label(new Rect(loadinCircleRect.x + loadinCircleRect.width + 8, loadinCircleRect.y + ((loadinCricleSize - loadingLabelWidth.y) / 2), loadingLabelWidth.x, loadingLabelWidth.y), loadinContent, EditorStyles.largeLabel);
		}

		public static bool LinkButtonLayout(GUIContent content, GUIStyle style)
		{
			Rect rect = GUILayoutUtility.GetRect(content, style);
			EditorGUIUtility.AddCursorRect(rect,MouseCursor.Link);
			return GUI.Button(rect, content, style);
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

		public static string FormatRemainningTime(DateTime timeOffset)
		{
			return FormatRemainningTime(timeOffset.Ticks);
		}

		public static string FormatRemainningTime(long ticks)
		{
			const int SECOND = 1;
			const int MINUTE = 60 * SECOND;
			const int HOUR = 60 * MINUTE;
			const int DAY = 24 * HOUR;
			const int MONTH = 30 * DAY;

			var ts = new TimeSpan(DateTime.Now.Ticks - ticks);
			double delta = Math.Abs(ts.TotalSeconds);

			#if SHOW_SECONDS
			if (delta < 1 * MINUTE)
				return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago";
			#endif

			if (delta < 2 * MINUTE)
				return "a minute ago";

			if (delta < 45 * MINUTE)
				return ts.Minutes + " minutes ago";

			if (delta < 90 * MINUTE)
				return "an hour ago";

			if (delta < 24 * HOUR)
				return ts.Hours + " hours ago";

			if (delta < 48 * HOUR)
				return "yesterday";

			if (delta < 30 * DAY)
				return ts.Days + " days ago";

			if (delta < 12 * MONTH)
			{
				int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
				return months <= 1 ? "one month ago" : months + " months ago";
			}

			int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
			return years <= 1 ? "one year ago" : years + " years ago";
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