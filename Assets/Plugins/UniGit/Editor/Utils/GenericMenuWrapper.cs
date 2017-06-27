using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GenericMenuWrapper : IGenericMenu
	{
		private GenericMenu genericMenu;

		public GenericMenuWrapper(GenericMenu genericMenu)
		{
			this.genericMenu = genericMenu;
		}

		public void AddItem(GUIContent content, bool @on, GenericMenu.MenuFunction func)
		{
			genericMenu.AddItem(content,on,func);
		}

		public void AddDisabledItem(GUIContent content)
		{
			genericMenu.AddDisabledItem(content);
		}

		public void AddItem(GUIContent content, bool @on, GenericMenu.MenuFunction2 func, object data)
		{
			genericMenu.AddItem(content, on, func,data);
		}

		public void AddSeparator(string text)
		{
			genericMenu.AddSeparator(text);
		}

		public GenericMenu GenericMenu
		{
			get { return genericMenu; }
		}
	}
}