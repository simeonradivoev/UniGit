using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	[InitializeOnLoad]
	public class GitAsyncManager
	{
		private static List<GitAsyncOperation> activeOperations;

		static GitAsyncManager()
		{
			activeOperations = new List<GitAsyncOperation>();
			EditorApplication.update += OnEditorUpdate;
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, Action<GitAsyncOperation> onComplete)
		{
			return QueueWorker(waitCallback,state, null, onComplete);
		}

		public static GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, null, onComplete, lockObj);
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name)
		{
			return QueueWorker(waitCallback,state, name, null);
		}

		public static GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, name, null, lockObj);
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback, T state)
		{
			return QueueWorker(waitCallback, state, null, null);
		}

		public static GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, state, null, null, lockObj);
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, Action<GitAsyncOperation> onComplete)
		{
			return QueueWorker(waitCallback, null, onComplete);
		}

		public static GitAsyncOperation QueueWorkerWithLock(Action waitCallback, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, null, onComplete, lockObj);
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, string name)
		{
			return QueueWorker(waitCallback, name, null);
		}

		public static GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, name, null, lockObj);
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback)
		{
			return QueueWorker(waitCallback, null, null);
		}

		public static GitAsyncOperation QueueWorkerWithLock(Action waitCallback,object lockObj)
		{
			return QueueWorkerWithLock(waitCallback, null, null, lockObj);
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			ThreadPool.QueueUserWorkItem(p =>
			{
				try
				{
					waitCallback.Invoke((T) p);
				}
				finally
				{
					operation.MarkDone();
				}
			}, state);
			activeOperations.Add(operation);
			return operation;
		}

		public static GitAsyncOperation QueueWorkerWithLock<T>(Action<T> waitCallback, T state, string name, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			ThreadPool.QueueUserWorkItem(p =>
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
			}, state);
			activeOperations.Add(operation);
			return operation;
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			ThreadPool.QueueUserWorkItem((c) =>
			{
				try
				{
					waitCallback.Invoke();
				}
				finally
				{
					operation.MarkDone();
				}
			});
			activeOperations.Add(operation);
			return operation;
		}

		public static GitAsyncOperation QueueWorkerWithLock(Action waitCallback, string name, Action<GitAsyncOperation> onComplete,object lockObj)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name);
			if (onComplete != null)
				operation.onComplete += onComplete;

			ThreadPool.QueueUserWorkItem((c) =>
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
			});
			activeOperations.Add(operation);
			return operation;
		}

		private static void OnEditorUpdate()
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
						Debug.LogError("There was a problem while notifying async operation of completion");
						Debug.LogException(e);
					}
					finally
					{
						activeOperations.RemoveAt(i);
					}
				}
			}
		}
	}
}