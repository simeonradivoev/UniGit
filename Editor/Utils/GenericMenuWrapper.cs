using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public class GenericMenuWrapper : IGenericMenu
	{
        public GenericMenu GenericMenu { get; }

        public GenericMenuWrapper(GenericMenu genericMenu)
		{
			this.GenericMenu = genericMenu;
		}

		public void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction func)
		{
			GenericMenu.AddItem(content,on,func);
		}

		public void AddDisabledItem(GUIContent content)
		{
			GenericMenu.AddDisabledItem(content);
		}

		public void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction2 func, object data)
		{
			GenericMenu.AddItem(content, on, func,data);
		}

		public void AddSeparator(string text)
		{
			GenericMenu.AddSeparator(text);
		}
    }
}