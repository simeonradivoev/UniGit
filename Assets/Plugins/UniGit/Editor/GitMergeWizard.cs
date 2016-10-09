using System;
using JetBrains.Annotations;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitMergeWizard : GitWizardBase
	{
		private MergeOptions mergeOptions;
		[SerializeField]
		private bool prune;
		[SerializeField]
		private bool commitOnSuccess;
		[SerializeField]
		private FastForwardStrategy fastForwardStrategy;
		[SerializeField] private ConflictMergeType mergeFileFavor;

		protected override void OnEnable()
		{
			base.OnEnable();
			mergeOptions = new MergeOptions() { CommitOnSuccess = commitOnSuccess, OnCheckoutNotify = OnCheckoutNotify, OnCheckoutProgress = OnCheckoutProgress, FastForwardStrategy = fastForwardStrategy ,FileConflictStrategy = (CheckoutFileConflictStrategy)((int)mergeFileFavor)};
		}

		protected override bool DrawWizardGUI()
		{
			prune = EditorGUILayout.Toggle(new GUIContent("Prune", "Prune all unreachable objects from the object database"), prune);
			commitOnSuccess = EditorGUILayout.Toggle(new GUIContent("Commit on success"), commitOnSuccess);
			fastForwardStrategy = (FastForwardStrategy)EditorGUILayout.EnumPopup(new GUIContent("Fast Forward Strategy"), fastForwardStrategy);
			mergeFileFavor = (ConflictMergeType)EditorGUILayout.EnumPopup(new GUIContent("File Merge Favor"), mergeFileFavor);
			return false;
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			try
			{
				MergeResult result = GitManager.Repository.MergeFetchedRefs(GitManager.Signature, mergeOptions);
				GitHistoryWindow.GetWindow(true);
				OnMergeComplete(result,"Merge");
				GitManager.Update();
				AssetDatabase.Refresh();
			}
			catch (CheckoutConflictException e)
			{
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}
	}
}