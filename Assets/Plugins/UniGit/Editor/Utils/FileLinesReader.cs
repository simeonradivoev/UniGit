using System.IO;

namespace UniGit.Utils
{
	public class FileLinesReader
	{
		public virtual bool ReadLines(string path,out string[] lines)
		{
			if (File.Exists(path))
			{
				lines = File.ReadAllLines(path);
				return true;
			}
			lines = null;
			return false;
		}
	}
}