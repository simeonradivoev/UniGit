using System;

namespace UniGit
{
	public class GitLfsTrackedInfo
	{
		private string extension;
		private string filter;
		private string diff;
		private string merge;
		private TrackType type;

        public static GitLfsTrackedInfo Parse(string data)
		{
			if (string.IsNullOrEmpty(data)) return null;
			var chunks = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (chunks.Length < 5) return null;
			var info = new GitLfsTrackedInfo
			{
				extension = chunks[0].Trim(),
				filter = chunks[1].Trim(),
				diff = chunks[2].Trim(),
				merge = chunks[3].Trim()
			};
			var typeString = chunks[4].Trim();
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
			get => extension;
            set
			{
				if (extension != value) IsDirty = true;
				extension = value;
			}
		}

		public TrackType Type
		{
			get => type;
            set
			{
				if(type != value) IsDirty = true;
				type = value;
			}
		}

		public bool IsDirty { get; private set; }

        public enum TrackType
		{
			Text,Delta
		}
	}
}