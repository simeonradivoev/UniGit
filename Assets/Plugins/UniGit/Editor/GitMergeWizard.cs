using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
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
		private GitCallbacks gitCallbacks;

		[UniGitInject]
		private void Construct(GitCallbacks gitCallbacks)
		{
			this.gitCallbacks = gitCallbacks;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			mergeOptions = new MergeOptions() { CommitOnSuccess = commitOnSuccess, OnCheckoutNotify = OnCheckoutNotify, OnCheckoutProgress = OnCheckoutProgress, FastForwardStrategy = fastForwardStrategy ,FileConflictStrategy = (CheckoutFileConflictStrategy)((int)mergeFileFavor), CheckoutNotifyFlags = CheckoutNotifyFlags.Updated};
		}

		protected override bool DrawWizardGUI()
		{
			prune = EditorGUILayout.Toggle(GitGUI.GetTempContent("Prune", "Prune all unreachable objects from the object database"), prune);
			commitOnSuccess = EditorGUILayout.Toggle(GitGUI.GetTempContent("Commit on success"), commitOnSuccess);
			fastForwardStrategy = (FastForwardStrategy)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("Fast Forward Strategy"), fastForwardStrategy);
			mergeFileFavor = (ConflictMergeType)EditorGUILayout.EnumPopup(GitGUI.GetTempContent("File Merge Favor"), mergeFileFavor);
			return false;
		}


		[UsedImplicitly]
		private void OnWizardCreate()
		{
			try
			{
				MergeResult result = gitManager.Repository.MergeFetchedRefs(gitManager.Signature, mergeOptions);
				OnMergeComplete(result,"Merge");
				gitManager.MarkDirty();
				gitCallbacks.IssueAssetDatabaseRefresh();
			}
			catch (CheckoutConflictException e)
			{
				logger.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}
	}
}