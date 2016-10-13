using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using LibGit2Sharp;

namespace UniGit.Adapters
{
	public interface IExternalAdapter
	{
		bool Push();
		bool Pull();
		bool Reset(Commit commit);
		bool Merge();
		bool Commit(string message);
		bool Fetch(string remote);
		bool Conflict(string path);
		bool Diff(string path);
		bool Diff(string path,string path2);
		bool Diff(string path, Commit start,Commit end);
		bool Revert(IEnumerable<string> paths);
		bool Switch();
	}
}