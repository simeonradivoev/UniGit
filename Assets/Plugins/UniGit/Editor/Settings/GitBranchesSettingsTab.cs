using System;
using System.Linq;
using LibGit2Sharp;
using UniGit.Status;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit.Settings
{
	public class GitBranchesSettingsTab : GitSettingsTab
	{
		private string[] remoteNames;
		private GitRemotesSettingsTab.RemoteEntry[] remoteCacheList = new GitRemotesSettingsTab.RemoteEntry[0];
		private Vector2 scroll;
		private GitExternalManager externalManager;

		[UniGitInject]
		public GitBranchesSettingsTab(GitManager gitManager,GitSettingsWindow settingsWindow, GitExternalManager externalManager) 
			: base(new GUIContent("Branches"), gitManager,settingsWindow)
		{
			this.externalManager = externalManager;
		}

		internal override void OnGUI(Rect rect, Event current)
		{
			var branches = gitManager.Repository.Branches;
			DoBranch(gitManager.Repository.Head, branches);

			EditorGUILayout.Space();
			GUILayout.Label(GUIContent.none, "sv_iconselector_sep");
			EditorGUILayout.Space();

			scroll = EditorGUILayout.BeginScrollView(scroll);
			if (branches != null)
			{
				foreach (var branch in branches)
				{
					if (branch.IsCurrentRepositoryHead) continue;
					DoBranch(branch, branches);
				}
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			Rect createBranchRect = GUILayoutUtility.GetRect(GitGUI.GetTempContent("Create Branch"), GitGUI.Styles.AddComponentBtn);
			if (GUI.Button(createBranchRect, GitGUI.IconContent("ol plus", "Create Branch"), GitGUI.Styles.AddComponentBtn))
			{
				PopupWindow.Show(createBranchRect, new GitCreateBranchWindow(gitManager.Repository.Commits.FirstOrDefault(), () =>
				{
					branches = null;
				}, gitManager));
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		private void DoBranch(Branch branch,BranchCollection branchCollection)
		{
			bool isHead = branch.IsCurrentRepositoryHead;

			GUIContent titleContent = GitGUI.GetTempContent(branch.FriendlyName);
			if (isHead)
				titleContent.text += " (HEAD)";
			if(branch.IsRemote)
				titleContent.image = GitGUI.IconContentTex("ToolHandleGlobal");

			GUILayout.Label(titleContent, isHead ? "IN BigTitle" : "ShurikenModuleTitle", GUILayout.ExpandWidth(true));
			int selectedRemote = Array.IndexOf(remoteCacheList, branch.Remote);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("Remote"));
			if (remoteNames != null)
			{
				EditorGUI.BeginChangeCheck();
				int newSelectedRemote = EditorGUILayout.Popup(selectedRemote, remoteNames);
				if (EditorGUI.EndChangeCheck() && selectedRemote != newSelectedRemote)
				{
					branchCollection.Update(branch, (u) =>
					{
						u.Remote = remoteCacheList[newSelectedRemote].Name;
						u.UpstreamBranch = branch.CanonicalName;
					});
				}
			}
			else
			{
				GUILayout.Button(new GUIContent("No Remotes"));
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.TextField(GitGUI.GetTempContent("Upstream Branch"), branch.UpstreamBranchCanonicalName);
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = remoteCacheList != null && remoteCacheList.Length < selectedRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Save", "Send branch changes to selected remote."), EditorStyles.miniButtonLeft))
			{
				branchCollection.Update(branch, (u) =>
				{
					u.Remote = remoteCacheList[selectedRemote].Name;
					u.UpstreamBranch = branch.CanonicalName;
				});
			}
			GUI.enabled = !branch.IsRemote && !isHead;
			Rect switchButtonRect = GUILayoutUtility.GetRect(GitGUI.GetTempContent("Switch"), EditorStyles.miniButtonMid);
			if (GUI.Button(switchButtonRect,GitGUI.GetTempContent("Switch"), EditorStyles.miniButtonMid))
			{
				if (externalManager.TakeSwitch())
				{
					gitManager.Callbacks.IssueAssetDatabaseRefresh();
					gitManager.MarkDirty();
				}
				else
				{
					PopupWindow.Show(switchButtonRect,new GitCheckoutWindowPopup(gitManager,branch));
				}
			}
			GUI.enabled = !isHead;
			if (GUILayout.Button(GitGUI.GetTempContent("Delete", branch.IsCurrentRepositoryHead ? "Can not delete head branch" : ""), EditorStyles.miniButtonMid))
			{
				if (EditorUtility.DisplayDialog("Delete Branch", "Are you sure you want do delete a branch? This action can not be undone.", "Delete", "Cancel"))
				{
					try
					{
						gitManager.Repository.Branches.Remove(branch);
						gitManager.MarkDirty(true);
					}
					catch (Exception e)
					{
						Debug.Log("Could not delete branch: " + branch.CanonicalName);
						Debug.LogException(e);
					}
				}
			}
			GUI.enabled = !branch.IsRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Reset", "Reset branch properties."), EditorStyles.miniButtonRight))
			{
				branchCollection.Update(branch, (u) =>
				{
					u.Remote = "";
					u.UpstreamBranch = "";
				});
			}
			GUI.enabled = true;
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		public override void OnGitUpdate(GitRepoStatus status, string[] paths)
		{
			base.OnGitUpdate(status, paths);
			if (gitManager.Repository == null) return;
			UpdateRemotes();
		}

		private void UpdateRemotes()
		{
			var remotes = gitManager.Repository.Network.Remotes;
			remoteCacheList = remotes.Select(r => new GitRemotesSettingsTab.RemoteEntry(r)).ToArray();
			remoteNames = remotes.Select(r => r.Name).ToArray();
		}
	}
}