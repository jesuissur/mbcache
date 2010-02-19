using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MbCache.Core;
using MbCache.Logic;

namespace MbCache.Configuration
{
    public class CacheBuilder
    {
        private readonly IDictionary<Type, ICollection<string>> _cachedMethods;

        public CacheBuilder()
        {
            _cachedMethods = new Dictionary<Type, ICollection<string>>();
        }


        public IMbCacheFactory BuildFactory(ICacheFactory cacheFactory)
        {
            return new MbCacheFactory(cacheFactory.Create(), _cachedMethods);
        }


        public void UseCacheFor<T>(Expression<Func<T, object>> expression)
        {
            Type type = typeof (T);
            if (!_cachedMethods.ContainsKey(type))
                _cachedMethods[type] = new List<string>();
            _cachedMethods[type].Add(memberName(expression.Body));                
        }

        private static string memberName(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                    var memberExpression = (MemberExpression)expression;
                    var supername = memberName(memberExpression.Expression);

                    if (String.IsNullOrEmpty(supername))
                        return memberExpression.Member.Name;

                    return String.Concat(supername, '.', memberExpression.Member.Name);

                case ExpressionType.Call:
                    var callExpression = (MethodCallExpression)expression;
                    return callExpression.Method.Name;

                case ExpressionType.Convert:
                    var unaryExpression = (UnaryExpression)expression;
                    return memberName(unaryExpression.Operand);

                case ExpressionType.Parameter:
                    return String.Empty;

                default:
                    throw new ArgumentException("The expression is not a member access or method call expression");
            }
        }
    }
}