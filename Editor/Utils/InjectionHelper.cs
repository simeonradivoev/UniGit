using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UniGit.Utils
{
	public class InjectionHelper : IDisposable
	{
		private const BindingFlags InjectMethodStaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		private const BindingFlags InjectMethodInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private readonly List<Resolve> resolves;
	    private InjectionHelper parent;
		private readonly Resolve injectorResolve;
		private bool disposed;

		public InjectionHelper()
		{
			resolves = new List<Resolve>();
			injectorResolve = Bind<InjectionHelper>().FromInstance(this);
		}

		public void Clear()
		{
			resolves.Clear();
		}

		public Resolve Bind(Type type)
		{
			var resolve = new Resolve(type);
			resolves.Add(resolve);
			return resolve;
		}

		public Resolve<T> Bind<T>()
		{
			var resolve = new Resolve<T>();
			resolves.Add(resolve);
			return resolve;
		}

		public void Unbind<T>()
		{
			resolves.RemoveAll(r => r.Type == typeof(T) || r.InstanceType == typeof(T));
		}

	    public void SetParent(InjectionHelper parent)
	    {
	        this.parent = parent;
	    }

		public T CreateInstance<T>()
		{
			return (T)CreateInstance(typeof(T),null);
		}

		public T CreateInstance<T>(params object[] additionalArguments)
		{
			return (T)CreateInstance(typeof(T),additionalArguments);
		}

		public object CreateInstance(Type type,object[] additionalArguments)
		{
			if (type == typeof(ScriptableObject) || type.IsSubclassOf(typeof(ScriptableObject)))
			{
				var instance = ScriptableObject.CreateInstance(type);
				Inject(instance);
				return instance;
			}

			var constructor = GetInjectConstructor(type);
			if (constructor != null)
			{
				var parameterInfos = constructor.GetParameters();
				var args = new object[parameterInfos.Length];
				for (var i = 0; i < parameterInfos.Length; i++)
				{
					args[i] = HandleParameter(parameterInfos[i], type,additionalArguments);
				}
				var instance = constructor.Invoke(args);
				Inject(instance);
				return instance;
			}
			return Activator.CreateInstance(type);
		}

		public void Inject(object obj)
		{
			Inject(obj.GetType(),obj,InjectMethodInstanceFlags);
		}

		public void InjectStatic(Type type)
		{
			Inject(type,null,InjectMethodStaticFlags);
		}

		private void Inject(Type type,object obj,BindingFlags flags)
		{
			var infos = GetInjectMethods(type,flags);
			foreach (var methodInfo in infos)
			{
				var parameterInfos = methodInfo.GetParameters();
				var args = new object[parameterInfos.Length];
				for (var i = 0; i < parameterInfos.Length; i++)
				{
					args[i] = HandleParameter(parameterInfos[i], type,null);
				}
				try
				{
					methodInfo.Invoke(obj, args);
				}
				catch (Exception e)
				{
					Debug.LogErrorFormat("There was a problem while calling injectable method {0}",methodInfo.Name);
					Debug.LogException(e);
					return;
				}
			}
		}

		private List<MethodInfo> GetInjectMethods(Type type,BindingFlags bindingFlags)
		{
			var infos = new List<MethodInfo>();
			var lastType = type;
			while (lastType != null)
			{
				var methods = lastType.GetMethods(bindingFlags);
				foreach (var method in methods)
				{
					if (HasInjectAttribute(method))
					{
						if(method.IsAbstract) continue;
						//don't put overridden methods, only methods that are declared by the type, aka Virtual
						if (method.IsVirtual && method.GetBaseDefinition().DeclaringType != lastType) continue;
						infos.Add(method);
					}
				}
				lastType = lastType.BaseType;
			}
			infos.Reverse();
			return infos;
		}

		private static bool HasInjectAttribute(MethodInfo methodInfo)
		{
			return methodInfo.GetCustomAttributes(typeof(UniGitInject), true).Length > 0;
		}

		private ConstructorInfo GetInjectConstructor(Type type)
		{
			var constructors = type.GetConstructors();
			return constructors.FirstOrDefault(c => c.GetCustomAttributes(typeof(UniGitInject), true).Length > 0);
		}

		private void CheckCrossDependency(Resolve resolve, Type injectedType,ParameterInfo parameterInfo)
        {
            resolve.injectedParamsCached ??= BuildInjectionParams(GetInjectConstructor(resolve.InstanceType));
            if (CrossDependency(resolve.injectedParamsCached, injectedType))
				throw new Exception($"Cross References detected when injecting parameter {parameterInfo.Name} of '{injectedType.Name}' with '{resolve.InstanceType.Name}'");
        }

		private static bool CrossDependency(KeyValuePair<string,Type>[] injectedParams, Type type)
		{
			if (injectedParams == null) return false;

			foreach (var param in injectedParams)
			{
				if (param.Value == type || param.Value.IsAssignableFrom(type) || type.IsAssignableFrom(param.Value))
				{
					return true;
				}
			}

			return false;
		}

		private KeyValuePair<string, Type>[] BuildInjectionParams(ConstructorInfo constructorInfo)
        {
            return constructorInfo == null ? new KeyValuePair<string, Type>[0] : constructorInfo.GetParameters().Select(p => new KeyValuePair<string, Type>(p.Name, p.ParameterType)).ToArray();
        }

		private bool IsGenericTypeList(Type type)
		{
			return typeof(ICollection<>).IsAssignableFrom(type) || typeof(IList<>).IsAssignableFrom(type) || typeof(List<>).IsAssignableFrom(type);
		}

		private object HandleParameter(ParameterInfo parameter,Type injecteeType,object[] additionalArguments)
		{
			if (parameter.ParameterType.IsGenericType)
			{
				var parameterGenericType = parameter.ParameterType.GetGenericTypeDefinition();
				if (IsGenericTypeList(parameterGenericType))
				{
					var listType = parameter.ParameterType.GetGenericArguments()[0];
					var parameterResolves = new List<Resolve>();
					var instanceCollection = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));
					if (FindResolves(listType, parameter.Name, injecteeType, parameterResolves))
					{
						foreach (var r in parameterResolves)
						{
							CheckCrossDependency(r, injecteeType, parameter);
							instanceCollection.Add(r.GetInstance(this));
						}
					}
					return instanceCollection;
				}
			}

            if (FindResolve(parameter, injecteeType, out var resolve))
			{
				CheckCrossDependency(resolve, injecteeType, parameter);
				return resolve.GetInstance(this);
			}

			if (additionalArguments != null)
			{
				foreach (var additionalArgument in additionalArguments)
				{
                    if (additionalArgument is InjectionArgument injectionArgument)
					{
						if (injectionArgument.Id == parameter.Name && parameter.ParameterType.IsInstanceOfType(injectionArgument.Obj))
						{
							return injectionArgument.Obj;
						}
					}
					else if (parameter.ParameterType.IsInstanceOfType(additionalArgument))
					{
						return additionalArgument;
					}
				}
			}

			if (parent != null)
		    {
		        return parent.HandleParameter(parameter, injecteeType,additionalArguments);
		    }

			var customAttributes = parameter.GetCustomAttributes(typeof(UniGitInjectOptional),true);
            if (customAttributes.Length <= 0 && !parameter.IsOptional)
                throw new Exception($"Unresolved parameter: '{parameter.Name}' with type: '{parameter.ParameterType}' when injecting into '{injecteeType.Name}'");

            var value = parameter.DefaultValue;
            if (value != DBNull.Value)
            {
                return value;
            }

            return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;

        }

		private bool FindResolves(Type types,string parameterId, Type injecteeType,ICollection<Resolve> outResolves)
		{
			var foundResolves = false;
			foreach (var resolve in resolves)
            {
                if (!ValidResolve(resolve, types, parameterId, injecteeType)) continue;
                outResolves.Add(resolve);
                foundResolves = true;
            }
			return foundResolves;
		}

		private bool FindResolve(ParameterInfo parameter, Type injecteeType,out Resolve resolveOut)
		{
			Resolve newResolve = null;
			foreach (var resolve in resolves)
			{
				if (ValidResolve(resolve, parameter, injecteeType))
				{
					if (newResolve != null)
					{
						Debug.LogErrorFormat("Found multiple resolve of type: {0}",parameter.ParameterType);
					}
					newResolve = resolve;
				}
			}
			resolveOut = newResolve;
			return newResolve != null;
		}

		private bool ValidResolve(Resolve resolve, ParameterInfo parameter, Type injecteeType)
		{
			return ValidResolve(resolve,parameter.ParameterType,parameter.Name,injecteeType);
		}

		private bool ValidResolve(Resolve resolve, Type parameterType,string parameterId, Type injecteeType)
		{
			if (resolve.Type != parameterType) return false;
			if (resolve.WhenInjectedIntoType != null && resolve.WhenInjectedIntoType != injecteeType) return false;
			if (!string.IsNullOrEmpty(resolve.id) && parameterId != resolve.id) return false;
			return true;
		}

		public T GetInstance<T>()
		{
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
				{
					return (T) resolve.GetInstance(this);
				}
			}
			return default(T);
		}

		public T GetInstance<T>(string id)
		{
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && resolve.id == id)
				{
					return (T) resolve.GetInstance(this);
				}
			}
			return default(T);
		}

		public List<T> GetInstances<T>()
		{
			var resolveCallList = new HashSet<Resolve>();
			var instances = new List<T>();
			foreach (var resolve in resolves)
			{
				if (typeof(T) == resolve.Type && resolve.WhenInjectedIntoType == null && string.IsNullOrEmpty(resolve.id))
				{
					resolveCallList.Add(resolve);
					instances.Add((T)resolve.GetInstance(this));
				}
			}
			return instances;
		}

		public void CreateNonLazy()
		{
			foreach (var resolve in resolves)
			{
				if (resolve.IsNonLazy)
				{
					resolve.EnsureInstance(this);
				}
			}
		}

		public void Dispose()
		{
			if(disposed) return;

			disposed = true;
			resolves.Remove(injectorResolve);

			foreach (var resolve in resolves)
			{
				resolve.Dispose();
			}
		}

		public readonly struct ResolveCreateContext
		{
			public readonly InjectionHelper injectionHelper;
			public readonly object[] arg;

			public ResolveCreateContext(InjectionHelper injectionHelper, object[] arg)
			{
				this.injectionHelper = injectionHelper;
				this.arg = arg;
			}
		}

		public class Resolve<T> : Resolve
		{
			public Resolve() : base(typeof(T))
			{
				
			}

			public new T GetInstance(InjectionHelper injectionHelper)
			{
				return (T)base.GetInstance(injectionHelper);
			}

			public Resolve<T> FromMethod(Func<ResolveCreateContext,T> method)
			{
				base.FromMethod(c => method.Invoke(c));
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

		public class Resolve : IEquatable<Resolve>, IDisposable
		{
            private Func<ResolveCreateContext,object> method;
			internal KeyValuePair<string, Type>[] injectedParamsCached;
			internal string id;
			private object instance;
			private object[] arg;
            private bool cache = true;

			public Resolve(Type type)
			{
				this.Type = type;
				InstanceType = type;
			}

			public bool Equals(Resolve other)
			{
				if (other == null) return false;
				if (Type != other.Type || WhenInjectedIntoType != other.WhenInjectedIntoType || other.id != id) return false;
				return false;
			}

			public Resolve To(Type type)
			{
				if (this.Type.IsAssignableFrom(type) || type == this.Type)
				{
					InstanceType = type;
				}
				else
				{
					throw new Exception($"Type '{type}' must be able to be assigned from type '{this.Type}'");
				}
				return this;
			}

			public Resolve To<T>()
			{
				return To(typeof(T));
			}

			public Resolve WhenInjectedInto(Type type)
			{
				WhenInjectedIntoType = type;
				return this;
			}

			public Resolve WhenInjectedInto<T>()
			{
				return WhenInjectedInto(typeof(T));
			}

			public Resolve FromMethod(Func<ResolveCreateContext,object> method)
			{
				this.method = method;
				return this;
			}

			public Resolve FromInstance(object instance)
			{
                this.instance = instance ?? throw new Exception("Instance cannot be null");
				if (!Type.IsInstanceOfType(instance))
				{
					throw new Exception($"Instance of type: {instance.GetType()} does not match resolve type: {Type}");
				}
				return this;
			}

			public Resolve WithId(string id)
			{
				this.id = id;
				return this;
			}

			public Resolve NonLazy()
			{
				IsNonLazy = true;
				return this;
			}

			public Resolve WithArguments(params object[] arg)
			{
				this.arg = arg;
				return this;
			}

			public Resolve AsTransient()
			{
				cache = false;
				return this;
			}

			public object GetInstance(InjectionHelper injectionHelper)
			{
                if (!cache) return CreateInstance(injectionHelper);
                EnsureInstance(injectionHelper);
                return instance;
            }

			internal object CreateInstance(InjectionHelper injectionHelper)
			{
				if (method != null)
				{
					var i = method.Invoke(new ResolveCreateContext(injectionHelper, arg));
					injectionHelper.Inject(i);
					return i;
				}

				return injectionHelper.CreateInstance(InstanceType, arg);
			}

			internal void EnsureInstance(InjectionHelper injectionHelper)
            {
                instance ??= CreateInstance(injectionHelper);
            }

			public void Dispose()
			{
				var disposableInstance = instance as IDisposable;
                disposableInstance?.Dispose();
                arg = null;
			}

			public bool HasInstance => instance != null;

            public Type Type { get; }

            public Type InstanceType { get; private set; }

            public Type WhenInjectedIntoType { get; private set; }

            public string Id => id;

            public bool IsNonLazy { get; private set; }
        }
	}

	public class InjectionArgument
	{
        public InjectionArgument(string id,object obj)
		{
			this.Obj = obj;
			this.Id = id;
		}

		public object Obj { get; }

        public string Id { get; }
    }
}