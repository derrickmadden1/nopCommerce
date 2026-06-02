using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Nop.Services.Localization;

/// <summary>
/// Caches delegates compiled from property selector expressions (<c>e =&gt; e.PropertyName</c>)
/// </summary>
/// <remarks>
/// Avoids repeated calls to <see cref="LambdaExpression.Compile"/> for the same property selector,
/// which would otherwise emit a new <c>DynamicMethod</c> on every invocation and create significant
/// GC and finalizer pressure under load. Cache key is <c>(EntityType, PropertyName)</c>; selectors
/// that do not target a property fall back to a plain compile.
/// </remarks>
public static class PropertySelectorCache
{
    #region Fields

    private static readonly ConcurrentDictionary<(Type EntityType, string PropertyName), Delegate> _cache = new();

    #endregion

    #region Methods

    /// <summary>
    /// Get a compiled delegate for the given property selector, reusing a previously cached one
    /// when the selector targets the same property on the same entity type
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <typeparam name="TPropType">Property type</typeparam>
    /// <param name="selector">Property selector expression</param>
    /// <returns>Compiled delegate</returns>
    public static Func<TEntity, TPropType> GetCompiled<TEntity, TPropType>(Expression<Func<TEntity, TPropType>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        //fall back to a plain compile for non-property selectors (e.g. method calls, casts)
        if (selector.Body is not MemberExpression { Member: PropertyInfo propInfo })
            return selector.Compile();

        return (Func<TEntity, TPropType>)_cache.GetOrAdd(
            (typeof(TEntity), propInfo.Name),
            static (_, sel) => sel.Compile(),
            selector);
    }

    #endregion
}
