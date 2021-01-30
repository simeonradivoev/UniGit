using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitAnimation : IDisposable
	{
		public static GitTween Empty = new GitTween(0,0,null,GitSettingsJson.AnimationTypeEnum.None);
		private readonly GitCallbacks gitCallbacks;
		private readonly GitSettingsJson gitSettings;
		private readonly List<GitTween> tweens;
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

		public GitTween StartAnimation(float time,GitSettingsJson.AnimationTypeEnum animationType)
		{
			return StartAnimation(time,null,animationType);
		}

		public GitTween StartAnimation(float time,EditorWindow window,GitSettingsJson.AnimationTypeEnum animationType)
		{
			var tween = new GitTween(IsAnimationAllowed(animationType) ? time : 0,time,window,animationType);
			tweens.Add(tween);
			return tween;
		}

		public GitTween StartManualAnimation(float time,EditorWindow window,out double animationTimer,GitSettingsJson.AnimationTypeEnum animationType)
		{
			animationTimer = EditorApplication.timeSinceStartup;
			return new GitTween(IsAnimationAllowed(animationType) ? time : 0,time,window,animationType);
		}

		private bool IsAnimationAllowed(GitSettingsJson.AnimationTypeEnum animationType)
		{
			return gitSettings.AnimationType.HasFlag(animationType);
		}

		public static float ApplyEasing(float t)
		{
			return t < .5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
		}

		public void Update(GitTween tween, ref double lastTime)
		{
			var deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
			tween.Time = Mathf.Max(0, tween.Time - deltaTime);
			if(tween.EditorWindow != null) tween.EditorWindow.Repaint();
			lastTime = EditorApplication.timeSinceStartup;
		}

		private void OnEditorUpdate()
		{
			var deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
			for (var i = tweens.Count-1; i >= 0; i--)
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
            public GitTween(float time, float maxTime,EditorWindow editorWindow,GitSettingsJson.AnimationTypeEnum animationType)
			{
				this.AnimationType = animationType;
				this.Time = time;
				this.MaxTime = maxTime;
				this.EditorWindow = editorWindow;
			}

			public EditorWindow EditorWindow { get; }

            public float Time { get; set; }

            public float Percent
			{
				get
				{
					if (MaxTime == 0) return 1;
					return Time / MaxTime; 
				}
			}

			public float MaxTime { get; }

            public GitSettingsJson.AnimationTypeEnum AnimationType { get; }

            public bool Valid => Time > 0;
        }
	}
}
