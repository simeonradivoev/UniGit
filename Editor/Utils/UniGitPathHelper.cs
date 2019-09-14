using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace UniGit.Utils
{
	public static class UniGitPathHelper
	{
		public const char UnityDeirectorySeparatorChar = '/';
		public const char NewLineChar = '\n';

		public static bool IsPathInAssetFolder(string path)
		{
			return path.StartsWith("Assets");
		}

        public static bool IsPathInPackagesFolder(string path)
        {
            return path.StartsWith("Packages");
        }

        public static bool IsMetaPath(string path)
		{
			return path.EndsWith(".meta");
		}

        public static string ToUnityPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, UniGitPathHelper.UnityDeirectorySeparatorChar);
        }

        public static string FixUnityPath(string path)
		{
			return path.Replace(UniGitPathHelper.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
		}

        public static string SubtractDirectory(string from, string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(from))
            {
                return value;
            }

            var replaced = from.Replace(value, "");
            if (replaced.Length > 0 && replaced[0] == Path.DirectorySeparatorChar ||
                replaced[0] == Path.AltDirectorySeparatorChar)
            {
                replaced = replaced.Remove(0, 1);
            }
            return replaced;
        }

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

		public static bool PathsEqual(string x, string y)
		{
			try
			{
				var xInfo = new DirectoryInfo(x);
				var yInfo = new DirectoryInfo(y);

				return xInfo.FullName == yInfo.FullName;
			}
			catch (Exception error)
			{
				var message = String.Format("Unable to check directories {0} and {1}: {2}", x, y, error);
				Trace.WriteLine(message);
			}

			return false;
		}

        //taken from https://stackoverflow.com/questions/8091829/how-to-check-if-one-path-is-a-child-of-another-path/36649958
        public static bool IsSubDirectoryOf(string candidate, string other)
		{
			var isChild = false;
			try
			{
				var candidateInfo = new DirectoryInfo(candidate);
				var otherInfo = new DirectoryInfo(other);

				while (candidateInfo.Parent != null)
				{
					if (candidateInfo.Parent.FullName == otherInfo.FullName)
					{
						isChild = true;
						break;
					}
					else candidateInfo = candidateInfo.Parent;
				}
			}
			catch (Exception error)
			{
				var message = String.Format("Unable to check directories {0} and {1}: {2}", candidate, other, error);
				Trace.WriteLine(message);
			}

			return isChild;
		}
	}
}