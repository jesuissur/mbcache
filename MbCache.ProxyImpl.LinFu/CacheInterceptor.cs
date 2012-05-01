using System;
using System.Linq;
using System.Reflection;
using LinFu.DynamicProxy;
using MbCache.Configuration;
using MbCache.Core;
using MbCache.Logic;

namespace MbCache.ProxyImpl.LinFu
{
	public class CacheInterceptor : IInterceptor
	{
		private readonly ICache _cache;
		private readonly IMbCacheKey _cacheKey;
		private readonly ILockObjectGenerator _lockObjectGenerator;
		private readonly Type _type;
		private readonly ImplementationAndMethods _methodData;
		private readonly object _target;
		private readonly ICachingComponent _cachingComponent;

		public CacheInterceptor(ICache cache,
										IMbCacheKey cacheKey,
										ILockObjectGenerator lockObjectGenerator, 
										Type type,
										ImplementationAndMethods methodData,
										params object[] ctorParameters)
		{
			_cache = cache;
			_cacheKey = cacheKey;
			_lockObjectGenerator = lockObjectGenerator;
			_type = type;
			_methodData = methodData;
			_target = createTarget(ctorParameters);
			_cachingComponent = new CachingComponent(cache, cacheKey, type, methodData);
		}

		private object createTarget(object[] ctorParameters)
		{
			try
			{
				return Activator.CreateInstance(_methodData.ConcreteType, ctorParameters);
			}
			catch (MissingMethodException ex)
			{
				var ctorParamMessage = "Incorrect number of parameters to ctor for type " + _methodData.ConcreteType;
				throw new ArgumentException(ctorParamMessage, ex);
			}
		}

		public object Intercept(InvocationInfo info)
		{
			return methodMarkedForCaching(info.TargetMethod) ?
					  interceptUsingCache(info) : callOriginalMethod(info);
		}

		private bool methodMarkedForCaching(MethodInfo method)
		{
			return _methodData.Methods.Any(cachedMethod => cachedMethod.Name.Equals(method.Name));
		}

		private object interceptUsingCache(InvocationInfo info)
		{
			var method = info.TargetMethod;
			var arguments = info.Arguments;
			var key = _cacheKey.Key(_type, _cachingComponent, method, arguments);
			if (key == null)
			{
				return callOriginalMethod(info);
			}
			var cachedValue = _cache.Get(key);
			if (cachedValue != null)
			{
				return cachedValue;
			}
			var lockObject = _lockObjectGenerator.GetFor(key);
			if (lockObject == null)
			{
				return executeAndPutInCache(info, key);
			}
			lock (lockObject)
			{
				var cachedValue2 = _cache.Get(key);
				return cachedValue2 ?? executeAndPutInCache(info, key);
			}
		}

		private object executeAndPutInCache(InvocationInfo info, string key)
		{
			var retVal = callOriginalMethod(info);
			_cache.Put(key, retVal);
			return retVal;
		}

		private object callOriginalMethod(InvocationInfo info)
		{
			if (info.TargetMethod.DeclaringType == typeof(ICachingComponent))
			{
				return info.TargetMethod.Invoke(_cachingComponent, info.Arguments);
			}
			return info.TargetMethod.Invoke(_target, info.Arguments);
		}
	}
}