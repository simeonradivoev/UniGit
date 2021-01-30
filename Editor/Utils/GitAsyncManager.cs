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

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, Action<GitAsyncOperation> onComplete,bool threaded)
		{
			return QueueWorker(waitCallback,state, null, onComplete, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, Action<GitAsyncOperation> onComplete,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, state, null, onComplete, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name, bool threaded)
		{
			return QueueWorker(waitCallback,state, name, null, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, state, name, null, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback, T state,bool threaded)
		{
			return QueueWorker(waitCallback, state, null, null, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, state, null, null, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, Action<GitAsyncOperation> onComplete, bool threaded)
		{
			return QueueWorker(waitCallback, null, onComplete, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, Action<GitAsyncOperation> onComplete,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, null, onComplete, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, string name, bool threaded)
		{
			return QueueWorker(waitCallback, name, null, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, name, null, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, bool threaded)
		{
			return QueueWorker(waitCallback, null, null, threaded);
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback,object lockObj, bool threaded)
		{
			return QueueWorkerWithLock(waitCallback, null, null, lockObj, threaded);
		}

		public GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name, Action<GitAsyncOperation> onComplete,bool threaded)
		{
			var operation = new GitAsyncOperationComplex<T>(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name, waitCallback,state);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if (threaded && ThreadPool.QueueUserWorkItem(p =>
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
                lock (activeOperations)
                {
                    activeOperations.Add(operation);
                }
            }
			else
			{
				operation.Invoke(operation.State);
			}
			return operation;
		}

		public GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name, Action<GitAsyncOperation> onComplete,object lockObj, bool threaded)
		{
			var operation = new GitAsyncOperationComplex<T>(string.IsNullOrEmpty(name) ? Guid.NewGuid().ToString() : name,waitCallback,state);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if(threaded && ThreadPool.QueueUserWorkItem(p =>
			{
				Monitor.Enter(lockObj);
				try
				{
					operation.Invoke(p);
				}
				finally
				{
					Monitor.Exit(lockObj);
				}
			}, state))
			{
                lock (activeOperations)
                {
                    activeOperations.Add(operation);
                }
			}
			else
			{
				operation.Invoke(operation.State);
			}
			return operation;
		}

		public GitAsyncOperation QueueWorker(Action waitCallback, string name, Action<GitAsyncOperation> onComplete, bool threaded)
		{
			var operation = new GitAsyncOperationSimple(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name,waitCallback);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if(threaded && ThreadPool.QueueUserWorkItem(operation.Invoke))
			{
				operation.Initialized = true;
			}

            lock (activeOperations)
            {
                activeOperations.Add(operation);
            }
			return operation;
		}

		public GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name, Action<GitAsyncOperation> onComplete,object lockObj, bool threaded)
		{
			var operation = new GitAsyncOperationSimple(string.IsNullOrEmpty(name) ? Guid.NewGuid().ToString() : name,waitCallback);
			if (onComplete != null)
				operation.onComplete += onComplete;

			if (threaded && ThreadPool.QueueUserWorkItem((c) =>
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
                lock (activeOperations)
                {
                    activeOperations.Add(operation);
                }
            }
			else
			{
				operation.Invoke(operation.State);
			}
			
			return operation;
		}

		private void OnEditorUpdate()
		{
            lock (activeOperations)
            {
                for (var i = activeOperations.Count - 1; i >= 0; i--)
                {
                    //todo Make sure thread always finishes. For some reason operation on another thread may not complete or even start and will never be done.
                    if (!activeOperations[i].IsDone) continue;
                    try
                    {
                        activeOperations[i].Complete();
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogType.Error, "There was a problem while notifying async operation of completion");
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