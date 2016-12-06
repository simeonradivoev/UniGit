using LibGit2Sharp;

namespace UniGit.Status
{
	public struct GitStatusEntry
	{
		public readonly string Path;
		public readonly FileStatus Status;

		public GitStatusEntry(string path, FileStatus status)
		{
			Path = path;
			Status = status;
		}
	}
}