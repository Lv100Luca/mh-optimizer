using System.Text.Json;
using System.Text.Json.Serialization;

namespace MHWildsOptimizer.Api;

/// <summary>JSON conventions of the API: the same snake_case + lower-case enum names the request files use.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions());

    public static JsonSerializerOptions Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.DictionaryKeyPolicy = null; // skill names stay as they are
        o.PropertyNameCaseInsensitive = true;
        o.ReadCommentHandling = JsonCommentHandling.Skip;
        o.AllowTrailingCommas = true;
        o.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.Converters.Add(new Core.Damage.ConditionsJsonConverter());
        return o;
    }
}
