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
		private readonly Regex[] lfsFilters; //array should be a bit faster then list for iterations
		private readonly UniGitPaths paths;

		[UniGitInject]
		public GitLfsHelper(UniGitPaths paths, FileLinesReader fileLinesReader)
		{
			this.paths = paths;
			this.fileLinesReader = fileLinesReader;
			lfsFilters = ReadGitAttributes();
		}

		private Regex[] ReadGitAttributes()
		{
			var list = new List<Regex>();
			var attributesPath = Path.Combine(paths.RepoPath, ".gitattributes");
			string[] attributesLines;
			if (fileLinesReader.ReadLines(attributesPath,out attributesLines))
			{
				foreach (var line in attributesLines)
				{
					ReadLine(line, list);
				}
			}
			return list.ToArray();
		}

		private void ReadLine(string line,List<Regex> list)
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
					list.Add(regex);
				}
			}
		}

		public bool IsLfsPath(string path)
		{
			//we need no GC that's why use a for loop
			for (int i = 0; i < lfsFilters.Length; i++)
			{
				if (lfsFilters[i].IsMatch(path)) return true;
			}
			return false;
		}
	}
}