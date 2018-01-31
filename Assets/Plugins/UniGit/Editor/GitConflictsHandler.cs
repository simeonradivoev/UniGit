using LibGit2Sharp;

namespace UniGit
{
	public class GitConflictsHandler
	{
		private readonly GitManager gitManager;
		private readonly GitExternalManager externalManager;
		private readonly GitInitializer initializer;

		public GitConflictsHandler(GitManager gitManager,GitExternalManager externalManager,GitInitializer initializer)
		{
			this.gitManager = gitManager;
			this.externalManager = externalManager;
			this.initializer = initializer;
		}

		public bool CanResolveConflictsWithTool(string path)
		{
			if (!initializer.IsValidRepo) return false;
			var conflict = gitManager.Repository.Index.Conflicts[path];
			return !gitManager.Repository.Lookup<Blob>(conflict.Ours.Id).IsBinary;
		}

		public void ResolveConflicts(string path, MergeFileFavor favor)
		{
			if(!initializer.IsValidRepo) return;

			if (favor == MergeFileFavor.Normal)
			{
				externalManager.HandleConflict(path);
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