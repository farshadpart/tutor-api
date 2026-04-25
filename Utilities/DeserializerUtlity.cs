using System.Text.Json;

namespace Tutor.Api.Utilities
{
    public static class DeserializerUtlity
    {
        public static T? Deserialize<T, U>(string json, ILogger<U> logger)
        {
            var deserialized = JsonSerializer.Deserialize<T>(json);
            if (deserialized is null)
            {
                logger.LogWarning("Deserialization returned null for type {Type}", typeof(T).FullName);
                logger.LogDebug("Failed to deserialize JSON: {Json}", json);
            }
            return deserialized;
        }

        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
