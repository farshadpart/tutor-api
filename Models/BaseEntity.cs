using System.Text.Json.Serialization;

namespace Tutor.Api.Models
{
    public class BaseEntity<T> : RowVersionClass, IRowVersion, IBaseEntity<T> where T : new()
    {
        public T Id { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
    }

    public interface IBaseEntity<T> : IRowVersion
    {
        T Id { get; set; }
        DateTimeOffset CreatedAt { get; set; }
    }

    public interface IRowVersion
    {
        public uint RowVersion { get; set; }
    }

    public class RowVersionClass : IRowVersion
    {
        [JsonPropertyName("xmin")]
        public uint RowVersion { get; set; }
    }
}
