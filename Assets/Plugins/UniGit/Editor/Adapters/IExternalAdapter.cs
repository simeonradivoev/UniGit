using System;
using JetBrains.Annotations;
using LibGit2Sharp;

namespace UniGit.Adapters
{
	public interface IExternalAdapter
	{
		void Push();
		void Pull();
		void Reset();
		void Merge();
		void Commit(string message);
		void Fetch(string remote);
		void Conflict(string left,string right,string ansestor,string merge,Type assetType);
		void Diff(string leftTitle,string leftPath, string rightTitle,string rightPath, [CanBeNull] Type assetType);
	}
}