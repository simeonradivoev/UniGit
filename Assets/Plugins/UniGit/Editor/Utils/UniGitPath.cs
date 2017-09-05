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
	}
}