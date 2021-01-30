using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LibGit2Sharp;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UniGit
{
	/// <summary>
	/// The overlays for the project windows
	/// </summary>
	public class GitProjectOverlay : IDisposable, IGitWatcher
	{
		private static Func<string, int> GetMainAssetOrInProgressProxyInstanceID;
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
        private readonly UniGitPaths paths;
        private readonly InjectionHelper injectionHelper;

        private bool isDirty = true;
		private bool isUpdating;

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
            InjectionHelper injectionHelper,
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
            this.injectionHelper = injectionHelper;

            gitManager.AddWatcher(this);

			gitCallbacks.EditorUpdate += OnEditorUpdate;
			gitCallbacks.ProjectWindowItemOnGUI += CustomIcons;
			gitCallbacks.UpdateRepository += OnUpdateRepository;

			//project browsers only get created before delay call but not before constructor
			gitCallbacks.DelayCall += () =>
			{
				ProjectWindows = new List<EditorWindow>(Resources.FindObjectsOfTypeAll(reflectionHelper.ProjectWindowType).Cast<EditorWindow>()); 
			};

			//shortcut to getting instance id without loading asset
			var method = typeof(AssetDatabase).GetMethod("GetMainAssetOrInProgressProxyInstanceID", BindingFlags.NonPublic | BindingFlags.Static);
			if(method != null)
				GetMainAssetOrInProgressProxyInstanceID = (Func<string, int>)Delegate.CreateDelegate(typeof(Func<string, int>), method);
		}

		private void OnUpdateRepository(GitRepoStatus status,string[] paths)
		{
			isDirty = true;
		}

		private void OnEditorUpdate()
		{
			if (isDirty && !isUpdating && ((ProjectWindows != null && ProjectWindows.Any(reflectionHelper.HasFocusFunction.Invoke)) | prefs.GetBool(ForceUpdateKey,false)))
            {
                if (!data.Initialized) return;
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
                    if(!Monitor.TryEnter(data.RepositoryStatus.LockObj)) return;
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
                        Monitor.Exit(data.RepositoryStatus.LockObj);
                        GitProfilerProxy.EndSample();
                    }
                    isDirty = false;
                }
            }
		}

		private IEnumerable<GUIContent> GetIcons(FileStatus status,SubmoduleStatus submoduleStatus, bool isSubModule)
		{
			foreach (var s in gitOverlay.GetDiffTypeIcons(status,true))
				yield return s;
			if (isSubModule)
			{
				yield return gitOverlay.icons.submoduleIconSmall;
				if (submoduleStatus.HasFlag(SubmoduleStatus.WorkDirFilesModified))
					yield return gitOverlay.icons.modifiedIconSmall;
				if (submoduleStatus.HasFlag(SubmoduleStatus.WorkDirFilesUntracked))
					yield return gitOverlay.icons.untrackedIconSmall;
				if (submoduleStatus.HasFlag(SubmoduleStatus.WorkDirFilesIndexDirty))
					yield return gitOverlay.icons.addedIconSmall;
				if (submoduleStatus.HasFlag(SubmoduleStatus.WorkDirModified))
					yield return GitGUI.GetTempContent(GitGUI.Textures.CollabPush);
			}
		}

		private void CustomIcons(string guid, Rect rect)
		{
			if (StatusTree == null) return;
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var status = StatusTree.GetStatus(path);
            if (status == null) return;
            if (AssetDatabase.IsValidFolder(path) && !status.IsSubModule)
            {
                var folderInstanceId = GetAssetInstanceId(path);
                //exclude the Assets folder
                if (status.Depth == 0) return;
                //todo cache expandedProjectWindowItems into a HashSet for faster Contains
                if (!status.ForceStatus && InternalEditorUtility.expandedProjectWindowItems.Contains(folderInstanceId)) return;
            }
            var small = rect.height <= 16;
            if (small)
            {
                DrawFileIcons(rect,GetIcons(status.State,status.SubmoduleStatus,status.isSubModule),status.IsSubModule,path);
            }
            else
            {
                DrawFileIcon(rect, status.IsSubModule ? gitOverlay.icons.submoduleIcon : gitOverlay.GetDiffTypeIcon(status.State, false));
            }
        }

		private int GetAssetInstanceId(string path)
		{
			if (GetMainAssetOrInProgressProxyInstanceID != null) return GetMainAssetOrInProgressProxyInstanceID.Invoke(path);
			var obj = AssetDatabase.LoadMainAssetAtPath(path);
			return obj.GetInstanceID();
		}

		private void DrawFileIcon(Rect rect, GUIContent icon)
		{
			var width = Mathf.Min(rect.width, 32);
			var height = Mathf.Min(rect.height, 32);
			GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), icon, iconStyle);
		}

		private void DrawFileIcons(Rect rect, IEnumerable<GUIContent> contents,bool subModule,string path)
		{
			var width = Mathf.Min(rect.width, 16);
			var height = Mathf.Min(rect.height, 16);
			var index = 0;
			foreach (var content in contents)
			{
				GUI.Label(new Rect(rect.x + rect.width - width - (width * index), rect.y, width, height), content, iconStyle);
				index++;
			}

			if (subModule)
			{
                var name = Path.GetFileName(path);
				EditorStyles.label.CalcMinMaxWidth(GitGUI.GetTempContent(name), out var textWidthMin, out var textWidthMax);
				var lineX = rect.x + textWidthMin + 18;
				var lineWidth = Mathf.Max(rect.width - (width * index) - textWidthMin - 20,0);
				GUI.Box(new Rect(lineX,rect.y + rect.height*0.5f,lineWidth,2),GUIContent.none,lineStyle);
			}
		}

		private void UpdateStatusTreeThreaded(GitRepoStatus status)
		{
			asyncManager.QueueWorkerWithLock(() =>
			{
				lock (status.LockObj)
				{
					UpdateStatusTree(status, true);
				}
			}, statusTreeLock,true);
		}

		private void UpdateStatusTree(GitRepoStatus status, bool threaded = false)
		{
			try
			{
				var subModules = status.SubModuleEntries;
				if (gitManager.InSubModule)
                    subModules = subModules.Concat(new []{new GitStatusSubModuleEntry(gitSettings.ActiveSubModule) });

                var newStatusTree = injectionHelper.CreateInstance<StatusTreeClass>(cullNonAssetPaths);
				newStatusTree.Build(status, subModules);
				StatusTree = newStatusTree;
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

		public StatusTreeClass StatusTree { get; private set; }

        public List<EditorWindow> ProjectWindows { get; private set; }

        #region Static Helpers

		public static void RepaintProjectWidnow()
		{
			var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
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

		public bool IsWatching => ProjectWindows != null && ProjectWindows.Any(reflectionHelper.HasFocusFunction.Invoke);

        public bool IsValid => true;

        #endregion

		#region Status Tree
		public class StatusTreeClass
		{
			private readonly Dictionary<string, StatusTreeEntry> entries = new Dictionary<string, StatusTreeEntry>();
			private string currentProjectPath;
			private string[] currentPathArray;
			private FileStatus currentStatus;
			private SubmoduleStatus currentSubModuleStatus;
			private readonly GitSettingsJson gitSettings;
			private readonly GitManager gitManager;
			private readonly bool cullNonAssetPaths;
            private readonly UniGitPaths paths;

            [UniGitInject]
			public StatusTreeClass(GitSettingsJson gitSettings,GitManager gitManager,bool cullNonAssetPaths, UniGitPaths paths)
            {
                this.paths = paths;
				this.gitManager = gitManager;
				this.cullNonAssetPaths = cullNonAssetPaths;
				this.gitSettings = gitSettings;
			}

			internal void Build(IEnumerable<GitStatusEntry> status,IEnumerable<GitStatusSubModuleEntry> subModules)
			{
				foreach (var entry in status)
				{
					currentProjectPath = gitManager.ToProjectPath(entry.LocalPath);
					currentPathArray = currentProjectPath.Split('\\');
					currentStatus = !gitSettings.ShowEmptyFolders && gitManager.IsEmptyFolderMeta(currentProjectPath) ? FileStatus.Ignored : entry.Status;
					if(cullNonAssetPaths && !UniGitPathHelper.IsPathInAssetFolder(currentProjectPath) && !UniGitPathHelper.IsPathInPackagesFolder(currentProjectPath)) continue;
					AddRecursive(0, entries);
				}

				foreach (var module in subModules)
				{
					currentPathArray = UniGitPathHelper.Combine(paths.RepoProjectRelativePath,module.Path).Split('\\');
					currentSubModuleStatus = module.Status;
					AddSubModuleRecursive(0, entries);
				}
			}

            private void AddRecursive(int entryNameIndex, Dictionary<string, StatusTreeEntry> entries)
            {
                while (true)
                {
                    var pathChunk = currentPathArray[entryNameIndex].Replace(".meta", "");

                    //should a state change be marked at this level (inverse depth)
                    //bool markState = Settings.ProjectStatusOverlayDepth < 0 || (Mathf.Abs(currentPathArray.Length - entryNameIndex)) <= Math.Max(1, Settings.ProjectStatusOverlayDepth);
                    //markState = true;
                    if (entries.TryGetValue(pathChunk, out var entry))
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
                        entryNameIndex = entryNameIndex + 1;
                        entries = entry.SubEntiEntries;
                        continue;
                    }

                    break;
                }
            }

            private void AddSubModuleRecursive(int entryNameIndex, Dictionary<string, StatusTreeEntry> entries)
            {
                while (true)
                {
                    var pathChunk = currentPathArray[entryNameIndex].Replace(".meta", "");
                    if (!entries.TryGetValue(pathChunk, out var entry))
                    {
                        entry = new StatusTreeEntry(entryNameIndex);
                        entries.Add(pathChunk, entry);
                    }

                    if (entryNameIndex < currentPathArray.Length - 1)
                    {
                        entryNameIndex = entryNameIndex + 1;
                        entries = entry.SubEntiEntries;
                        continue;
                    }

                    entry.isSubModule = true;
                    entry.SubmoduleStatus = currentSubModuleStatus;
                    entry.forceStatus = true;

                    break;
                }
            }

            public StatusTreeEntry GetStatus(string path)
			{
                GetStatusRecursive(0, path.Split(new[] { UniGitPathHelper.UnityDeirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries), entries, out var entry);
				return entry;
			}

			private void GetStatusRecursive(int entryNameIndex, string[] path, Dictionary<string, StatusTreeEntry> entries, out StatusTreeEntry entry)
			{
				if (path.Length <= 0)
				{
					entry = null;
					return;
				}

                if (entries.TryGetValue(path[entryNameIndex], out var entryTmp))
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
            internal bool forceStatus;
            public FileStatus State { get; set; }
			internal bool isSubModule;
			public SubmoduleStatus SubmoduleStatus { get; set; }

			public StatusTreeEntry(int depth)
			{
				this.Depth = depth;
			}

			public int Depth { get; }

            public bool ForceStatus => forceStatus;

            public bool IsSubModule => isSubModule;

            public Dictionary<string, StatusTreeEntry> SubEntiEntries { get; set; } = new Dictionary<string, StatusTreeEntry>();
        }
		#endregion
	}
}