using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	public class GitProjectOverlay : IDisposable
	{
		private readonly GitManager gitManager;
		private readonly GitSettingsJson gitSettings;
		private readonly GUIStyle iconStyle;
		private StatusTreeClass statusTree;
		private readonly object statusTreeLock = new object();
		private readonly GitAsyncManager asyncManager;
		private readonly GitCallbacks gitCallbacks;

		[UniGitInject]
		public GitProjectOverlay(GitManager gitManager,GitCallbacks gitCallbacks, GitSettingsJson gitSettings, GitAsyncManager asyncManager)
		{
			if (iconStyle == null)
			{
				iconStyle = new GUIStyle
				{
					imagePosition = ImagePosition.ImageOnly,
					alignment = TextAnchor.LowerLeft,
					padding = new RectOffset(2, 2, 2, 2)
				};
			}

			this.gitManager = gitManager;
			this.gitSettings = gitSettings;
			this.asyncManager = asyncManager;
			this.gitCallbacks = gitCallbacks;
			gitCallbacks.ProjectWindowItemOnGUI += CustomIcons;
			gitCallbacks.UpdateRepository += OnUpdateRepository;
		}

		private void OnUpdateRepository(GitRepoStatus status,string[] paths)
		{
			if (gitSettings.Threading.IsFlagSet(GitSettingsJson.ThreadingType.StatusList))
			{
				gitManager.ActionQueue.Enqueue(() =>
				{
					UpdateStatusTreeThreaded(status);
				});
			}
			else
			{
				GitProfilerProxy.BeginSample("Git Project Window status tree building");
				UpdateStatusTree(status);
				GitProfilerProxy.EndSample();
			}
		}

		private void CustomIcons(string guid, Rect rect)
		{
			if (statusTree == null) return;
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var status = statusTree.GetStatus(path);
			if (status != null)
			{
				Object assetObject = AssetDatabase.LoadMainAssetAtPath(path);
				if (assetObject != null && ProjectWindowUtil.IsFolder(assetObject.GetInstanceID()))
				{
					//exclude the Assets folder
					if (status.Depth == 0) return;
					//todo cache expandedProjectWindowItems into a HashSet for faster Contains
					if (!status.ForceStatus && InternalEditorUtility.expandedProjectWindowItems.Contains(assetObject.GetInstanceID())) return;
				}
				DrawFileIcon(rect, GitOverlay.GetDiffTypeIcon(status.State, rect.height <= 16));
			}
		}

		private void DrawFileIcon(Rect rect, GUIContent icon)
		{
			float width = Mathf.Min(rect.width, 32);
			float height = Mathf.Min(rect.height, 32);
			GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), icon, iconStyle);
		}

		private void UpdateStatusTreeThreaded(GitRepoStatus status)
		{
			asyncManager.QueueWorkerWithLock(() => { UpdateStatusTree(status, true); }, statusTreeLock);
		}

		private void UpdateStatusTree(GitRepoStatus status, bool threaded = false)
		{
			try
			{
				var newStatusTree = new StatusTreeClass(gitSettings, status);
				statusTree = newStatusTree;
				gitManager.ExecuteAction(RepaintProjectWidnow, threaded);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		public void Dispose()
		{
			gitCallbacks.ProjectWindowItemOnGUI -= CustomIcons;
			gitCallbacks.UpdateRepository -= OnUpdateRepository;
		}

		public StatusTreeClass StatusTree
		{
			get { return statusTree; }
		}

		#region Static Helpers

		public static void RepaintProjectWidnow()
		{
			Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
			var projectWindow = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
			if (projectWindow != null)
			{
				((EditorWindow)projectWindow).Repaint();
			}
		}

		#endregion

		#region Status Tree
		public class StatusTreeClass
		{
			private Dictionary<string, StatusTreeEntry> entries = new Dictionary<string, StatusTreeEntry>();
			private string currentPath;
			private string[] currentPathArray;
			private FileStatus currentStatus;
			private readonly GitSettingsJson gitSettings;

			public StatusTreeClass(GitSettingsJson gitSettings)
			{
				this.gitSettings = gitSettings;
			}

			public StatusTreeClass(GitSettingsJson gitSettings, IEnumerable<GitStatusEntry> status) : this(gitSettings)
			{
				Build(status);
			}

			private void Build(IEnumerable<GitStatusEntry> status)
			{
				foreach (var entry in status)
				{
					currentPath = entry.Path;
					currentPathArray = entry.Path.Split('\\');
					currentStatus = !gitSettings.ShowEmptyFolders && GitManager.IsEmptyFolderMeta(currentPath) ? FileStatus.Ignored : entry.Status;
					AddRecursive(0, entries);
				}
			}

			private void AddRecursive(int entryNameIndex, Dictionary<string, StatusTreeEntry> entries)
			{
				StatusTreeEntry entry;
				string pathChunk = currentPathArray[entryNameIndex].Replace(".meta", "");

				//should a state change be marked at this level (inverse depth)
				//bool markState = Settings.ProjectStatusOverlayDepth < 0 || (Mathf.Abs(currentPathArray.Length - entryNameIndex)) <= Math.Max(1, Settings.ProjectStatusOverlayDepth);
				//markState = true;
				if (entries.TryGetValue(pathChunk, out entry))
				{
					entry.State = entry.State.SetFlags(currentStatus, true);
				}
				else
				{
					entry = new StatusTreeEntry(entryNameIndex);
					entry.State = entry.State.SetFlags(currentStatus);
					entries.Add(pathChunk, entry);
				}
				//check if it's at a allowed depth for status forcing on folders
				if (currentPathArray.Length - entryNameIndex < (gitSettings.ProjectStatusOverlayDepth + 1))
				{
					entry.forceStatus = true;
				}
				if (entryNameIndex < currentPathArray.Length - 1)
				{
					AddRecursive(entryNameIndex + 1, entry.SubEntiEntries);
				}
			}

			public StatusTreeEntry GetStatus(string path)
			{
				StatusTreeEntry entry;
				GetStatusRecursive(0, path.Split(new[] { UniGitPath.UnityDeirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries), entries, out entry);
				return entry;
			}

			private void GetStatusRecursive(int entryNameIndex, string[] path, Dictionary<string, StatusTreeEntry> entries, out StatusTreeEntry entry)
			{
				if (path.Length <= 0)
				{
					entry = null;
					return;
				}
				StatusTreeEntry entryTmp;
				if (entries.TryGetValue(path[entryNameIndex], out entryTmp))
				{
					if (entryNameIndex < path.Length - 1)
					{
						GetStatusRecursive(entryNameIndex + 1, path, entryTmp.SubEntiEntries, out entry);
						return;
					}
					entry = entryTmp;
					return;
				}

				entry = null;
			}
		}

		public class StatusTreeEntry
		{
			private Dictionary<string, StatusTreeEntry> subEntiEntries = new Dictionary<string, StatusTreeEntry>();
			internal bool forceStatus;
			private readonly int depth;
			public FileStatus State { get; set; }

			public StatusTreeEntry(int depth)
			{
				this.depth = depth;
			}

			public int Depth
			{
				get { return depth; }
			}

			public bool ForceStatus
			{
				get { return forceStatus; }
			}

			public Dictionary<string, StatusTreeEntry> SubEntiEntries
			{
				get { return subEntiEntries; }
				set { subEntiEntries = value; }
			}
		}
		#endregion
	}
}