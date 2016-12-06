using LibGit2Sharp;

namespace UniGit
{
	public static class GitConflictsHandler
	{
		public static bool CanResolveConflictsWithTool(string path)
		{
			if (!GitManager.IsValidRepo) return false;
			var conflict = GitManager.Repository.Index.Conflicts[path];
			return !GitManager.Repository.Lookup<Blob>(conflict.Ours.Id).IsBinary;
		}

		public static void ResolveConflicts(string path, MergeFileFavor favor)
		{
			if(!GitManager.IsValidRepo) return;

			if (favor == MergeFileFavor.Normal)
			{
				GitExternalManager.HandleConflict(path);
			}
			else if (favor == MergeFileFavor.Ours)
			{
				var conflict = GitManager.Repository.Index.Conflicts[path];
				var ours = conflict.Ours;
				if (ours != null)
				{
					GitManager.Repository.Index.Remove(ours.Path);
					GitManager.Repository.CheckoutPaths("ORIG_HEAD", new[] { ours.Path });
				}
			}
			else if (favor == MergeFileFavor.Theirs)
			{
				var conflict = GitManager.Repository.Index.Conflicts[path];
				var theirs = conflict.Theirs;
				if (theirs != null)
				{
					GitManager.Repository.Index.Remove(theirs.Path);
					GitManager.Repository.CheckoutPaths("MERGE_HEAD", new[] { theirs.Path });
				}
			}

			//Debug.Log(EditorUtility.InvokeDiffTool(Path.GetFileName(theirs.Path) + " - Theirs", conflictPathTheirs, Path.GetFileName(ours.Path) + " - Ours", conflictPathOurs, "", conflictPathAncestor));
		}
	}
}