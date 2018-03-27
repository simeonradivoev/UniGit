using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	/// <summary>
	/// The overlays for the project windows
	/// </summary>
	public class GitProjectOverlay : IDisposable, IGitWatcher
	{
		public const string ForceUpdateKey = "ForceUpdateProjectOverlay";

		private readonly GitManager gitManager;
		private readonly GitSettingsJson gitSettings;
		private readonly GUIStyle iconStyle;
		private readonly GUIStyle lineStyle;
		private readonly object statusTreeLock = new object();
		private readonly GitAsyncManager asyncManager;
		private readonly GitCallbacks gitCallbacks;
		private readonly GitReflectionHelper reflectionHelper;
		private readonly GitOverlay gitOverlay;
		private readonly IGitPrefs prefs;
		private readonly UniGitData data;
		private readonly ILogger logger;
		private readonly bool cullNonAssetPaths;

		private StatusTreeClass statusTree;
		private bool isDirty = true;
		private bool isUpdating;
		private List<EditorWindow> projectWindows;

		[UniGitInject]
		public GitProjectOverlay(GitManager gitManager,
			GitCallbacks gitCallbacks, 
			GitSettingsJson gitSettings, 
			GitAsyncManager asyncManager,
			GitReflectionHelper reflectionHelper,
			GitOverlay gitOverlay,
			IGitPrefs prefs,
			UniGitData data,
			ILogger logger,
			[UniGitInjectOptional] bool cullNonAssetPaths = true)
		{
			if (iconStyle == null)
			{
				iconStyle = new GUIStyle
				{
					imagePosition = ImagePosition.ImageOnly,
					alignment = TextAnchor.LowerLeft,
					padding = new RectOffset(2, 2, 2, 2)
				};
				lineStyle = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IN Title"))
				{
					padding = new RectOffset(),
					margin = new RectOffset(),
					contentOffset = Vector2.zero,
					fixedHeight = 0,
					fixedWidth = 0,
					border = {left = 0,right = 0}
				};
			}

			this.logger = logger;
			this.data = data;
			this.gitManager = gitManager;
			this.gitSettings = gitSettings;
			this.asyncManager = asyncManager;
			this.gitCallbacks = gitCallbacks;
			this.reflectionHelper = reflectionHelper;
			this.gitOverlay = gitOverlay;
			this.prefs = prefs;
			this.cullNonAssetPaths = cullNonAssetPaths;

			gitManager.AddWatcher(this);

			gitCallbacks.EditorUpdate += OnEditorUpdate;
			gitCallbacks.ProjectWindowItemOnGUI += CustomIcons;
			gitCallbacks.UpdateRepository += OnUpdateRepository;

			//project browsers only get created before delay call but not before constructor
			gitCallbacks.DelayCall += () =>
			{
				projectWindows = new List<EditorWindow>(Resources.FindObjectsOfTypeAll(reflectionHelper.ProjectWindowType).Cast<EditorWindow>()); 
			};
		}

		private void OnUpdateRepository(GitRepoStatus status,string[] paths)
		{
			isDirty = true;
		}

		private void OnEditorUpdate()
		{
			if (isDirty && !isUpdating && ((projectWindows != null && projectWindows.Any(reflectionHelper.HasFocusFucntion.Invoke)) | prefs.GetBool(ForceUpdateKey,false)))
			{
				if (data.Initialized)
				{
					if (gitSettings.Threading.IsFlagSet(GitSettingsJson.ThreadingType.Status))
					{
						isUpdating = true;
						gitManager.ActionQueue.Enqueue(() =>
						{
							try
							{
								UpdateStatusTreeThreaded(data.RepositoryStatus);
							}
							finally
							{
								isUpdating = false;
							}
						});
						isDirty = false;
					}
					else
					{
						if(!data.RepositoryStatus.TryEnterLock()) return;
						isUpdating = true;
						GitProfilerProxy.BeginSample("Git Project Window status tree building");
						try
						{
							
							try
							{
								UpdateStatusTree(data.RepositoryStatus);
							}
							finally
							{
								isUpdating = false;
							}
						}
						finally
						{
							data.RepositoryStatus.Unlock();
							GitProfilerProxy.EndSample();
						}
						isDirty = false;
					}
					
				}
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
				if (assetObject != null && ProjectWindowUtil.IsFolder(assetObject.GetInstanceID()) && !status.IsSubModule)
				{
					//exclude the Assets folder
					if (status.Depth == 0) return;
					//todo cache expandedProjectWindowItems into a HashSet for faster Contains
					if (!status.ForceStatus && InternalEditorUtility.expandedProjectWindowItems.Contains(assetObject.GetInstanceID())) return;
				}
				bool small = rect.height <= 16;
				if (small)
				{
					DrawFileIcons(rect, gitOverlay.GetDiffTypeIcons(status.State,true),status.IsSubModule,path);
				}
				else
				{
					DrawFileIcon(rect, gitOverlay.GetDiffTypeIcon(status.State, false),status.IsSubModule);
				}
				
			}
		}

		private void DrawFileIcon(Rect rect, GUIContent icon,bool subModule)
		{
			float width = Mathf.Min(rect.width, 32);
			float height = Mathf.Min(rect.height, 32);
			if(subModule) GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), gitOverlay.icons.submoduleIcon, iconStyle);
			else GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), icon, iconStyle);
		}

		private void DrawFileIcons(Rect rect, IEnumerable<GUIContent> contents,bool subModule,string path)
		{
			float width = Mathf.Min(rect.width, 16);
			float height = Mathf.Min(rect.height, 16);
			int index = 0;
			foreach (var content in contents)
			{
				GUI.Label(new Rect(rect.x + rect.width - width - (width * index), rect.y, width, height), content, iconStyle);
				index++;
			}

			if (subModule)
			{
				GUI.Label(new Rect(rect.x + rect.width - width - (width * index), rect.y, width, height), gitOverlay.icons.submoduleIconSmall, iconStyle);
				index++;

				float textWidthMin, textWidthMax;
				string name = Path.GetFileName(path);
				EditorStyles.label.CalcMinMaxWidth(GitGUI.GetTempContent(name), out textWidthMin, out textWidthMax);
				float lineX = rect.x + textWidthMin + 18;
				float lineWidth = Mathf.Max(rect.width - (width * index) - textWidthMin - 20,0);
				GUI.Box(new Rect(lineX,rect.y + rect.height*0.5f,lineWidth,2),GUIContent.none,lineStyle);
			}
		}

		private void UpdateStatusTreeThreaded(GitRepoStatus status)
		{
			asyncManager.QueueWorkerWithLock(() =>
			{
				status.Lock();
				UpdateStatusTree(status, true);
				status.Unlock();

			}, statusTreeLock);
		}

		private void UpdateStatusTree(GitRepoStatus status, bool threaded = false)
		{
			try
			{
				var subModules = status.SubModuleEntries.Select(m => m.Path);
				if (gitManager.InSubModule) subModules = subModules.Concat(new[] {gitSettings.ActiveSubModule});
				var newStatusTree = new StatusTreeClass(gitSettings,gitManager, status,subModules,cullNonAssetPaths);
				statusTree = newStatusTree;
				gitManager.ExecuteAction(RepaintProjectWidnow, threaded);
			}
			catch (Exception e)
			{
				logger.LogException(e);
			}
		}

		public void Dispose()
		{
			gitCallbacks.EditorUpdate -= OnEditorUpdate;
			gitCallbacks.ProjectWindowItemOnGUI -= CustomIcons;
			gitCallbacks.UpdateRepository -= OnUpdateRepository;
		}

		public StatusTreeClass StatusTree
		{
			get { return statusTree; }
		}

		public List<EditorWindow> ProjectWindows
		{
			get { return projectWindows; }
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

		#region GitWatcher

		public void MarkDirty()
		{
			isDirty = true;
		}

		public bool IsWatching
		{
			get { return projectWindows != null && projectWindows.Any(reflectionHelper.HasFocusFucntion.Invoke); }
		}

		public bool IsValid
		{
			get { return true; }
		}

		#endregion

		#region Status Tree
		public class StatusTreeClass
		{
			private Dictionary<string, StatusTreeEntry> entries = new Dictionary<string, StatusTreeEntry>();
			private string currentProjectPath;
			private string[] currentPathArray;
			private FileStatus currentStatus;
			private readonly GitSettingsJson gitSettings;
			private readonly GitManager gitManager;
			private readonly bool cullNonAssetPaths;

			public StatusTreeClass(GitSettingsJson gitSettings,GitManager gitManager,bool cullNonAssetPaths)
			{
				this.gitManager = gitManager;
				this.cullNonAssetPaths = cullNonAssetPaths;
				this.gitSettings = gitSettings;
			}

			public StatusTreeClass(GitSettingsJson gitSettings,GitManager gitManager, IEnumerable<GitStatusEntry> status,IEnumerable<string> subModules,bool cullNonAssetPaths) : this(gitSettings,gitManager,cullNonAssetPaths)
			{
				Build(status,subModules);
			}

			private void Build(IEnumerable<GitStatusEntry> status,IEnumerable<string> subModules)
			{
				foreach (var entry in status)
				{
					currentProjectPath = gitManager.ToProjectPath(entry.LocalPath);
					currentPathArray = currentProjectPath.Split('\\');
					currentStatus = !gitSettings.ShowEmptyFolders && gitManager.IsEmptyFolderMeta(currentProjectPath) ? FileStatus.Ignored : entry.Status;
					if(cullNonAssetPaths && !GitManager.IsPathInAssetFolder(currentProjectPath)) continue;
					AddRecursive(0, entries);
				}

				foreach (var module in subModules)
				{
					currentPathArray = module.Split('\\');
					AddSubModuleRecursive(0, entries);
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

			private void AddSubModuleRecursive(int entryNameIndex, Dictionary<string, StatusTreeEntry> entries)
			{
				StatusTreeEntry entry;
				string pathChunk = currentPathArray[entryNameIndex].Replace(".meta", "");
				if (!entries.TryGetValue(pathChunk, out entry))
				{
					entry = new StatusTreeEntry(entryNameIndex);
					entries.Add(pathChunk, entry);
				}

				if (entryNameIndex < currentPathArray.Length - 1)
				{
					AddSubModuleRecursive(entryNameIndex + 1, entry.SubEntiEntries);
				}
				else
				{
					entry.isSubModule = true;
					entry.forceStatus = true;
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
			internal bool isSubModule;

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

			public bool IsSubModule
			{
				get { return isSubModule; }
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