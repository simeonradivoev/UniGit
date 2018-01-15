using UnityEngine;

namespace UniGit.Utils
{
	public static class GitLinks
	{
		public const string DiffWindowHelp = "https://github.com/simeonradivoev/UniGit/wiki/File-Difference";
		public const string HistoryWindowHelp = "https://github.com/simeonradivoev/UniGit/wiki/Commit-History";
		public const string DiffInspectorHelp = "https://github.com/simeonradivoev/UniGit/wiki/File-Difference#in-editor-diff-inspector";
		public const string SettingsWindowHelp = "https://github.com/simeonradivoev/UniGit/wiki/Setup#configuration";
		public const string SettingsUniGitSettings = "https://github.com/simeonradivoev/UniGit/wiki/Setup#unigit-settings";
		public const string SettingsGitSettings = "https://github.com/simeonradivoev/UniGit/wiki/Setup#git-settings";
		public const string SettingsGitIgnoreHelp = "https://git-scm.com/docs/gitignore";
		public const string Donate = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=4A4LQGA69LQ5A";
		public const string Homepage = "https://github.com/simeonradivoev/UniGit";
		public const string License = "https://github.com/simeonradivoev/UniGit/blob/master/LICENSE.md";
		public const string ReportIssue = "https://github.com/simeonradivoev/UniGit/issues/new";
		public const string Wiki = "https://github.com/simeonradivoev/UniGit/wiki";
		public const string GitLFS = "https://git-lfs.github.com/";

		public static Tuple<string, string>[] Credits = 
		{
			new Tuple<string, string>("Git Stash Icon : Christian Scheb","https://www.christianscheb.de"),
			new Tuple<string, string>("Additional Icons : Freepik","https://www.flaticon.com/authors/freepik"),
		};

		public static void GoTo(string link)
		{
			Application.OpenURL(link);
		}
	}
}