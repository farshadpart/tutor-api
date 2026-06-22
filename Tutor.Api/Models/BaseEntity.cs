namespace Tutor.Api.Models
{
    public class BaseEntity<T> : IBaseEntity<T> where T : new()
    {
        public T Id { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
    }

    public interface IBaseEntity<T>
    {
        T Id { get; set; }
        DateTimeOffset CreatedAt { get; set; }
    }
}
