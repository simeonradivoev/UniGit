using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Utils.Extensions;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace UniGit
{
	public static class GitManager
	{
		public const string GitDirectory = @"C:\Program Files\Git";

		private static string repoPathCached;
		public static string RepoPath { get { return repoPathCached; } }

		private static Repository repository;
		private static StatusTreeClass statusTree;
		private static GitCredentials gitCredentials;
		private static GitSettings gitSettings;
		internal static Icons icons;
		private static bool needsFetch;
		private readonly static Queue<Action> actionQueue = new Queue<Action>();
		private static GitRepoStatus status;
		private static object statusTreeLock = new object();
		private static object statusRetriveLock = new object();
		private static bool repositoryDirty;
		private static bool reloadDirty;
		private static readonly List<string> dirtyFiles = new List<string>(); 

		public class Icons
		{
			public GUIContent validIcon;
			public GUIContent validIconSmall;
			public GUIContent modifiedIcon;
			public GUIContent modifiedIconSmall;
			public GUIContent addedIcon;
			public GUIContent addedIconSmall;
			public GUIContent untrackedIcon;
			public GUIContent untrackedIconSmall;
			public GUIContent ignoredIcon;
			public GUIContent ignoredIconSmall;
			public GUIContent conflictIcon;
			public GUIContent conflictIconSmall;
			public GUIContent deletedIcon;
			public GUIContent deletedIconSmall;
			public GUIContent renamedIcon;
			public GUIContent renamedIconSmall;
			public GUIContent loadingIconSmall;
			public GUIContent objectIcon;
			public GUIContent objectIconSmall;
			public GUIContent metaIcon;
			public GUIContent metaIconSmall;
		}

		private static GUIStyle IconStyle;

		[InitializeOnLoadMethod]
		internal static void Initlize()
		{
			repoPathCached = Application.dataPath.Replace("/Assets", "").Replace("/", "\\");

			if (!IsValidRepo)
			{
				return;
			}

			gitCredentials = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
			gitSettings = EditorGUIUtility.Load("UniGit/Git-Settings.asset") as GitSettings;
			if (gitSettings == null)
			{
				gitSettings = ScriptableObject.CreateInstance<GitSettings>();
				AssetDatabase.CreateAsset(gitSettings, "Assets/Editor Default Resources/UniGit/Git-Settings.asset");
				AssetDatabase.SaveAssets();
			}

			if (IconStyle == null)
			{
				IconStyle = new GUIStyle
				{
					imagePosition = ImagePosition.ImageOnly,
					alignment = TextAnchor.LowerLeft,
					padding = new RectOffset(2, 2, 2, 2)
				};
			}

			if (icons == null)
			{
				icons = new Icons()
				{
					validIcon = EditorGUIUtility.IconContent("UniGit/success") ,
					validIconSmall = EditorGUIUtility.IconContent("UniGit/success_small"),
					modifiedIcon = EditorGUIUtility.IconContent("UniGit/error"),
					modifiedIconSmall = EditorGUIUtility.IconContent("UniGit/error_small"),
					addedIcon = EditorGUIUtility.IconContent("UniGit/add"),
					addedIconSmall = EditorGUIUtility.IconContent("UniGit/add_small"),
					untrackedIcon = EditorGUIUtility.IconContent("UniGit/info"),
					untrackedIconSmall = EditorGUIUtility.IconContent("UniGit/info_small"),
					ignoredIcon = EditorGUIUtility.IconContent("UniGit/minus"),
					ignoredIconSmall = EditorGUIUtility.IconContent("UniGit/minus_small"),
					conflictIcon = EditorGUIUtility.IconContent("UniGit/warning"),
					conflictIconSmall = EditorGUIUtility.IconContent("UniGit/warning_small"),
					deletedIcon = EditorGUIUtility.IconContent("UniGit/deleted"),
					deletedIconSmall = EditorGUIUtility.IconContent("UniGit/deleted_small"),
					renamedIcon = EditorGUIUtility.IconContent("UniGit/renamed"),
					renamedIconSmall = EditorGUIUtility.IconContent("UniGit/renamed_small"),
					loadingIconSmall = EditorGUIUtility.IconContent("UniGit/loading"),
					objectIcon = EditorGUIUtility.IconContent("UniGit/object"),
					objectIconSmall = EditorGUIUtility.IconContent("UniGit/object_small"),
					metaIcon = EditorGUIUtility.IconContent("UniGit/meta"),
					metaIconSmall = EditorGUIUtility.IconContent("UniGit/meta_small")
				};
			}

			EditorApplication.projectWindowItemOnGUI += CustomIcons;

			GitLfsManager.Load();
			GitHookManager.Load();
			GitExternalManager.Load();
			GitCredentialsManager.Load();

			needsFetch = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
			repositoryDirty = true;
			EditorApplication.update += OnEditorUpdate;
		}

		internal static void OnEditorUpdate()
		{
			if (needsFetch)
			{
				try
				{
					needsFetch = AutoFetchChanges();
#if UNITY_EDITOR
					Debug.Log("Auto Fetch");
#endif
				}
				catch(Exception e)
				{
#if UNITY_EDITOR
					Debug.LogException(e);
#endif
					needsFetch = false;
				}
			}

			if (IsValidRepo && !(EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
			{
				if ((repository == null || repositoryDirty))
				{
					Update(reloadDirty);
					reloadDirty = false;
					repositoryDirty = false;
					dirtyFiles.Clear();
				}
				else if (dirtyFiles.Count > 0)
				{
					Update(reloadDirty || repository == null, dirtyFiles.ToArray());
					dirtyFiles.Clear();
				}
			}

			if (actionQueue.Count > 0)
			{
				Action action = actionQueue.Dequeue();
				if (action != null)
				{
					try
					{
						action.Invoke();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
						throw;
					}
				}
			}
		}

		private static void Update(bool reloadRepository,string[] paths = null)
		{
			if ((repository == null || reloadRepository) && IsValidRepo)
			{
				if (repository != null) repository.Dispose();
				repository = new Repository(RepoPath);
				GitCallbacks.IssueOnRepositoryLoad(repository);
			}

			if (repository != null)
			{
				if (Settings.GitStatusMultithreaded)
				{
					ThreadPool.QueueUserWorkItem(ReteriveStatusThreaded, paths);
				}
				else
				{
					RetreiveStatus(paths);
				}
			}
		}

		public static void MarkDirty()
		{
			repositoryDirty = true;
		}

		public static void MarkDirty(bool reloadRepo)
		{
			repositoryDirty = true;
			reloadDirty = reloadRepo;
		}

		public static void MarkDirty(string[] paths)
		{
			MarkDirty((IEnumerable<string>)paths);
		}

		public static void MarkDirty(IEnumerable<string> paths)
		{
			dirtyFiles.AddRange(paths.Select(s => s.Replace("/","\\")));
		}

		private static void RebuildStatus(string[] paths)
		{
			if (paths != null && paths.Length > 0 && status != null)
			{
				foreach (var path in paths)
				{
					status.Update(path, repository.RetrieveStatus(path));
				}
			}
			else
			{
				status = new GitRepoStatus(repository.RetrieveStatus());
			}
			
		}

		private static void RetreiveStatus(string[] paths)
		{
			GitProfilerProxy.BeginSample("Git Repository Status Retrieval");
			RebuildStatus(paths);
			GitProfilerProxy.EndSample();
			GitCallbacks.IssueUpdateRepository(status,paths);
			ThreadPool.QueueUserWorkItem(UpdateStatusTreeThreaded, status);
		}

		private static void ReteriveStatusThreaded(object param)
		{
			Monitor.Enter(statusRetriveLock);
			try
			{
				string[] paths = param as string[];
				RebuildStatus(paths);
				actionQueue.Enqueue(() =>
				{
					GitCallbacks.IssueUpdateRepository(status, paths);
					ThreadPool.QueueUserWorkItem(UpdateStatusTreeThreaded, status);
				});
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				Monitor.Exit(statusRetriveLock);
			}
		}

		private static void UpdateStatusTreeThreaded(object statusObj)
		{
			Monitor.Enter(statusTreeLock);
			try
			{
				GitRepoStatus status = (GitRepoStatus) statusObj;
				statusTree = new StatusTreeClass(status);
				actionQueue.Enqueue(RepaintProjectWidnow);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				Monitor.Exit(statusTreeLock);
			}
		}

		public static Texture2D GetGitStatusIcon()
		{
			if (!IsValidRepo) return EditorGUIUtility.FindTexture("CollabNew");
			if (Repository == null) return EditorGUIUtility.FindTexture("Collab");
			if (Repository.Index.Conflicts.Any()) return EditorGUIUtility.FindTexture("CollabConflict");
			int? behindBy = Repository.Head.TrackingDetails.BehindBy;
			int? aheadBy = Repository.Head.TrackingDetails.AheadBy;
			if (behindBy.GetValueOrDefault(0) > 0)
			{
				return EditorGUIUtility.FindTexture("CollabPull");
			}
			if (aheadBy.GetValueOrDefault(0) > 0)
			{
				return EditorGUIUtility.FindTexture("CollabPush");
			}
			return EditorGUIUtility.FindTexture("Collab");
		}

		public static GUIContent GetDiffTypeIcon(FileStatus type,bool small)
		{
			GUIContent content = null;

			if (type.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.ModifiedInIndex))
			{
				content = small ? icons.modifiedIconSmall : icons.modifiedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInIndex))
			{
				content = small ? icons.addedIconSmall : icons.addedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInWorkdir))
			{
				content = small ? icons.untrackedIconSmall : icons.untrackedIcon;
			}
			if (type.IsFlagSet(FileStatus.Ignored))
			{
				content = small ? icons.ignoredIconSmall : icons.ignoredIcon;
			}
			if (type.IsFlagSet(FileStatus.Conflicted))
			{
				content = small ? icons.conflictIconSmall : icons.conflictIcon;
			}
			if (type.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				content = small ? icons.renamedIconSmall : icons.renamedIcon;
			}
			if (type.IsFlagSet(FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir))
			{
				content = small ? icons.deletedIconSmall : icons.deletedIcon;
			}
			return content != null ? SetupTooltip(content, type) : GUIContent.none;
		}

		private static GUIContent SetupTooltip(GUIContent content,FileStatus type)
		{
			content.tooltip = type.ToString();
			return content;
		}

		public static GUIContent GetDiffTypeIcon(ChangeKind type,bool small)
		{
			switch (type)
			{
				case ChangeKind.Unmodified:
					return small ? icons.validIconSmall : icons.validIcon;
				case ChangeKind.Added:
					return small ? icons.addedIconSmall : icons.addedIcon;
				case ChangeKind.Deleted:
					return small ? icons.deletedIconSmall : icons.deletedIcon;
				case ChangeKind.Modified:
					return small ? icons.modifiedIconSmall : icons.modifiedIcon;
				case ChangeKind.Ignored:
					return small ? icons.ignoredIconSmall : icons.ignoredIcon;
				case ChangeKind.Untracked:
					return small ? icons.untrackedIconSmall : icons.untrackedIcon;
				case ChangeKind.Conflicted:
					return small ? icons.conflictIconSmall : icons.conflictIcon;
				case ChangeKind.Renamed:
					return small ? icons.renamedIconSmall : icons.renamedIcon;
			}
			return null;
		}

		private static void CustomIcons(string guid, Rect rect)
		{
			if(statusTree == null) return;
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var status = statusTree.GetStatus(path);
			if (status != null)
			{
				Object assetObject = AssetDatabase.LoadMainAssetAtPath(path);
				if (assetObject != null && ProjectWindowUtil.IsFolder(assetObject.GetInstanceID()))
				{
					//exclude the Assets folder
					if(status.Depth == 0) return;
					//todo cache expandedProjectWindowItems into a HashSet for faster Contains
					if (!status.ForceStatus && InternalEditorUtility.expandedProjectWindowItems.Contains(assetObject.GetInstanceID())) return;
				}
				DrawFileIcon(rect, GetDiffTypeIcon(status.State,rect.height <= 16));
			}
		}

		private static void DrawFileIcon(Rect rect, GUIContent icon)
		{
			float width = Mathf.Min(rect.width, 32);
			float height = Mathf.Min(rect.height, 32);
			GUI.Label(new Rect(rect.x + rect.width - width, rect.y, width, height), icon, IconStyle);
		}

		#region Auto Fetching
		private static bool AutoFetchChanges()
		{
			if (repository == null || !IsValidRepo || !Settings.AutoFetch) return false;
			Remote remote = repository.Network.Remotes.FirstOrDefault();
			if (remote == null) return false;
			GitProfilerProxy.BeginSample("Git automatic fetching");
			try
			{

				repository.Network.Fetch(remote, new FetchOptions() { CredentialsProvider = GitCredentialsManager.FetchChangesAutoCredentialHandler, OnTransferProgress = FetchTransferProgressHandler });
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("Automatic Fetching from remote: {0} with URL: {1} Failed!", remote.Name, remote.Url);
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			GitProfilerProxy.EndSample();
			return false;
		}
		#endregion

		#region Helpers

		public static void RepaintProjectWidnow()
		{
			Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
			var projectWindow = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
			if (projectWindow != null)
			{
				((EditorWindow)projectWindow).Repaint();
			}
		}

		public static bool IsEmptyFolderMeta(string path)
		{
			if (path.EndsWith(".meta"))
			{
				return IsEmptyFolder(path.Substring(0, path.Length - 5));
			}
			return false;
		}

		public static bool IsEmptyFolder(string path)
		{
			if (Directory.Exists(path))
			{
				return Directory.GetFileSystemEntries(path).Length <= 0;
			}
			return false;
		}

		public static bool CanStage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.NewInWorkdir | FileStatus.RenamedInWorkdir | FileStatus.TypeChangeInWorkdir | FileStatus.DeletedFromWorkdir);
		}

		public static bool CanUnstage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInIndex | FileStatus.NewInIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex | FileStatus.DeletedFromIndex);
		}

		public static void ShowDiff(string path, [NotNull] Commit start,[NotNull] Commit end)
		{
			GitExternalManager.ShowDiff(path,start,end);
		}

		public static void ShowDiff(string path)
		{
			if (string.IsNullOrEmpty(path) ||  Repository == null) return;
			GitExternalManager.ShowDiff(path);
		}

		public static void ShowDiffPrev(string path)
		{
			if (string.IsNullOrEmpty(path) && Repository != null) return;

			TreeEntry entry = null;
			string lastId = null;
			foreach (var commit in Repository.Head.Commits)
			{
				TreeEntry e = commit.Tree[path];
				if (e == null) continue;
				if (lastId == null)
				{
					lastId = e.Target.Sha;
				}
				else if (lastId != e.Target.Sha)
				{
					entry = e;
					break;
				}
			}

			if (entry == null) return;
			Blob blob = entry.Target as Blob;
			if (blob == null || blob.IsBinary) return;
			string newPath = Application.dataPath.Replace("Assets", "Temp/") + "Git-diff-tmp-file";
			if (!string.IsNullOrEmpty(newPath))
			{
				using (FileStream file = File.Create(newPath))
				{
					blob.GetContentStream().CopyTo(file);
				}
			}
			EditorUtility.InvokeDiffTool(Path.GetFileName(path) + " - " + entry.Target.Sha, newPath, Path.GetFileName(path) + " - Working Tree", path, "", "");
		}
		#endregion

		#region Enumeration helpers

		public static IEnumerable<string> GetPathWithMeta(string path)
		{
			if (path.EndsWith(".meta"))
			{
				if (Path.HasExtension(path)) yield return path;
				string assetPath = AssetDatabase.GetAssetPathFromTextMetaFilePath(path);
				if (!string.IsNullOrEmpty(assetPath))
				{
					yield return assetPath;
				}
			}
			else
			{
				if (Path.HasExtension(path)) yield return path;
				string metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(path);
				if (!string.IsNullOrEmpty(metaPath))
				{
					yield return metaPath;
				}
			}
		}

		public static IEnumerable<string> GetPathsWithMeta(IEnumerable<string> paths)
		{
			return paths.SelectMany(p => GetPathWithMeta(p));
		}
		#endregion

		#region Progress Handlers
		public static bool FetchTransferProgressHandler(TransferProgress progress)
		{
			float percent = (float)progress.ReceivedObjects / progress.TotalObjects;
			bool cancel = EditorUtility.DisplayCancelableProgressBar("Transferring", string.Format("Transferring: Received total of: {0} bytes. {1}%", progress.ReceivedBytes, (percent * 100).ToString("###")), percent);
			if (progress.TotalObjects == progress.ReceivedObjects)
			{
#if UNITY_EDITOR
				Debug.Log("Transfer Complete. Received a total of " + progress.IndexedObjects + " objects");
#endif
			}
			//true to continue
			return !cancel;
		}
		#endregion

		public static void DisablePostprocessing()
		{
			EditorPrefs.SetBool("UniGit_DisablePostprocess",true);
		}

		public static void EnablePostprocessing()
		{
			EditorPrefs.SetBool("UniGit_DisablePostprocess", false);
		}

		#region Getters and Setters
		public static Signature Signature
		{
			get { return new Signature(Repository.Config.GetValueOrDefault<string>("user.name"), Repository.Config.GetValueOrDefault<string>("user.email"),DateTimeOffset.Now);}
		}

		public static bool IsValidRepo
		{
			get { return Repository.IsValid(RepoPath); }
		}

		public static Repository Repository
		{
			get { return repository; }
		}

		public static StatusTreeClass StatusTree
		{
			get { return statusTree; }
		}

		public static GitCredentials GitCredentials
		{
			get { return gitCredentials; }
			internal set { gitCredentials = value; }
		}

		public static GitSettings Settings
		{
			get { return gitSettings; }
		}

		public static GitRepoStatus LastStatus
		{
			get { return status; }
		}

		public static Queue<Action> ActionQueue
		{
			get { return actionQueue; }
		}

		#endregion

		#region Status Tree
		public class StatusTreeClass
		{
			private Dictionary<string, StatusTreeEntry> entries = new Dictionary<string, StatusTreeEntry>();
			private string currentPath;
			private string[] currentPathArray;
			private FileStatus currentStatus;

			public StatusTreeClass(IEnumerable<GitStatusEntry> status)
			{
				Build(status);
			}

			private void Build(IEnumerable<GitStatusEntry> status)
			{
				foreach (var entry in status)
				{
					currentPath = entry.Path;
					currentPathArray = entry.Path.Split('\\');
					currentStatus = !Settings.ShowEmptyFolders && IsEmptyFolderMeta(currentPath) ? FileStatus.Ignored : entry.Status;
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
				if (currentPathArray.Length - entryNameIndex < (Settings.ProjectStatusOverlayDepth+1))
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
				GetStatusRecursive(0, path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries), entries, out entry);
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
			private int depth;
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