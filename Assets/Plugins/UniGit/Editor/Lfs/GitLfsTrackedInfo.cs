using System;
using UnityEngine;

namespace UniGit
{
	public class GitLfsTrackedInfo
	{
		private string extension;
		private string filter;
		private string diff;
		private string merge;
		private TrackType type;
		private bool isDirty;

		public static GitLfsTrackedInfo Parse(string data)
		{
			if (string.IsNullOrEmpty(data)) return null;
			string[] chunks = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (chunks.Length < 5) return null;
			GitLfsTrackedInfo info = new GitLfsTrackedInfo
			{
				extension = chunks[0].Trim(),
				filter = chunks[1].Trim(),
				diff = chunks[2].Trim(),
				merge = chunks[3].Trim()
			};
			string typeString = chunks[4].Trim();
			if (typeString.EndsWith("delta"))
			{
				info.type = TrackType.Delta;
			}
			else if (typeString.EndsWith("text"))
			{
				info.type = TrackType.Text;
			}
			return info;
		}

		public override string ToString()
		{
			return extension + " " + filter + " " + diff + " " + merge + " " + (type == TrackType.Delta ? "-delta" : "-text");
		}

		public string Extension
		{
			get { return extension; }
			set
			{
				if (extension != value) isDirty = true;
				extension = value;
			}
		}

		public TrackType Type
		{
			get { return type; }
			set
			{
				if(type != value) isDirty = true;
				type = value;
			}
		}

		public bool IsDirty
		{
			get { return isDirty; }
		}

		public enum TrackType
		{
			Text,Delta
		}
	}
}