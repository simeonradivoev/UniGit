using System;
using System.Linq;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

namespace UniGit
{
	public class GitWizardBase : ScriptableWizard
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
		private SerializedObject serializedObject;
		protected GitManager gitManager;
		protected GitCredentialsManager credentialsManager;
		protected GitExternalManager externalManager;

        [UniGitInject]
		private void Construct(GitManager gitManager, GitCredentialsManager credentialsManager, GitExternalManager externalManager)
		{
			this.gitManager = gitManager;
			this.credentialsManager = credentialsManager;
			this.externalManager = externalManager;

			remotes = gitManager.Repository.Network != null && gitManager.Repository.Network.Remotes != null ? gitManager.Repository.Network.Remotes.ToArray() : new Remote[0];
			remoteNames = remotes.Select(r => new GUIContent(r.Name)).ToArray();
			branchNames = gitManager.Repository.Branches.Select(b => b.CanonicalName).ToArray();
			branchFriendlyNames = gitManager.Repository.Branches.Select(b => b.FriendlyName).ToArray();
		}

		protected virtual void OnEnable()
		{
			GitWindows.AddWindow(this);
			serializedObject = new SerializedObject(this);
			Repaint();
		}

		public void Init(Branch branch)
		{
			if(branch == null) return;

			if(branchNames == null)
				branchNames = gitManager.Repository.Branches.Select(b => b.CanonicalName).ToArray();
			if(branchFriendlyNames == null)
				branchFriendlyNames = gitManager.Repository.Branches.Select(b => b.FriendlyName).ToArray();

			if (remotes != null && branch.Remote.Name != null)
				selectedRemote = Array.IndexOf(remotes, branch.Remote.Name);
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
			credentalsExpanded = EditorGUILayout.PropertyField(serializedObject.FindProperty("credentials"));
			if (credentalsExpanded)
			{
				EditorGUI.indentLevel = 1;
				credentials.IsToken = EditorGUILayout.Toggle(GitGUI.GetTempContent("Is Token"), credentials.IsToken);
				if (credentials.IsToken)
				{
					credentials.Token = EditorGUILayout.TextField(GitGUI.GetTempContent("Token", "If left empty, stored credentials in settings will be used."), credentials.Token);
				}
				else
				{
					credentials.Username = EditorGUILayout.TextField(GitGUI.GetTempContent("Username", "If left empty, stored credentials in settings will be used."), credentials.Username);
					credentials.Password = EditorGUILayout.PasswordField(GitGUI.GetTempContent("Password", "If left empty, stored credentials in settings will be used."), credentials.Password);
				}
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

		protected void OnDisable()
		{
			GitWindows.RemoveWindow(this);
		}

		#region Handlers
		protected LibGit2Sharp.Credentials CredentialsHandler(string url, string user, SupportedCredentialTypes supported)
		{
			if (supported == SupportedCredentialTypes.UsernamePassword)
			{
				string username = credentials.Username;
				string password = credentials.Password;

				if (credentials.IsToken)
				{
					username = credentials.Token;
					password = string.Empty;
				}

				if (credentialsManager.GitCredentials != null)
				{
					if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
					{
						credentialsManager.LoadCredentials(url,ref username,ref password,true);
					}
				}

				return new UsernamePasswordCredentials()
				{
					Username = username,
					Password = password
				};
			}
			return new DefaultCredentials();
		}

		#region Fetch

		protected bool FetchProgress(string serverProgressOutput)
		{
			Debug.Log(string.Format("Fetching: {0}", serverProgressOutput));
			return true;
		}

		protected bool FetchOperationStarting(RepositoryOperationContext context)
		{
			Debug.Log("Fetch Operation Started");
			//true to continue
			return true;
		}

		protected void FetchOperationCompleted(RepositoryOperationContext context)
		{
			Debug.Log("Operation Complete");
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
				        GitDiffWindow.SetCommitMessage(gitManager, gitManager.Repository.Info.Message);
				    }
					Debug.Log(mergeType + " Complete without Fast Forwarding.");
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
				        GitDiffWindow.SetCommitMessage(gitManager, gitManager.Repository.Info.Message);
				    }
					break;
			}
			gitManager.MarkDirty();
			Debug.LogFormat("{0} Status: {1}", mergeType, result.Status);
		}

		protected bool OnCheckoutNotify(string path, CheckoutNotifyFlags notifyFlags)
		{
			Debug.Log(path + " (" + notifyFlags + ")");
			return true;
		}

		protected void OnCheckoutProgress(string path, int completedSteps, int totalSteps)
		{
			float percent = (float)completedSteps / totalSteps;
			EditorUtility.DisplayProgressBar("Checkout", string.Format("Checking {0} steps out of {1}.", completedSteps, totalSteps), percent);
		}
		#endregion
		#endregion

		[Serializable]
		protected struct Credentials
		{
			private string password;
			[SerializeField]
			private string username;
			[SerializeField] private bool isToken;
			[SerializeField] private string token;

			public string Password
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

			public string Token
			{
				get { return token; }
				set { token = value; }
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