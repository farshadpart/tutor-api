using System.Text.Json.Serialization;

namespace Tutor.Api.Models.Tutor.Api.Contracts.Log
{
    public record LogRequest
    (
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        LogLevel LogLevel, 
        string? Exception,
        string Message, 
        object[] Arguments
    );
}
