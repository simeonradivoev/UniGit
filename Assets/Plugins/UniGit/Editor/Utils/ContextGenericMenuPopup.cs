using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class ContextGenericMenuPopup : PopupWindowContent, IGenericMenu
	{
		private const float AnimationTime = 0.4f;
		private const float HoverPressTime = 0.5f;
		private class Element
		{
			public Element parent;
			public GUIContent Content;
			public bool on;
			public GenericMenu.MenuFunction Func;
			public GenericMenu.MenuFunction2 Func2;
			public object data;
			public bool isSeparator;
			public bool isDisabled;
			public List<Element> children;

			public bool IsParent { get { return children != null && children.Count > 0; } }
		}
		private readonly GitOverlay gitOverlay;
		private readonly List<Element> elements;
		private GUIStyle elementStyle;
		private GUIStyle separatorStyle;
		private Element currentElement;
		private Element lastElement;
		private bool animatingToCurrentElement;
		private double lastAnimationTime;
		private int animDir = 1;
		private bool isClickHovering;
		private int lastHoverControlId;
		private double lastHoverStartTime;

		[UniGitInject]
		public ContextGenericMenuPopup(GitOverlay gitOverlay)
		{
			this.gitOverlay = gitOverlay;
			elements = new List<Element>();
		}

		private Element FindOrBuildTree(GUIContent content,ref GUIContent newContent)
		{
			if (string.IsNullOrEmpty(content.text)) return null;
			string[] elementStrings = content.text.Split(new [] {'/'},StringSplitOptions.RemoveEmptyEntries);

			if (elementStrings.Length > 1)
			{
				Element lastParent = elements.FirstOrDefault(e => e.Content.text == elementStrings[0]);

				if (lastParent == null)
				{
					lastParent = new Element() {children = new List<Element>(),Content = new GUIContent(elementStrings[0])};
					elements.Add(lastParent);
				}
				else if(lastParent.children == null)
				{
					return null;
				}

				for (int i = 1; i < elementStrings.Length-1; i++)
				{
					var newLastParent = lastParent.children.FirstOrDefault(e => e.Content.text == elementStrings[i]);
					if (newLastParent == null)
					{
						newLastParent = new Element() {children = new List<Element>(), Content = new GUIContent(elementStrings[i])};
						lastParent.children.Add(newLastParent);
						newLastParent.parent = lastParent;
					}
					else if (newLastParent.children == null)
						return null;

					lastParent = newLastParent;
				}

				newContent = new GUIContent(elementStrings[elementStrings.Length-1],content.image,content.tooltip);
				return lastParent;
			}
			return null;
		}

		public void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction func)
		{
			Element parent = FindOrBuildTree(content, ref content);
			AddElement(new Element { Content = content, on = on, Func = func },parent);
		}

		public void AddDisabledItem(GUIContent content)
		{
			Element parent = FindOrBuildTree(content, ref content);
			AddElement(new Element() { isDisabled = true, Content = content },parent);
		}

		public void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction2 func,object data)
		{
			Element parent = FindOrBuildTree(content, ref content);
			AddElement(new Element { Content = content, on = on, Func2 = func, data = data },parent);
		}

		public void AddSeparator(string text)
		{
			GUIContent content = new GUIContent(text);
			Element parent = FindOrBuildTree(content, ref content);
			AddElement(new Element() {isSeparator = true, Content = content}, parent);
		}

		private void AddElement(Element element, Element parent)
		{
			if (parent == null)
				elements.Add(element);
			else
			{
				parent.children.Add(element);
				element.parent = parent;
			}
		}

		public override Vector2 GetWindowSize()
		{
			if (elementStyle == null) InitStyles();
			Vector2 maxSize = Vector2.zero;
			if (animatingToCurrentElement)
			{
				if(lastElement != null && lastElement.children != null) CalculateMaxSize(ref maxSize, lastElement.children,true);
			}
			else
			{
				if(currentElement != null && currentElement.children != null) CalculateMaxSize(ref maxSize, currentElement.children,true);
			}

			CalculateMaxSize(ref maxSize, elements,false);

			return maxSize;
		}

		private void CalculateMaxSize(ref Vector2 maxSize,List<Element> elements,bool includeBackButton)
		{
			float maxHeight = includeBackButton ? elementStyle.CalcSize(GitGUI.GetTempContent("Back")).y : 0;
			foreach (var element in elements)
			{
				Vector2 size = Vector2.zero;
				if (element.isSeparator)
				{
					size = separatorStyle.CalcSize(element.Content);
				}
				else
				{
					size = elementStyle.CalcSize(element.Content);
				}
				maxSize.x = Mathf.Max(maxSize.x, size.x);
				maxHeight += size.y;
			}

			maxSize.y = Mathf.Max(maxSize.y, maxHeight);
		}

		private void InitStyles()
		{
			elementStyle = new GUIStyle("CN Message") {alignment = TextAnchor.MiddleLeft,padding = new RectOffset(32,32, 4,4),fontSize = 12,fixedHeight = 26};
			separatorStyle = new GUIStyle("sv_iconselector_sep") {fixedHeight = 6,margin = new RectOffset(32,32,4,4)};
		}

		private void DrawElementList(Rect rect,List<Element> elements,bool drawBack)
		{
			float height = 0;

			if (drawBack)
			{
				Rect backRect = new Rect(rect.x, rect.y + height, rect.width, elementStyle.fixedHeight);
				int backControlId = GUIUtility.GetControlID(GitGUI.GetTempContent("Back"), FocusType.Passive, backRect);
				if (Event.current.type == EventType.Repaint)
				{
					EditorGUIUtility.AddCursorRect(backRect, MouseCursor.Link);
					elementStyle.Draw(backRect, GitGUI.GetTempContent("Back"), false, false, backRect.Contains(Event.current.mousePosition), false);
					((GUIStyle)"AC LeftArrow").Draw(new Rect(backRect.x,backRect.y + ((backRect.height - 16) / 2f),16,16),GUIContent.none, false,false,false,false);

					if (backRect.Contains(Event.current.mousePosition) && !animatingToCurrentElement)
					{
						if (lastHoverControlId != backControlId)
						{
							lastHoverControlId = backControlId;
							lastHoverStartTime = EditorApplication.timeSinceStartup;
						}
						isClickHovering = true;
						DrawHoverClickIndicator();
					}
				}
				else if (((Event.current.type == EventType.MouseDown && Event.current.button == 0 && backRect.Contains(Event.current.mousePosition)) || (lastHoverControlId == backControlId && EditorApplication.timeSinceStartup > lastHoverStartTime + HoverPressTime)) && !animatingToCurrentElement && currentElement != null)
				{
					lastElement = currentElement;
					currentElement = currentElement.parent;
					animatingToCurrentElement = true;
					lastAnimationTime = EditorApplication.timeSinceStartup + AnimationTime;
					lastHoverStartTime = EditorApplication.timeSinceStartup;
					animDir = -1;
				}
				height = elementStyle.fixedHeight;
			}

			for (int i = 0; i < elements.Count; i++)
			{
				var element = elements[i];

				Rect elementRect = new Rect(rect.x, rect.y + height, rect.width, elementStyle.fixedHeight);
				int controlId = GUIUtility.GetControlID(element.Content, FocusType.Passive, elementRect);

				if (element.isSeparator)
				{
					if (Event.current.type == EventType.Repaint)
					{
						GUI.backgroundColor = new Color(1, 1, 1, 0.2f);
						Rect separatorRect = new Rect(rect.x + separatorStyle.margin.left, rect.y + height, rect.width - separatorStyle.margin.left - separatorStyle.margin.right, separatorStyle.fixedHeight);
						separatorStyle.Draw(separatorRect, element.Content, false, false, false, false);
						GUI.backgroundColor = Color.white;
					}
					height += separatorStyle.fixedHeight;
				}
				else
				{
					if (Event.current.type == EventType.Repaint)
					{
						GUI.enabled = !element.isDisabled;
						if (GUI.enabled)
						{
							EditorGUIUtility.AddCursorRect(elementRect, MouseCursor.Link);
						}
						
						elementStyle.Draw(elementRect, element.Content, controlId, elementRect.Contains(Event.current.mousePosition));
						if(element.children != null)
							((GUIStyle)"AC RightArrow").Draw(new Rect(elementRect.x + elementRect.width - 21, elementRect.y + ((elementRect.height - 21) / 2f), 21, 21),GUIContent.none,false,false,false,false);

						if (elementRect.Contains(Event.current.mousePosition) && !animatingToCurrentElement)
						{
							if (element.IsParent)
							{
								if (lastHoverControlId != controlId)
								{
									lastHoverControlId = controlId;
									lastHoverStartTime = EditorApplication.timeSinceStartup;
								}
								isClickHovering = true;
								DrawHoverClickIndicator();
							}
							else
							{
								lastHoverControlId = controlId;
								lastHoverStartTime = EditorApplication.timeSinceStartup;
								isClickHovering = false;
							}
						}
					}
					else if (element.IsParent && ((lastHoverControlId == controlId && EditorApplication.timeSinceStartup > lastHoverStartTime + HoverPressTime) || (Event.current.type == EventType.MouseDown && elementRect.Contains(Event.current.mousePosition))))
					{
						lastElement = currentElement;
						currentElement = element;
						animatingToCurrentElement = true;
						lastAnimationTime = EditorApplication.timeSinceStartup + AnimationTime;
						lastHoverStartTime = EditorApplication.timeSinceStartup;
						animDir = 1;
						editorWindow.Repaint();
					}
					else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && elementRect.Contains(Event.current.mousePosition) && !animatingToCurrentElement)
					{
						editorWindow.Close();
						if (element.Func != null)
						{
							element.Func.Invoke();
						}
						else if (element.Func2 != null)
						{
							element.Func2.Invoke(element.data);
						}
					}
					height += elementStyle.fixedHeight;
				}
			}
		}

		public override void OnGUI(Rect rect)
		{
			if (elementStyle == null) InitStyles();
			if ((Event.current.type == EventType.MouseMove && rect.Contains(Event.current.mousePosition)) || animatingToCurrentElement || isClickHovering) editorWindow.Repaint();

			if (Event.current.type == EventType.Repaint)
			{
				isClickHovering = false;
			}

			if (animatingToCurrentElement)
			{
				float animationTime = ApplyEasing((float)(lastAnimationTime - EditorApplication.timeSinceStartup) / AnimationTime);

				Rect lastElementRect = new Rect(rect.x - (rect.width * (1 - animationTime) * animDir), rect.y,rect.width,rect.height);
				if (lastElement != null && lastElement.IsParent)
				{
					DrawElementList(lastElementRect,lastElement.children,true);
				}
				else
				{
					DrawElementList(lastElementRect, elements,false);
				}
				Rect currentElementRect = new Rect(rect.x + (rect.width * animationTime) * animDir, rect.y, rect.width, rect.height);
				if (currentElement != null && currentElement.IsParent)
				{
					DrawElementList(currentElementRect, currentElement.children, true);
				}
				else
				{
					DrawElementList(currentElementRect, elements, false);
				}

				if (animationTime <= 0)
				{
					animatingToCurrentElement = false;
				}
			}
			else
			{
				if (currentElement != null && currentElement.children != null)
				{
					DrawElementList(rect, currentElement.children,true);
				}
				else
				{
					DrawElementList(rect,elements,false);
				}
			}
		}

		private void DrawHoverClickIndicator()
		{
			if (Event.current.type == EventType.Repaint)
			{
				GUI.color = new Color(1,1,1,0.3f);
				var tex = gitOverlay.icons.loadingCircle.image;
				int index = Mathf.RoundToInt(Mathf.Lerp(0, 7, (float) (EditorApplication.timeSinceStartup - lastHoverStartTime) / HoverPressTime));
				GUI.DrawTextureWithTexCoords(new Rect(Event.current.mousePosition - new Vector2(12,5),new Vector2(34,34)), tex,new Rect((index % 4 / 4f), 0.5f - Mathf.FloorToInt(index / 4f) * 0.5f, 1/4f,0.5f));
				GUI.color = Color.white;
			}
		}

		private float ApplyEasing(float t)
		{
			return t < .5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
		}
	}
}