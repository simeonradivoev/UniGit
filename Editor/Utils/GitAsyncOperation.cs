using System;

namespace UniGit.Utils
{
	public class GitAsyncOperation
	{
		public event Action<GitAsyncOperation> onComplete;

		internal GitAsyncOperation(string name,object state)
		{
			this.State = state;
			this.Name = name;
		}

		internal virtual void Invoke(object state)
		{
			Initialized = true;
		}

		internal void Complete()
        {
            if (onComplete == null) return;
            onComplete.Invoke(this);
            onComplete = null;
        }

		public void MarkDone()
		{
			IsDone = true;
		}

		public string Name { get; private set; }
		public bool IsDone { get; private set; }
		public bool Initialized { get; internal set; }
		public object State { get; private set; }
	}

	public class GitAsyncOperationSimple : GitAsyncOperation
	{
		private readonly Action invokeAction;

		internal GitAsyncOperationSimple(string name, Action invokeAction) : base(name,null)
		{
			this.invokeAction = invokeAction;
		}

		internal sealed override void Invoke(object state)
		{
			base.Invoke(state);
			try
			{
				invokeAction.Invoke();
			}
			finally
			{
				MarkDone();
			}
		}
	}

	public class GitAsyncOperationComplex<T> : GitAsyncOperation
	{
		private readonly Action<T> invokeAction;

		internal GitAsyncOperationComplex(string name, Action<T> invokeAction, T param) : base(name,param)
		{
			this.invokeAction = invokeAction;
		}

		internal sealed override void Invoke(object state)
		{
			base.Invoke(state);
			try
			{
				invokeAction.Invoke((T)state);
			}
			finally
			{
				MarkDone();
			}
		}
	}
}