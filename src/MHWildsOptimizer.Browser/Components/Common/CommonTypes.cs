using Microsoft.AspNetCore.Components;

namespace MHWildsOptimizer.Browser.Components.Common;

/// <summary>A choice of <see cref="Segmented{T}"/>; <paramref name="Content"/> replaces the plain label when given.</summary>
public sealed record SegmentOption<T>(T Value, string Label, RenderFragment? Content = null, string? Hint = null, bool Disabled = false);

/// <summary>An entry of <see cref="SelectBox"/>; entries with a group are listed under that group's heading.</summary>
public sealed record SelectOption(string Value, string Label, string? Group = null);

/// <summary>
/// One choice of a <see cref="Picker{T}"/>; <paramref name="Search"/> is the lower-case text the query is matched against
/// (every word must occur).
/// </summary>
public sealed record PickerOption<T>(string Key, T Value, string Search, RenderFragment Render, string? Group = null);
