using System;

namespace UniGit.Utils
{
	public class GitAsyncOperation
	{
		private string name;
		private bool isDone;
		private Action<GitAsyncOperation> onComplete;

		private GitAsyncOperation(string name,Action<GitAsyncOperation> onComplete)
		{
			this.name = name;
			this.onComplete = onComplete;
		}

		internal static GitAsyncOperation Create(string name, Action<GitAsyncOperation> onComplete)
		{
			return new GitAsyncOperation(name,onComplete);
		}

		internal void Complete()
		{
			if (onComplete != null)
			{
				onComplete.Invoke(this);
				onComplete = null;
			}
		}

		internal void MarkDone()
		{
			isDone = true;
		}

		public string Name
		{
			get { return name; }
		}

		public bool IsDone
		{
			get { return isDone; }
		}
	}
}