using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Gyroown.Services;

/// <summary>
/// Shared JSON options with reflection fallback (required for Release/AOT compatibility).
/// </summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
}
