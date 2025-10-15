namespace PackingService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 PackingService is starting...");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("📮 PackingService is running at: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("⏹️ PackingService is stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error occurred in PackingService");
            throw;
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("✅ PackingService started successfully");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 PackingService stopped");
        return base.StopAsync(cancellationToken);
    }
}
