using System;

namespace UniGit.Utils
{
	public class GitAsyncOperation
	{
		private string name;
		private bool isDone;
		public event Action<GitAsyncOperation> onComplete;

		private GitAsyncOperation(string name)
		{
			this.name = name;
		}

		internal static GitAsyncOperation Create(string name)
		{
			return new GitAsyncOperation(name);
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