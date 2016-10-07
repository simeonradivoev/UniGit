using System.Text.RegularExpressions;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitDiffInspector : EditorWindow
	{
		private Vector2 scroll;
		public string path;
		public Patch patch;
		public string originalFileContents;
		private Styles styles;

		private class Styles
		{
			public GUIStyle AddeLine;
			public GUIStyle RemovedLine;
			public GUIStyle NormalLine;
		}

		private void InitStyles()
		{
			if (styles == null)
			{
				Texture2D greenText = new Texture2D(1,1) {hideFlags = HideFlags.HideAndDontSave};
				greenText.SetPixel(0,0,new Color(0,1,0,0.1f));
				greenText.Apply();

				Texture2D redText = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
				redText.SetPixel(0, 0, new Color(1, 0, 0, 0.1f));
				redText.Apply();


				styles = new Styles();
				styles.AddeLine = new GUIStyle(EditorStyles.label) {normal = new GUIStyleState() {background = greenText },focused = new GUIStyleState() { background = greenText },active = new GUIStyleState() { background = greenText }, margin = new RectOffset()};
				styles.RemovedLine = new GUIStyle(EditorStyles.label) {normal = new GUIStyleState() {background = redText},focused = new GUIStyleState() { background = redText },active = new GUIStyleState() { background = redText }, margin = new RectOffset()};
				styles.NormalLine = new GUIStyle(EditorStyles.label) {margin = new RectOffset(),normal = new GUIStyleState(),onNormal = new GUIStyleState()};
			}
		}

		private void OnGui()
		{
			if (patch == null)
			{
				Close();
				GUIUtility.ExitGUI();
				return;
			}

			if(originalFileContents.Length <= 0)
			{
				EditorGUILayout.HelpBox("No Difference",MessageType.Info);
				return;
			}

			InitStyles();

			scroll = EditorGUILayout.BeginScrollView(scroll);
			string[] lines = originalFileContents.Split('\n');
			int lineNUmber = 0;
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].StartsWith("@@"))
				{
					var titleMatch = Regex.Match(lines[i], @"(?<=@@\s)[^-+].+");
					if (titleMatch.Success)
					{
						GUILayout.Box(titleMatch.Value, "ProjectBrowserTopBarBg");
					}
					else
					{
						GUILayout.Box(lines[i], "ProjectBrowserTopBarBg");
					}

					var match = Regex.Match(lines[i], @"\+.\d+");
					if (match.Success) lineNUmber = int.Parse(match.Value);
				}
				else if (lines[i].StartsWith("+"))
				{
					GUILayout.Label(lineNUmber.ToString() + lines[i], styles.AddeLine);
					lineNUmber++;
				}
				else if (lines[i].StartsWith("-"))
				{
					GUILayout.Label(lineNUmber.ToString() + lines[i], styles.RemovedLine);
					lineNUmber++;
				}
				else
				{
					GUILayout.Label(lineNUmber.ToString() + lines[i], styles.NormalLine);
					lineNUmber++;
				}
			}
		}
	}
}