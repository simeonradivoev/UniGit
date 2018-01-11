using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitAsyncManager : IDisposable
	{
		private readonly List<GitAsyncOperation> activeOperations;
		private readonly GitCallbacks gitCallbacks;
		private readonly ILogger logger;

		[UniGitInject]
		public GitAsyncManager(GitCallbacks gitCallbacks,ILogger logger)
		{
			this.gitCallbacks = gitCallbacks;
			this.logger = logger;
			activeOperations = new List<GitAsyncOperation>();
			gitCallbacks.EditorUpdate += OnEditorUpdate;
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, Action<GitAsyncOperation> onComplete)
		{
			return QueueWorker(waitCallback,state, null, onComplete);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, null, onComplete, lockObj);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name)
		{
			return QueueWorker(waitCallback,state, name, null);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, name, null, lockObj);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback, T state)
		{
			return QueueWorker(waitCallback, state, null, null);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, null, null, lockObj);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, Action<GitAsyncOperation> onComplete)
		{
			return QueueWorker(waitCallback, null, onComplete);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, null, onComplete, lockObj);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, string name)
		{
			return QueueWorker(waitCallback, name, null);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, name, null, lockObj);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback)
		{
			return QueueWorker(waitCallback, null, null);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, null, null, lockObj);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if (!ThreadPool.QueueUserWorkItem(p =>
			{
				try
				{
					waitCallback.Invoke((T) p);
				}
				finally
				{
					operation.MarkDone();
				}
			}, state))
			{
				ShowUnableToQueueError(name);
				try
				{
					waitCallback.Invoke(state);
				}
				catch (Exception e)
				{
					logger.LogException(e);
				}
				finally
				{
					operation.MarkDone();
					operation.Complete();
				}
			}
			else
			{
				activeOperations.Add(operation);
			}
			return operation;
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if(!ThreadPool.QueueUserWorkItem(p =>
			{
				Monitor.Enter(lockObj);
				try
				{
					waitCallback.Invoke((T)p);
				}
				finally
				{
					operation.MarkDone();
					Monitor.Exit(lockObj);
				}
			}, state))
			{
				ShowUnableToQueueError(name);
				try
				{
					waitCallback.Invoke(state);
				}
				catch (Exception e)
				{
					logger.LogException(e);
				}
				finally
				{
					operation.MarkDone();
					operation.Complete();
				}
			}
			else
			{
				activeOperations.Add(operation);
			}
			return operation;
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if(!ThreadPool.QueueUserWorkItem((c) =>
			{
				try
				{
					waitCallback.Invoke();
				}
				finally
				{
					operation.MarkDone();
				}
			}))
			{
				ShowUnableToQueueError(name);
				try
				{
					waitCallback.Invoke();
				}
				catch (Exception e)
				{
					logger.LogException(e);
				}
				finally
				{
					operation.MarkDone();
					operation.Complete();
				}
			}
			else
			{
				activeOperations.Add(operation);
			}
			return operation;
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if (!ThreadPool.QueueUserWorkItem((c) =>
			{
				Monitor.Enter(lockObj);
				try
				{
					waitCallback.Invoke();
				}
				finally
				{
					operation.MarkDone();
					Monitor.Exit(lockObj);
				}
			}))
			{
				ShowUnableToQueueError(name);
				try
				{
					waitCallback.Invoke();
				}
				catch (Exception e)
				{
					logger.LogException(e);
				}
				finally
				{
					operation.MarkDone();
					operation.Complete();
				}
			}
			else
			{
				activeOperations.Add(operation);
			}
			return operation;
		}

		private void ShowUnableToQueueError(string name)
		{
			Debug.LogError("Could not Queue Git Async Operation with name: " + name);
		}

		private void OnEditorUpdate()
		{
			for (int i = activeOperations.Count-1; i >= 0; i--)
			{
				if (activeOperations[i].IsDone)
				{
					try
					{
						activeOperations[i].Complete();
					}
					catch (Exception e)
					{
						logger.Log(LogType.Error,"There was a problem while notifying async operation of completion");
						logger.LogException(e);
					}
					finally
					{
						activeOperations.RemoveAt(i);
					}
				}
			}
		}

		public void Dispose()
		{
			if(gitCallbacks != null) gitCallbacks.EditorUpdate -= OnEditorUpdate;
		}
	}
}