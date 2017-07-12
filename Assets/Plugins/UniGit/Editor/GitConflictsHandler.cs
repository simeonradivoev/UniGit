using LibGit2Sharp;

namespace UniGit
{
	public class GitConflictsHandler
	{
		private readonly GitManager gitManager;

		public GitConflictsHandler(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}

		public bool CanResolveConflictsWithTool(string path)
		{
			if (!gitManager.IsValidRepo) return false;
			var conflict = gitManager.Repository.Index.Conflicts[path];
			return !gitManager.Repository.Lookup<Blob>(conflict.Ours.Id).IsBinary;
		}

		public void ResolveConflicts(string path, MergeFileFavor favor)
		{
			if(!gitManager.IsValidRepo) return;

			if (favor == MergeFileFavor.Normal)
			{
				GitExternalManager.HandleConflict(path);
			}
			else if (favor == MergeFileFavor.Ours)
			{
				var conflict = gitManager.Repository.Index.Conflicts[path];
				var ours = conflict.Ours;
				if (ours != null)
				{
					gitManager.Repository.Index.Remove(ours.Path);
					gitManager.Repository.CheckoutPaths("ORIG_HEAD", new[] { ours.Path });
				}
			}
			else if (favor == MergeFileFavor.Theirs)
			{
				var conflict = gitManager.Repository.Index.Conflicts[path];
				var theirs = conflict.Theirs;
				if (theirs != null)
				{
					gitManager.Repository.Index.Remove(theirs.Path);
					gitManager.Repository.CheckoutPaths("MERGE_HEAD", new[] { theirs.Path });
				}
			}

			//Debug.Log(EditorUtility.InvokeDiffTool(Path.GetFileName(theirs.Path) + " - Theirs", conflictPathTheirs, Path.GetFileName(ours.Path) + " - Ours", conflictPathOurs, "", conflictPathAncestor));
		}
	}
}