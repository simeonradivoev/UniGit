using UnityEngine;

namespace UniGit.Utils
{
	public static class GitGUI
	{
		private static GUIContent tmpContent = new GUIContent();

		public static GUIContent GetTempContent(Texture tex)
		{
			tmpContent.text = string.Empty;
			tmpContent.tooltip = string.Empty;
			tmpContent.image = tex;
			return tmpContent;
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
	}
}