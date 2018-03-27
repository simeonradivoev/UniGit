using System.IO;

namespace UniGit.Utils
{
	public static class UniGitPath
	{
		public const char UnityDeirectorySeparatorChar = '/';
		public const char NewLineChar = '\n';

		public static string Combine(params string[] paths)
		{
			string finalPath = null;
			for (int i = 0; i < paths.Length; i++)
			{
				if (finalPath == null)
				{
					finalPath = paths[i];
				}
				else
				{
					finalPath = Path.Combine(finalPath, paths[i]);
				}
			}
			return finalPath;
		}

		public static bool Compare(string lhs, string rhs)
		{
			if (lhs.Length != rhs.Length) return false;
			for (int i = 0; i < lhs.Length; i++)
			{
				if (lhs[i] == Path.DirectorySeparatorChar && rhs[i] == Path.AltDirectorySeparatorChar) continue;
				if (lhs[i] != rhs[i]) return false;
			}
			return true;
		}
	}
}