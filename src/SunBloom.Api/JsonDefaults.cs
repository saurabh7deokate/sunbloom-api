using System.Text.Json;

namespace SunBloom.Api;

/// <summary>Shared serializer options, so formatting is consistent and allocated once.</summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Health { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
