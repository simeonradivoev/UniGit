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

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name)
		{
			return QueueWorker(waitCallback,state, name, null);
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback, T state)
		{
			return QueueWorker(waitCallback, state, null, null);
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, Action<GitAsyncOperation> onComplete)
		{
			return QueueWorker(waitCallback, null, onComplete);
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, string name)
		{
			return QueueWorker(waitCallback, name, null);
		}

		public static GitAsyncOperation QueueWorker<T>(Action<T> waitCallback,T state, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name, onComplete);

			ThreadPool.QueueUserWorkItem(p =>
			{
				waitCallback.Invoke((T)p);
				operation.MarkDone();
			}, state);
			activeOperations.Add(operation);
			return operation;
		}

		public static GitAsyncOperation QueueWorker(Action waitCallback, string name, Action<GitAsyncOperation> onComplete)
		{
			var operation = GitAsyncOperation.Create(string.IsNullOrEmpty(name) ? GUID.Generate().ToString() : name, onComplete);

			ThreadPool.QueueUserWorkItem((c) =>
			{
				waitCallback.Invoke();
				operation.MarkDone();
			});

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