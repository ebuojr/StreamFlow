namespace Entities.Model
{
    public class Outbox
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; }
        public string Payload { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int RetryCount { get; set; }
    }
}
