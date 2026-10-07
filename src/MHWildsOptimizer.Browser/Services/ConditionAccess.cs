using System.Reflection;
using MHWildsOptimizer.Core.Damage;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>The boolean conditions by their C# property name (<see cref="ConditionDto.Property"/>), as the catalog lists them.</summary>
public static class ConditionAccess
{
    private static readonly Dictionary<string, PropertyInfo> Properties = typeof(Conditions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
        .ToDictionary(p => p.Name);

    public static bool Get(Conditions c, string property) => Properties.TryGetValue(property, out var p) && p.GetValue(c) is true;

    /// <summary>A copy of <paramref name="c"/> with one boolean changed.</summary>
    public static Conditions With(Conditions c, string property, bool value)
    {
        var copy = c with { };
        if (Properties.TryGetValue(property, out var p)) p.SetValue(copy, value);
        return copy;
    }
}
