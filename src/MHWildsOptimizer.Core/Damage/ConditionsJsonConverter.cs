using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// Reads <see cref="Conditions"/> written before the proc damage toggle was split (2026-10-08): a <c>proc_damage</c> value
/// becomes both <see cref="Conditions.DarkArtsShockwave"/> and <see cref="Conditions.BadBlood"/>. Writing is unchanged.
/// </summary>
public sealed class ConditionsJsonConverter : JsonConverter<Conditions>
{
    private const string Legacy = "proc_damage";
    private static readonly string[] Split = ["dark_arts_shockwave", "bad_blood"];

    public override Conditions? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader);
        if (node is JsonObject o && Take(o, Legacy) is { } legacy)
        {
            foreach (var key in Split)
                if (!o.ContainsKey(key)) o[key] = legacy.GetValue<bool>();
        }
        return node.Deserialize<Conditions>(Inner(options));
    }

    public override void Write(Utf8JsonWriter writer, Conditions value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, Inner(options));

    /// <summary>Removes the key (any casing) and returns its value.</summary>
    private static JsonNode? Take(JsonObject o, string key)
    {
        var name = o.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        var value = o[name];
        o.Remove(name);
        return value;
    }

    // the same options without this converter, so the record is (de)serialized the normal way
    private static readonly Dictionary<JsonSerializerOptions, JsonSerializerOptions> Inners = new();

    private static JsonSerializerOptions Inner(JsonSerializerOptions options)
    {
        lock (Inners)
        {
            if (Inners.TryGetValue(options, out var inner)) return inner;
            inner = new JsonSerializerOptions(options);
            foreach (var c in inner.Converters.Where(c => c is ConditionsJsonConverter).ToList()) inner.Converters.Remove(c);
            return Inners[options] = inner;
        }
    }
}
