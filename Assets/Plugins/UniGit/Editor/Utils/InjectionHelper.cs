using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UniGit.Utils
{
	public class InjectionHelper
	{
		private readonly List<Resolve> resolves;

		public InjectionHelper()
		{
			resolves = new List<Resolve>();
		}

		public Resolve Bind(Type type)
		{
			var resolve = new Resolve(this,type);
			resolves.Add(resolve);
			return resolve;
		}

		public Resolve<T> Bind<T>()
		{
			var resolve = new Resolve<T>(this);
			resolves.Add(resolve);
			return resolve;
		}

		public T CreateInstance<T>()
		{
			return (T)CreateInstance(typeof(T));
		}

		public object CreateInstance(Type type)
		{
			var constructors = type.GetConstructors();
			foreach (var constructor in constructors)
			{
				var customAttributes = constructor.GetCustomAttributes(typeof(UniGitInject), true);
				if (customAttributes.Length > 0)
				{
					var parameterInfos = constructor.GetParameters();
					object[] args = new object[parameterInfos.Length];
					for (int i = 0; i < parameterInfos.Length; i++)
					{
						Resolve resolve;
						if (FindResolve(parameterInfos[i], type, out resolve))
						{
							args[i] = resolve.GetInstance();
						}
						else
						{
							throw new Exception(string.Format("Unresolved parameter: {0} with type: {1}", parameterInfos[i].Name, parameterInfos[i].ParameterType));
						}
					}
					object instance = constructor.Invoke(args);
					return instance;
				}
			}
			return Activator.CreateInstance(type);
		}

		private bool FindResolve(ParameterInfo parameter, Type injecteeType,out Resolve resolveOut)
		{
			foreach (var resolve in resolves)
			{
				if (ValidResolve(resolve, parameter, injecteeType))
				{
					resolveOut = resolve;
					return true;
				}
			}
			resolveOut = null;
			return false;
		}

		private bool ValidResolve(Resolve resolve, ParameterInfo parameter, Type injecteeType)
		{
			if (resolve.Type != parameter.ParameterType) return false;
			if (resolve.WhenInjectedIntoType != null && resolve.WhenInjectedIntoType != injecteeType) return false;
			if (!string.IsNullOrEmpty(resolve.id) && parameter.Name != resolve.id) return false;
			return true;
		}

		public T GetInstance<T>()
		{
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
					return (T)resolve.GetInstance();
			}
			return default(T);
		}

		public List<T> GetInstances<T>()
		{
			List<T> instances = new List<T>();
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
				{
					instances.Add((T)resolve.GetInstance());
				}
			}
			return instances;
		}

		public class Resolve<T> : Resolve
		{
			public Resolve(InjectionHelper injectionHelper) : base(injectionHelper,typeof(T))
			{
				
			}

			public new T GetInstance()
			{
				return (T)base.GetInstance();
			}

			public Resolve<T> FromMethod(Func<T> method)
			{
				base.FromMethod(()=> method.Invoke());
				return this;
			}

			public new Resolve<T> To(Type type)
			{
				base.To(type);
				return this;
			}

			public new Resolve<T> To<TK>() where TK : T
			{
				base.To(typeof(TK));
				return this;
			}

			public new Resolve<T> WhenInjectedInto(Type type)
			{
				base.WhenInjectedInto(type);
				return this;
			}

			public new Resolve<T> WithId(string id)
			{
				base.WithId(id);
				return this;
			}
		}

		public class Resolve : IEquatable<Resolve>
		{
			private Type type;
			private Type instanceType;
			private Type whenInjectedInto;
			private Func<object> method;
			internal string id;
			private object instance;
			private readonly InjectionHelper injectionHelper;

			public Resolve(InjectionHelper injectionHelper,Type type)
			{
				this.injectionHelper = injectionHelper;
				this.type = type;
				instanceType = type;
			}

			public bool Equals(Resolve other)
			{
				if (other == null) return false;
				if (type != other.type || whenInjectedInto != other.whenInjectedInto || other.id != id) return false;
				return false;
			}

			public Resolve To(Type type)
			{
				if (this.type.IsAssignableFrom(type) || type == this.type)
				{
					instanceType = type;
				}
				else
				{
					throw new Exception(string.Format("Type '{0}' must be able to be assigned from type '{1}'", type, this.type));
				}
				return this;
			}

			public Resolve To<T>()
			{
				return To(typeof(T));
			}

			public Resolve WhenInjectedInto(Type type)
			{
				whenInjectedInto = type;
				return this;
			}

			public Resolve WhenInjectedInto<T>()
			{
				return WhenInjectedInto(typeof(T));
			}

			public Resolve FromMethod(Func<object> method)
			{
				this.method = method;
				return this;
			}

			public Resolve FromInstance(object instance)
			{
				this.instance = instance;
				if (!type.IsInstanceOfType(instance))
				{
					throw new Exception(string.Format("Instance of type: {0} does not match resolve type: {1}",instance.GetType(),type));
				}
				return this;
			}

			public Resolve WithId(string id)
			{
				this.id = id;
				return this;
			}

			public object GetInstance()
			{
				if (instance == null)
				{
					if (method != null)
						instance = method.Invoke();
					else
						instance = injectionHelper.CreateInstance(instanceType);
				}
				return instance;
			}

			public Type Type
			{
				get { return type; }
			}

			public Type InstanceType
			{
				get { return instanceType; }
			}

			public Type WhenInjectedIntoType
			{
				get { return whenInjectedInto; }
			}

			public string Id
			{
				get { return id; }
			}
		}
	}
}