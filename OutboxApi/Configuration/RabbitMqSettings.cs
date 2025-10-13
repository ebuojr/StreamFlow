namespace OutboxApi.Configuration
{
    public class RabbitMqSettings
    {
        public string Host { get; set; } = "localhost";
        public string Port { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
    }
}
