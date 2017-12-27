using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniGit.Utils
{
	public class GitLfsHelper
	{
		private readonly FileLinesReader fileLinesReader;
		private readonly string repoPath;
		private readonly List<Regex> lfsFilters;

		[UniGitInject]
		public GitLfsHelper(string repoPath, FileLinesReader fileLinesReader)
		{
			lfsFilters = new List<Regex>();
			this.repoPath = repoPath;
			this.fileLinesReader = fileLinesReader;
			ReadGitAttributes();
		}

		private void ReadGitAttributes()
		{
			var attributesPath = Path.Combine(repoPath, ".gitattributes");
			string[] attributesLines;
			if (fileLinesReader.ReadLines(attributesPath,out attributesLines))
			{
				foreach (var line in attributesLines)
				{
					ReadLine(line);
				}
			}
		}

		private void ReadLine(string line)
		{
			string[] pairs = line.Split(new []{ ' ' },StringSplitOptions.RemoveEmptyEntries);
			if (pairs.Length > 0)
			{
				var filter = pairs[0];
				if (pairs.Any(p => p.Equals("filter=lfs",StringComparison.OrdinalIgnoreCase)))
				{
					string pattern = '^' +
									 filter
										 .Replace(".", "[.]")
						                 .Replace("*", ".*")
						                 .Replace("?", ".")
					                 + '$';
					var regex = new Regex(pattern, RegexOptions.Compiled);
					lfsFilters.Add(regex);
				}
			}
		}

		public bool IsLfsPath(string path)
		{
			return lfsFilters.Any(f => f.IsMatch(path));
		}
	}
}