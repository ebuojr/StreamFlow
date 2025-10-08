namespace OrderApi.Configuration
{
    public class ConfigReader
    {
        public class RabbitMqSettings
        {
            public string Host { get; set; } = string.Empty;
            public string Port { get; set; } = "/";
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}
