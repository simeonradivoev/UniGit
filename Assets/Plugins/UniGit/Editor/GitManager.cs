using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using LibGit2Sharp;
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

		public static string RepoPath { get { return Application.dataPath.Replace("/Assets", "").Replace("/", "\\"); } }

		public static event Action<RepositoryStatus> updateRepository;
		private static Repository repository;
		private static StatusTreeClass statusTree;
		private static GitCredentials gitCredentials;
		private static GitSettings gitSettings;
		internal static Icons icons;
		private static bool needsFetch;

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
		}

		private static GUIStyle IconStyle;

		[InitializeOnLoadMethod]
		internal static void Initlize()
		{
			if (!IsValidRepo)
			{
				return;
			}

			gitCredentials = EditorGUIUtility.Load("UniGit/Git-Credentials.asset") as GitCredentials;
			gitSettings = EditorGUIUtility.LoadRequired("UniGit/Git-Settings.asset") as GitSettings;
			if (gitSettings == null)
			{
				gitSettings = ScriptableObject.CreateInstance<GitSettings>();
				AssetDatabase.CreateAsset(gitSettings, "Assets/Editor Default Resources/UniGit/Git-Settings.asset");
				AssetDatabase.SaveAssets();
			}
			repository = new Repository(RepoPath);
			Update();

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
					renamedIconSmall = EditorGUIUtility.IconContent("UniGit/renamed_small")
				};
			}

			EditorApplication.projectWindowItemOnGUI += CustomIcons;

			GitLfsManager.Load();
			GitHookManager.Load();
			GitExternalManager.Load();
			GitCredentialsManager.Load();

			needsFetch = true;
			EditorApplication.update += OnEditorUpdate;
			
		}

		internal static void OnEditorUpdate()
		{
			if (needsFetch)
			{
				try
				{
					AutoFetchChanges();
				}
				finally
				{
					needsFetch = false;
				}
			}
		}

		internal static void Update()
		{
			Update(false);
		}

		internal static void Update(bool reloadRepository)
		{
			if (reloadRepository || (repository == null && IsValidRepo))
			{
				if(repository != null) repository.Dispose();
				repository = new Repository(RepoPath);
			}

			if (repository != null)
			{
				RepositoryStatus repoStatus = repository.RetrieveStatus();
				if(updateRepository != null) updateRepository.Invoke(repoStatus);
				statusTree = new StatusTreeClass(repoStatus);
			}
		}

		public static Texture2D GetGitStatusIcon()
		{
			if (!IsValidRepo) return EditorGUIUtility.FindTexture("CollabNew");
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
			if (type.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.ModifiedInIndex))
			{
				return small ? icons.modifiedIconSmall : icons.modifiedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInIndex))
			{
				return small ? icons.addedIconSmall : icons.addedIcon;
			}
			if (type.IsFlagSet(FileStatus.NewInWorkdir))
			{
				return small ? icons.untrackedIconSmall : icons.untrackedIcon;
			}
			if (type.IsFlagSet(FileStatus.Ignored))
			{
				return small ? icons.ignoredIconSmall : icons.ignoredIcon;
			}
			if (type.IsFlagSet(FileStatus.Conflicted))
			{
				return small ? icons.conflictIconSmall : icons.conflictIcon;
			}
			if (type.IsFlagSet(FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir))
			{
				return small ? icons.renamedIconSmall : icons.renamedIcon;
			}
			return new GUIContent();
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
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var status = statusTree.GetStatus(path);
			if (status != null)
			{
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
		private static void AutoFetchChanges()
		{
			if (!IsValidRepo || !Settings.AutoFetch) return;
			using (Repository repository = new Repository(RepoPath))
			{
				Remote remote = repository.Network.Remotes.FirstOrDefault();
				if (remote == null) return;
				Profiler.BeginSample("Git automatic fetching");
				try
				{
					Repository.Network.Fetch(remote, new FetchOptions() {CredentialsProvider = GitCredentialsManager.FetchChangesAutoCredentialHandler,OnTransferProgress = FetchTransferProgressHandler });
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
				Profiler.EndSample();
			}
		}
		#endregion

		#region Helpers

		public static bool CanStage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInWorkdir | FileStatus.NewInWorkdir | FileStatus.RenamedInWorkdir | FileStatus.TypeChangeInWorkdir | FileStatus.DeletedFromWorkdir);
		}

		public static bool CanUnstage(FileStatus fileStatus)
		{
			return fileStatus.IsFlagSet(FileStatus.ModifiedInIndex | FileStatus.NewInIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex | FileStatus.DeletedFromIndex);
		}

		public static void ShowDiff(string path,Commit commit,[CanBeNull] Commit other)
		{
			TreeEntry entry = commit[path];

			if (entry != null)
			{
				Blob blob = entry.Target as Blob;
				if (blob == null || blob.IsBinary) return;
				string oldFilePath = Application.dataPath.Replace("Assets", "Temp/") + "Git-diff-tmp-file";
				if (!string.IsNullOrEmpty(oldFilePath))
				{
					using (FileStream file = File.Create(oldFilePath))
					{
						blob.GetContentStream().CopyTo(file);
					}
				}
				string otherFilePath = path;
				if (other != null)
				{
					TreeEntry otherEntry = other[path];
					if (otherEntry == null) return;
					Blob otherBlob = otherEntry.Target as Blob;
					if (otherBlob == null || otherBlob.IsBinary) return;
					otherFilePath = Application.dataPath.Replace("Assets", "Temp/") + "Git-diff-tmp-file-other";
					if (string.IsNullOrEmpty(otherFilePath)) return;
					using (FileStream file = File.Create(otherFilePath))
					{
						otherBlob.GetContentStream().CopyTo(file);
					}
				}

				var asset = AssetDatabase.LoadAssetAtPath(path,typeof(Object));
				GitExternalManager.ShowDiff(Path.GetFileName(path) + " - " + entry.Target.Sha, oldFilePath, Path.GetFileName(path) + " - " + (other == null ? "Working Tree" : other.Id.Sha), otherFilePath, asset != null ? asset.GetType() : null);
			}
		}

		public static void ShowDiff(string path)
		{
			if (string.IsNullOrEmpty(path) ||  Repository == null) return;
			if (Repository.Head != null && Repository.Head.Tip != null)
			{
				ShowDiff(path, Repository.Head.Tip,null);
			}
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
				yield return path.Substring(path.Length - 5, 5);
				if (Path.HasExtension(path)) yield return path;
			}
			else
			{
				if (Path.HasExtension(path)) yield return path;
				yield return path + ".meta";
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
				Debug.Log("Transfer Complete. Received a total of " + progress.IndexedObjects + " objects");
			}
			//true to continue
			return !cancel;
		}
		#endregion

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
		#endregion

		#region Status Tree
		public class StatusTreeClass
		{
			private Dictionary<string, StatusTreeEntry> entries = new Dictionary<string, StatusTreeEntry>();

			public StatusTreeClass(IEnumerable<StatusEntry> status)
			{
				Build(status);
			}

			private void Build(IEnumerable<StatusEntry> status)
			{
				foreach (var entry in status)
				{
					string[] paths = entry.FilePath.Split('\\');
					AddRecursive(0, paths, entries, entry.State);
				}
			}

			private void AddRecursive(int entryNameIndex, string[] path, Dictionary<string, StatusTreeEntry> entries, FileStatus fileStatus)
			{
				StatusTreeEntry entry;
				string pathChunk = path[entryNameIndex].Replace(".meta", "");
				if (entries.TryGetValue(pathChunk, out entry))
				{
					entry.State = entry.State.SetFlags(fileStatus, true);
				}
				else
				{
					entry = new StatusTreeEntry(fileStatus);
					entries.Add(pathChunk, entry);
				}
				if (entryNameIndex < path.Length - 1)
				{
					AddRecursive(entryNameIndex + 1, path, entry.SubEntiEntries, fileStatus);
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
			private FileStatus state;

			public StatusTreeEntry(FileStatus state)
			{
				this.state = state;
			}

			public FileStatus State
			{
				get { return state; }
				set { state = value; }
			}

			public Dictionary<string, StatusTreeEntry> SubEntiEntries
			{
				get { return subEntiEntries; }
				set { subEntiEntries = value; }
			}
		}
		#endregion
	}

	#region Postprocessors
	public class GitAssetModificationPostprocessor : UnityEditor.AssetModificationProcessor
	{
		private static string[] OnWillSaveAssets(string[] paths)
		{
			if (GitManager.Settings != null && GitManager.Settings.AutoStage)
			{
				string[] pathsFinal = paths.SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
				if(pathsFinal.Length > 0) GitManager.Repository.Stage(pathsFinal);
			}
			GitManager.Update();
			return paths;
		}
	}

	public class GitBrowserAssetPostprocessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (GitManager.Settings != null && GitManager.Settings.AutoStage)
			{
				if (importedAssets.Length > 0)
				{
					string[] importedAssetsFinal = importedAssets.SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
					if(importedAssetsFinal.Length > 0) GitManager.Repository.Stage(importedAssetsFinal);
				}

				if (movedAssets.Length > 0)
				{
					string[] movedAssetsFinal = movedAssets.SelectMany(g => GitManager.GetPathWithMeta(g)).Where(g => GitManager.CanStage(GitManager.Repository.RetrieveStatus(g))).ToArray();
					if (movedAssetsFinal.Length > 0) GitManager.Repository.Stage(movedAssetsFinal);
				}
			}
			GitManager.Update();
		}
	}
	#endregion
}