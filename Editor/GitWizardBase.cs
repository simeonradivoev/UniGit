using System;
using System.Linq;
using System.Security;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitWizardBase : GitWizard
	{
		protected Remote[] remotes;
		protected GUIContent[] remoteNames;
		protected string[] branchNames;
		protected string[] branchFriendlyNames;
		[SerializeField] protected Credentials credentials;
		[SerializeField]
		protected int selectedRemote;
		[SerializeField]
		protected int selectedBranch;
		[SerializeField] protected bool credentalsExpanded;
		protected GitManager gitManager;
		protected GitCredentialsManager credentialsManager;
		protected GitExternalManager externalManager;
		protected ILogger logger;
		protected GitSettingsJson gitSettings;
		protected GitInitializer initializer;

        [UniGitInject]
		private void Construct(GitManager gitManager, 
	        GitCredentialsManager credentialsManager, 
	        GitExternalManager externalManager,
	        ILogger logger,
	        GitSettingsJson gitSettings,
	        GitInitializer initializer)
        {
	        this.logger = logger;
			this.gitManager = gitManager;
			this.credentialsManager = credentialsManager;
			this.externalManager = externalManager;
	        this.gitSettings = gitSettings;
	        this.initializer = initializer;

			remotes = gitManager.Repository.Network != null && gitManager.Repository.Network.Remotes != null ? gitManager.Repository.Network.Remotes.ToArray() : new Remote[0];
			remoteNames = remotes.Select(r => new GUIContent(r.Name)).ToArray();
			branchNames = gitManager.Repository.Branches.Select(b => b.CanonicalName).ToArray();
			branchFriendlyNames = gitManager.Repository.Branches.Select(b => b.FriendlyName).ToArray();
		}

		public void Init(Branch branch)
		{
			if(branch == null) return;

			if(branchNames == null)
				branchNames = gitManager.Repository.Branches.Select(b => b.CanonicalName).ToArray();
			if(branchFriendlyNames == null)
				branchFriendlyNames = gitManager.Repository.Branches.Select(b => b.FriendlyName).ToArray();

			if (remotes != null && branch.Remote != null)
				selectedRemote = Array.IndexOf(remotes, branch.Remote);
			if (branchNames != null && !string.IsNullOrEmpty(branch.CanonicalName))
				selectedBranch = Array.IndexOf(branchNames, branch.CanonicalName);
		}

		protected void DrawRemoteSelection()
		{
			selectedRemote = EditorGUILayout.Popup(GitGUI.GetTempContent("Remote"), selectedRemote, remoteNames);
		}

		protected void DrawBranchSelection()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(GitGUI.GetTempContent("Branch"));
			selectedBranch = EditorGUILayout.Popup(selectedBranch, branchFriendlyNames);
			EditorGUILayout.EndHorizontal();
		}

		protected void DrawCredentials()
		{
			credentials.Active = EditorGUILayout.Toggle(GitGUI.GetTempContent("Custom Credentials","Credentials to use instead of the ones from the credentials manager."), credentials.Active);
			if (credentials.Active)
			{
				EditorGUI.indentLevel = 1;
				credentials.IsToken = EditorGUILayout.Toggle(GitGUI.GetTempContent("Is Token"), credentials.IsToken);
				credentials.Username = EditorGUILayout.TextField(GitGUI.GetTempContent(credentials.IsToken ? "Token" : "Username", "If left empty, stored credentials in settings will be used."), credentials.Username);
				if(!credentials.IsToken)
					GitGUI.SecurePasswordFieldLayout(GitGUI.GetTempContent("Password", "If left empty, stored credentials in settings will be used."),credentials.Password);
				EditorGUI.indentLevel = 0;
			}
		}

		protected override bool DrawWizardGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawRemoteSelection();
			DrawBranchSelection();
			DrawCredentials();
			return EditorGUI.EndChangeCheck();
		}

		#region Handlers
		protected LibGit2Sharp.Credentials CredentialsHandler(string url, string user, SupportedCredentialTypes supported)
		{
			if(credentials.Active)
				return credentialsManager.DefaultCredentialsHandler(url,credentials.Username, credentials.Password,supported,credentials.IsToken);

			return credentialsManager.DefaultCredentialsHandler(url,user, null,supported,credentials.IsToken);
		}

		#region Fetch

		protected bool FetchProgress(string serverProgressOutput)
		{
			logger.LogFormat(LogType.Log,"Fetching: {0}", serverProgressOutput);
			return true;
		}

		protected bool FetchOperationStarting(RepositoryOperationContext context)
		{
			logger.Log(LogType.Log,"Fetch Operation Started");
			//true to continue
			return true;
		}

		protected void FetchOperationCompleted(RepositoryOperationContext context)
		{
			logger.Log(LogType.Log,"Operation Complete");
		}
		#endregion

		#region Merge

		protected void OnMergeComplete(MergeResult result,string mergeType)
		{
		    var historyWindow = UniGitLoader.FindWindow<GitHistoryWindow>();
		    var diffWindow = UniGitLoader.FindWindow<GitDiffWindow>();

            switch (result.Status)
			{
				case MergeStatus.UpToDate:
				    if(historyWindow != null) historyWindow.ShowNotification(new GUIContent(string.Format("Everything is Up to date. Nothing to {0}.", mergeType)));
					break;
				case MergeStatus.FastForward:
					if(historyWindow != null) historyWindow.ShowNotification(new GUIContent(mergeType + " Complete with Fast Forwarding."));
					break;
				case MergeStatus.NonFastForward:
				    if (diffWindow != null)
				    {
				        diffWindow.ShowNotification(new GUIContent("Do a merge commit in order to push changes."));
				        diffWindow.SetCommitMessage(gitManager.Repository.Info.Message);
                    }
				    else
				    {
				        GitDiffWindow.SetCommitMessage(initializer,gitManager, gitSettings,gitManager.Repository.Info.Message);
				    }
					logger.LogFormat(LogType.Log,"{0} Complete without Fast Forwarding.",mergeType);
					break;
				case MergeStatus.Conflicts:
					GUIContent content = GitGUI.IconContent("console.warnicon", "There are merge conflicts!");
				    if (diffWindow != null)
				    {
				        diffWindow.ShowNotification(content);
				        diffWindow.SetCommitMessage(gitManager.Repository.Info.Message);
                    }
				    else
				    {
				        GitDiffWindow.SetCommitMessage(initializer,gitManager, gitSettings,gitManager.Repository.Info.Message);
				    }
					break;
			}
			gitManager.MarkDirty();
			logger.LogFormat(LogType.Log,"{0} Status: {1}", mergeType, result.Status);
		}

		#endregion
		#endregion

		[Serializable]
		protected class Credentials
		{
			private SecureString password;
			[SerializeField]
			private bool active;
			[SerializeField]
			private string username;
			[SerializeField] private bool isToken;

			public Credentials()
			{
				password = new SecureString();
			}

			~Credentials()
			{
				password.Dispose();
			}

			public SecureString Password
			{
				get { return password; }
				set { password = value; }
			}

			public string Username
			{
				get { return username; }
				set { username = value; }
			}

			public bool IsToken
			{
				get { return isToken; }
				set { isToken = value; }
			}

			public bool Active
			{
				get { return active; }
				set { active = value; }
			}
		}

		[Serializable]
		protected enum ConflictMergeType
		{
			Normal = 0,
			Ours = 1,
			Theirs = 2
		}
	}
}