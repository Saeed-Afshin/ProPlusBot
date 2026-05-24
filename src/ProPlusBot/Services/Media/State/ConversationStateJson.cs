using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProPlusBot.Services.Media.State;

internal static class ConversationStateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
