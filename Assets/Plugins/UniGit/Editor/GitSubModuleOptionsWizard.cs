using System;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitSubModuleOptionsWizard : GitWizardBase
	{
		[SerializeField] private bool init;
		[SerializeField] private string path;
		private SubmoduleUpdateOptions options;
		private GitManager gitManager;
		private Submodule submodule;

		[UniGitInject]
		private void Construct(GitManager gitManager)
		{
			this.gitManager = gitManager;
			options.OnTransferProgress = gitManager.FetchTransferProgressHandler;
			options.OnCheckoutNotify = gitManager.CheckoutNotifyHandler;
			options.OnCheckoutProgress = gitManager.CheckoutProgressHandler;
			options.RepositoryOperationCompleted = OnComplete;
			options.RepositoryOperationStarting = OnStarting;
			createButtonName = "Update";
			titleContent = new GUIContent("Sub Module Options");
			if(!string.IsNullOrEmpty(path))
				submodule = gitManager.Repository.Submodules[path];
		}

		internal void Init(string path)
		{
			this.path = path;
			submodule = gitManager.Repository.Submodules[path];
		}

		protected override void OnEnable()
		{
			options = new SubmoduleUpdateOptions()
			{
				CredentialsProvider = CredentialsHandler,
				OnProgress = FetchProgress,
				RepositoryOperationStarting = FetchOperationStarting
			};
			base.OnEnable();
		}

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			GUILayout.Label(path,EditorStyles.largeLabel);
			if (submodule != null)
			{
				GUILayout.Label("URL: " + submodule.Url,EditorStyles.miniLabel);
				if(submodule.WorkDirCommitId != null)
					GUILayout.Label("Workdir: " + submodule.WorkDirCommitId.Sha,EditorStyles.miniLabel);
				if(submodule.HeadCommitId != null)
					GUILayout.Label("Head: " + submodule.HeadCommitId.Sha,EditorStyles.miniLabel);
			}
			EditorGUILayout.Space();
			DrawCredentials();
			init = EditorGUILayout.Toggle(GitGUI.GetTempContent("Init"), init);
			return false;
		}

		private bool OnStarting(RepositoryOperationContext context)
		{
			logger.LogFormat(LogType.Log,"Module '{0}' update starting from '{1}'",context.SubmoduleName,context.RemoteUrl);
			return true;
		}

		private void OnComplete(RepositoryOperationContext context)
		{
			logger.LogFormat(LogType.Log,"Module '{0}' update complete from '{1}'",context.SubmoduleName,context.RemoteUrl);
		}

		[UsedImplicitly]
		private void OnWizardCreate()
		{
			options.Init = init;
			try
			{
				gitManager.Repository.Submodules.Update(path, options);
			}
			catch (Exception e)
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
