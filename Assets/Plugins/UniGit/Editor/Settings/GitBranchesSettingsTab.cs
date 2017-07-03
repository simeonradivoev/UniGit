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
		private BranchCollection branches;
		private string[] remoteNames;
		private GitRemotesSettingsTab.RemoteEntry[] remoteCacheList = new GitRemotesSettingsTab.RemoteEntry[0];

		internal override void OnGUI(Rect rect, Event current)
		{
			DoBranch(GitManager.Repository.Head);

			EditorGUILayout.Space();
			GUILayout.Label(GUIContent.none, "sv_iconselector_sep");
			EditorGUILayout.Space();

			if (branches != null)
			{
				foreach (var branch in branches)
				{
					if (branch.IsCurrentRepositoryHead) continue;
					DoBranch(branch);
				}
			}

			EditorGUILayout.Space();
			Rect createBranchRect = GUILayoutUtility.GetRect(GitGUI.GetTempContent("Create Branch"), GUI.skin.button);
			if (GUI.Button(createBranchRect, GitGUI.GetTempContent("Create Branch")))
			{
				PopupWindow.Show(createBranchRect, new GitCreateBranchWindow(settingsWindow, GitManager.Repository.Commits.FirstOrDefault(), () => { branches = null; }));
			}
		}

		private void DoBranch(Branch branch)
		{
			GUILayout.Label(GitGUI.GetTempContent(branch.FriendlyName), branch.IsCurrentRepositoryHead ? "IN BigTitle" : "ShurikenModuleTitle", GUILayout.ExpandWidth(true));
			int selectedRemote = Array.IndexOf(remoteCacheList, branch.Remote);
			if (remoteNames != null)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("Remote"));
				EditorGUI.BeginChangeCheck();
				int newSelectedRemote = EditorGUILayout.Popup(selectedRemote, remoteNames);
				EditorGUILayout.EndHorizontal();
				if (EditorGUI.EndChangeCheck() && selectedRemote != newSelectedRemote)
				{
					branches.Update(branch, (u) =>
					{
						u.Remote = remoteCacheList[newSelectedRemote].Name;
						u.UpstreamBranch = branch.CanonicalName;
					});
				}
			}

			EditorGUILayout.TextField(GitGUI.GetTempContent("Upstream Branch"), branch.UpstreamBranchCanonicalName);
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = remoteCacheList != null && remoteCacheList.Length < selectedRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Save", "Send branch changes to selected remote."), "minibuttonleft"))
			{
				branches.Update(branch, (u) =>
				{
					u.Remote = remoteCacheList[selectedRemote].Name;
					u.UpstreamBranch = branch.CanonicalName;
				});
			}
			GUI.enabled = !branch.IsRemote && branch.IsCurrentRepositoryHead;
			if (GUILayout.Button("Switch", "minibuttonmid"))
			{
				if (GitExternalManager.TakeSwitch())
				{
					AssetDatabase.Refresh();
					GitManager.MarkDirty();
				}
				else
				{
					Debug.LogException(new NotImplementedException("Branch Checkout not implemented. Use External program for branch switching."));
					//todo implement branch checkout
				}
			}
			GUI.enabled = !branch.IsCurrentRepositoryHead;
			if (GUILayout.Button(GitGUI.GetTempContent("Delete", branch.IsCurrentRepositoryHead ? "Can not delete head branch" : ""), "minibuttonmid"))
			{
				if (EditorUtility.DisplayDialog("Delete Branch", "Are you sure you want do delete a branch? This action can not be undone.", "Delete", "Cancel"))
				{
					try
					{
						GitManager.Repository.Branches.Remove(branch);
						branches = null;
						GitManager.MarkDirty(true);
					}
					catch (Exception e)
					{
						Debug.Log("Could not delete branch: " + branch.CanonicalName);
						Debug.LogException(e);
					}
				}
			}
			GUI.enabled = !branch.IsRemote;
			if (GUILayout.Button(GitGUI.GetTempContent("Reset", "Reset branch properties."), "minibuttonright"))
			{
				branches.Update(branch, (u) =>
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
			if (GitManager.Repository == null) return;
			UpdateRemotes();
			UpdateBranches();
		}

		private void UpdateBranches()
		{
			branches = GitManager.Repository.Branches;
		}

		private void UpdateRemotes()
		{
			var remotes = GitManager.Repository.Network.Remotes;
			remoteCacheList = remotes.Select(r => new GitRemotesSettingsTab.RemoteEntry(r)).ToArray();
			remoteNames = remotes.Select(r => r.Name).ToArray();
		}
	}
}