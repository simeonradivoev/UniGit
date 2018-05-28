using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using SimpleJSON;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UniGit
{
	public class GitRemoteToolsWindow : EditorWindow
	{
		public class Styles
		{
			public GUIStyle IssueElementBg;
			public GUIStyle IssueBodyText;
			public GUIStyle IssueTitle;
			public GUIStyle IssueTitleBig;
			public GUIStyle IssueTitleBigBg;
			public GUIStyle IssueSubTitle;
			public GUIStyle IssueLabel;
			public GUIStyle Avatar;
		}

		[SerializeField] private string issuesSerialized;
		[SerializeField] private string currentCommentsSerialzied;
		[SerializeField] private int currentIssue = -1;
		private List<Element> elements = new List<Element>();
		private GitCallbacks callbacks;
		private Vector2 issuesScroll;
		private Vector2 currentIssueScroll;
		private static Styles styles;
		private GitSettingsJson settings;
		private int callsLeft = 0;

		[UniGitInject]
		private void Construct(GitSettingsJson settings)
		{
			this.settings = settings;
			Refresh();
		}

		private void InitStyles()
		{
			if (styles == null)
			{
				styles = new Styles()
				{
					IssueElementBg = new GUIStyle("IN GameObjectHeader")
					{

					},
					IssueBodyText = new GUIStyle(EditorStyles.textArea)
					{
						wordWrap = true
					},
					IssueTitleBig = new GUIStyle("TL Selection H1")
					{
						alignment = TextAnchor.MiddleLeft
					},
					IssueTitleBigBg = new GUIStyle("IN GameObjectHeader")
					{
						margin = new RectOffset(0,0,8,8),
						padding = new RectOffset(16,16,21,16)
					},
					IssueTitle = new GUIStyle(EditorStyles.largeLabel)
					{
						fontStyle = FontStyle.Bold,
						fontSize = 14,
						alignment = TextAnchor.UpperLeft
					},
					IssueLabel = new GUIStyle("sv_iconselector_selection")
					{
						fixedHeight = EditorGUIUtility.singleLineHeight,
						margin = new RectOffset(4,4,7,7),
						padding = new RectOffset(4,4,1,1),
						alignment = TextAnchor.UpperCenter
					},
					IssueSubTitle = new GUIStyle(EditorStyles.miniLabel),
					Avatar = new GUIStyle("ShurikenEffectBg")
					{
						contentOffset = Vector3.zero, 
						alignment = TextAnchor.MiddleCenter, 
						clipping = TextClipping.Clip, 
						imagePosition = ImagePosition.ImageOnly,
					}
				};
			}
		}

		[UsedImplicitly]
		private void OnEnable()
		{
			wantsMouseMove = true;
			GitWindows.AddWindow(this);
			if (!string.IsNullOrEmpty(issuesSerialized))
			{
				DeserialzieIssues(JSON.Parse(issuesSerialized));
				if (currentIssue >= 0)
				{
					var issue = GetIssue(currentIssue);
					if (issue != null)
					{
						if (string.IsNullOrEmpty(currentCommentsSerialzied))
						{
							LoadComments(issue);
						}
						else
						{
							issue.comments = LoadElements<Comment>(JSON.Parse(currentCommentsSerialzied));
						}
					}
				}
				Repaint();
			}
		}

		private void Refresh()
		{
			var request = UnityWebRequest.Get("https://api.github.com/repos/simeonradivoev/UniGit/issues");
			request.SetRequestHeader("Authorization","token " + settings.RemoteToken);
			request.SendWebRequest().completed += operation =>
			{
				issuesSerialized = request.downloadHandler.text;
				DeserialzieIssues(JSON.Parse(issuesSerialized));
				int.TryParse(request.GetResponseHeaders()["X-RateLimit-Remaining"],out callsLeft);
				Repaint();
			};
		}

		private void OnGUI()
		{
			InitStyles();
			var currentIssueObj = GetIssue(currentIssue);

			var current = Event.current;
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button(new GUIContent("Refresh"),EditorStyles.toolbarButton))
			{
				Refresh();
			}
			EditorGUILayout.Space();
			if (currentIssueObj != null)
			{
				if (GUILayout.Button(new GUIContent("Issues"),"GUIEditor.BreadcrumbLeft"))
				{
					currentIssue = -1;
				}
				GUILayout.Toggle(true,new GUIContent(currentIssueObj.title,"GUIEditor.BreadcrumbMid"));
			}
			else
			{
				GUILayout.Toggle(true,new GUIContent("Issues"),"GUIEditor.BreadcrumbLeft");
			}

			GUILayout.FlexibleSpace();
			EditorGUI.BeginChangeCheck();
			GUILayout.Label(GitGUI.GetTempContent(string.Format("calls left: {0}",callsLeft)));
			EditorGUILayout.Space();
			GUILayout.Label(GitGUI.GetTempContent("Token"));
			settings.RemoteToken = EditorGUILayout.TextField(settings.RemoteToken,EditorStyles.toolbarTextField);
			if (EditorGUI.EndChangeCheck())
			{
				settings.MarkDirty();
			}
			EditorGUILayout.EndHorizontal();
			if (currentIssueObj != null)
			{
				currentIssueScroll = EditorGUILayout.BeginScrollView(currentIssueScroll);

				EditorGUILayout.BeginHorizontal(styles.IssueTitleBigBg);

				GUILayout.Label(new GUIContent(currentIssueObj.user.avatar_texture),styles.Avatar,GUILayout.MaxWidth(64),GUILayout.MaxHeight(64));

				EditorGUILayout.BeginVertical();
				EditorGUILayout.LabelField(new GUIContent(string.Format("{0} #{1}", currentIssueObj.title, currentIssueObj.number)), styles.IssueTitleBig);
				EditorGUILayout.LabelField(new GUIContent(string.Format("{0} opened this issue on {1} · {2} comments", currentIssueObj.user, currentIssueObj.created_at, currentIssueObj.commentCount)));
				EditorGUILayout.EndVertical();

				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal(); //body
				EditorGUILayout.LabelField(currentIssueObj.body, styles.IssueBodyText, GUILayout.ExpandWidth(true));

				EditorGUILayout.BeginVertical(GUILayout.MaxWidth(200)); //labels

				GUILayout.Label(new GUIContent("Assignees"), "ProjectBrowserHeaderBgTop");

				foreach (var assignee in currentIssueObj.assignees)
				{
					GUILayout.Label(new GUIContent(assignee.login), "AssetLabel");
				}

				EditorGUILayout.Space();

				GUILayout.Label(new GUIContent("Labels"), "ProjectBrowserHeaderBgTop");

				foreach (var label in currentIssueObj.labels)
				{
					GUI.backgroundColor = label.color;
					GUILayout.Label(new GUIContent(label.name), styles.IssueLabel);
					GUI.backgroundColor = Color.white;
				}

				EditorGUILayout.Space();

				GUILayout.Label(new GUIContent("Milestone"), "ProjectBrowserHeaderBgTop");

				string milestone = currentIssueObj.milestone;
				if (!string.IsNullOrEmpty(milestone))
				{
					GUILayout.Label(new GUIContent(milestone), "AssetLabel");
				}

				EditorGUILayout.Space();

				EditorGUILayout.EndVertical(); //end labels
				EditorGUILayout.EndHorizontal(); //end body

				EditorGUILayout.Space();

				EditorGUILayout.LabelField(new GUIContent("Comments"),"IN BigTitle",GUILayout.Height(24));

				EditorGUILayout.Space();

				foreach (var comment in currentIssueObj.comments)
				{
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(new GUIContent(comment.user.avatar_texture),styles.Avatar,GUILayout.MaxWidth(32),GUILayout.MaxHeight(32));
					EditorGUILayout.LabelField(comment.body, styles.IssueBodyText, GUILayout.ExpandWidth(true),GUILayout.MinHeight(64));
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
				}

				EditorGUILayout.EndScrollView();
			}
			else
			{
				float issuesWidth = position.width;
				float issueElementHeight = EditorGUIUtility.singleLineHeight * 3.5f;
				float totalIssuesHeight = elements.OfType<Issue>().Count() * issueElementHeight;
				issuesScroll = GUI.BeginScrollView(new Rect(0, EditorGUIUtility.singleLineHeight, issuesWidth, position.height - EditorGUIUtility.singleLineHeight), issuesScroll, new Rect(0, 0, issuesWidth, totalIssuesHeight));
				Rect lastRect = new Rect();
				foreach (var issue in elements.OfType<Issue>())
				{
					RectOffset elementPadding = styles.IssueElementBg.padding;
					Rect elementRect = new Rect(0, lastRect.y + lastRect.height, position.width, issueElementHeight);
					Rect paddedRect = new Rect(elementRect.x + elementPadding.left, elementRect.y + elementPadding.top, elementRect.width - elementPadding.horizontal, elementRect.height - elementPadding.vertical);
					GUI.Box(elementRect, GUIContent.none, styles.IssueElementBg);
					EditorGUIUtility.AddCursorRect(elementRect, MouseCursor.Link);
					bool isOver = elementRect.Contains(current.mousePosition);
					if (isOver)
					{
						GUI.Box(new Rect(elementRect.x, elementRect.y, 6, elementRect.height), GUIContent.none, "LODSliderRangeSelected");
						paddedRect.x += 6;
						paddedRect.width -= 6;
					}

					GUILayout.BeginArea(paddedRect);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"));
					EditorGUILayout.BeginVertical();
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(issue.title, styles.IssueTitle);
					foreach (var label in issue.labels)
					{
						GUI.backgroundColor = label.color;
						GUILayout.Label(new GUIContent(label.name), styles.IssueLabel);
						GUI.backgroundColor = Color.white;
					}

					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
					GUILayout.Label(new GUIContent(string.Format("#{0} opened on {1} by {2}", issue.number, issue.created_at, issue.user.login)), styles.IssueSubTitle);
					EditorGUILayout.EndVertical();
					EditorGUILayout.EndHorizontal();
					GUILayout.EndArea();

					if (isOver && current.type == EventType.MouseUp)
					{
						OpenIssue(issue);
						Repaint();
					}

					lastRect = elementRect;
				}

				GUI.EndScrollView();
			}

			if (current.type == EventType.MouseMove)
			{
				Repaint();
			}
		}

		public bool MyRemoteCertificateValidationCallback(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) 
		{
			bool isOk = true;
			// If there are errors in the certificate chain, look at each error to determine the cause.
			if (sslPolicyErrors != SslPolicyErrors.None) {
				for (int i=0; i<chain.ChainStatus.Length; i++) {
					if (chain.ChainStatus [i].Status != X509ChainStatusFlags.RevocationStatusUnknown) {
						chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
						chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
						chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
						chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
						bool chainIsValid = chain.Build ((X509Certificate2)certificate);
						if (!chainIsValid) {
							isOk = false;
						}
					}
				}
			}
			return isOk;
		}

		private void OpenIssue(Issue issue)
		{
			currentIssue = issue.number;
			if (issue.comments == null)
			{
				LoadComments(issue);
			}
		}

		private void LoadComments(Issue issue)
		{
			issue.comments = new Comment[0];
			if (issue.commentCount > 0)
			{
				var commentsRequest = UnityWebRequest.Get(string.Format("https://api.github.com/repos/simeonradivoev/UniGit/issues/{0}/comments",issue.number));
				commentsRequest.SetRequestHeader("Authorization","token " + settings.RemoteToken);
				commentsRequest.SendWebRequest().completed += operation =>
				{
					currentCommentsSerialzied = commentsRequest.downloadHandler.text;
					issue.comments = LoadElements<Comment>(JSON.Parse(currentCommentsSerialzied));
					int.TryParse(commentsRequest.GetResponseHeaders()["X-RateLimit-Remaining"], out callsLeft);
				};
			}
		}

		private Element GetElement(int id,Type elementType)
		{
			return elements.FirstOrDefault(e => e.id == id && elementType.IsInstanceOfType(e));
		}

		private T GetElement<T>(int id) where T : Element
		{
			return GetElement(id, typeof(T)) as T;
		}

		public class Element
		{
			public readonly int id;

			public Element(int id)
			{
				this.id = id;
			}

			public virtual void OnDeserialzied()
			{
				
			}

			public override bool Equals(object obj)
			{
				Element other = obj as Element;
				if (other == null) return false;
				return id == other.id;
			}

			public override int GetHashCode()
			{
				return id;
			}
		}

		private void DeserialzieIssues(JSONNode obj)
		{
			elements.Clear();
			foreach (var child in obj.Children)
			{
				Issue issue = new Issue(child["id"]);
				issue.number = child["number"];
				issue.title = child["title"];
				issue.body = child["body"];
				issue.commentCount = child["comments"];
				issue.user = LoadElement<User>(child["user"]);
				issue.assignees = LoadElements<User>(child["assignees"]);
				issue.created_at = DateTime.Parse(child["created_at"]);
				issue.labels = LoadElements<Label>(child["labels"]);
				issue.milestone = child["milestone"];
				elements.Add(issue);
			}
		}

		public Element LoadElement(JSONNode node,Type elementType)
		{
			var existingElement = GetElement(node["id"],elementType);
			if (existingElement == null)
			{
				existingElement = (Element)Activator.CreateInstance(elementType,node["id"].AsInt);
				foreach (var keyValue in node)
				{
					var field = elementType.GetField(keyValue.Key);
					if (field != null)
					{
						string stringValue = keyValue.Value.Value;
						object parsedValue;
						if (typeof(Element).IsAssignableFrom(field.FieldType))
						{
							parsedValue = LoadElement(keyValue.Value, field.FieldType);
						}
						else if (field.FieldType == typeof(Color))
						{
							Color outColor;
							if (ColorUtility.TryParseHtmlString("#" + stringValue, out outColor))
							{
								parsedValue = outColor;
							}
							else
							{
								throw new Exception("Could not parse color");
							}
						}
						else
						{
							parsedValue = Convert.ChangeType(stringValue, field.FieldType);
						}

						if (parsedValue != null)
						{
							field.SetValue(existingElement,parsedValue);
						}
					}
				}
				elements.Add(existingElement);
				existingElement.OnDeserialzied();
			}
			return existingElement;
		}

		public Issue GetIssue(int number)
		{
			return elements.OfType<Issue>().FirstOrDefault(i => i.number == number);
		}

		public T LoadElement<T>(JSONNode node) where T : Element
		{
			return (T)LoadElement(node, typeof(T));
		}

		public T[] LoadElements<T>(JSONNode node) where T : Element
		{
			T[] elements = new T[node.Count];
			for (int i = 0; i < node.Count; i++)
			{
				elements[i] = LoadElement<T>(node[i]);
			}
			return elements;
		}

		public class Issue : Element
		{
			public int number;
			public string title;
			public string body;
			public int commentCount;
			public Comment[] comments;
			public User user;
			public User[] assignees;
			public DateTime created_at;
			public Label[] labels;
			public string milestone;

			public Issue(int id) : base(id)
			{

			}
		}

		public class Comment : Element
		{
			public string body;
			public DateTime created_at;
			public User user;

			public Comment(int id) : base(id)
			{
			}
		}

		public class User : Element
		{
			public string login;
			public string avatar_url;
			public Texture2D avatar_texture;

			public User(int id) : base(id)
			{
			}

			public override void OnDeserialzied()
			{
				var operation = UnityWebRequestTexture.GetTexture(avatar_url).SendWebRequest();
				operation.completed += (o) =>
				{
					avatar_texture = new Texture2D(32, 32)
					{
						filterMode = FilterMode.Bilinear
					};
					avatar_texture.LoadImage(((DownloadHandlerTexture) operation.webRequest.downloadHandler).data);
					avatar_texture.Apply(true);
				};
			}
		}

		public class Label : Element
		{
			public Color color;
			public string url;
			public string name;

			public Label(int id) : base(id)
			{
			}
		}
	}
}
