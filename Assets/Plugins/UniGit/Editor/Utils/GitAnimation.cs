using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitAnimation : IDisposable
	{
		public static GitTween Empty = new GitTween(0,0,null);
		private readonly GitCallbacks gitCallbacks;
		private readonly GitSettingsJson gitSettings;
		private List<GitTween> tweens;
		private double lastTime;

		[UniGitInject]
		public GitAnimation(GitCallbacks gitCallbacks,GitSettingsJson gitSettings)
		{
			tweens = new List<GitTween>();
			this.gitSettings = gitSettings;
			this.gitCallbacks = gitCallbacks;
			gitCallbacks.EditorUpdate += OnEditorUpdate;
			lastTime = EditorApplication.timeSinceStartup;
		}

		public GitTween StartAnimation(float time)
		{
			return StartAnimation(time,null);
		}

		public GitTween StartAnimation(float time,EditorWindow window)
		{
			var tween = new GitTween(gitSettings.DisableAnimations ? 0 : time,time,window);
			tweens.Add(tween);
			return tween;
		}

		public GitTween StartManualAnimation(float time,EditorWindow window,ref double animationTimer)
		{
			animationTimer = EditorApplication.timeSinceStartup;
			return new GitTween(time,time,window);
		}

		public static float ApplyEasing(float t)
		{
			return t < .5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
		}

		public void Update(GitTween tween, ref double lastTime)
		{
			float deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
			tween.Time = Mathf.Max(0, tween.Time - deltaTime);
			if(tween.EditorWindow != null) tween.EditorWindow.Repaint();
			lastTime = EditorApplication.timeSinceStartup;
		}

		private void OnEditorUpdate()
		{
			float deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
			for (int i = tweens.Count-1; i >= 0; i--)
			{
				var tween = tweens[i];
				tween.Time = Mathf.Max(0, tween.Time - deltaTime);
				if(tween.EditorWindow != null) tween.EditorWindow.Repaint();
				if (!tween.Valid)
				{
					tweens.RemoveAt(i);
				}
			}
			lastTime = EditorApplication.timeSinceStartup;
		}

		public void Dispose()
		{
			gitCallbacks.EditorUpdate -= OnEditorUpdate;
		}

		public class GitTween
		{
			private float time;
			private float maxTime;
			private EditorWindow editorWindow;

			public GitTween(float time, float maxTime,EditorWindow editorWindow)
			{
				this.time = time;
				this.maxTime = maxTime;
				this.editorWindow = editorWindow;
			}

			public EditorWindow EditorWindow
			{
				get { return editorWindow; }
			}

			public float Time
			{
				get { return time; }
				set { time = value; }
			}

			public float Percent
			{
				get
				{
					if (maxTime == 0) return 1;
					return time / maxTime; 
				}
			}

			public float MaxTime
			{
				get { return maxTime; }
			}

			public bool Valid
			{
				get { return time > 0; }
			}
		}
	}
}
