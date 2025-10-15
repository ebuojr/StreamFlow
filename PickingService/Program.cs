using MassTransit;
using PickingService.Consumers;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "PickingService")
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/pickingservice-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting PickingService");

    var builder = Host.CreateApplicationBuilder(args);

    // Use Serilog for logging
    builder.Services.AddSerilog();

    // MassTransit with RabbitMQ and Priority Queue
    builder.Services.AddMassTransit(x =>
    {
        // Register consumers
        x.AddConsumer<StockReservedConsumer>();
        x.AddConsumer<PartialStockReservedConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            // Configure priority queue for picking (full stock reserved)
            cfg.ReceiveEndpoint("picking-stock-reserved", e =>
            {
                e.ConfigureConsumer<StockReservedConsumer>(context);

                // Priority queue configuration
                e.SetQueueArgument("x-max-priority", 10);
                
                // Low prefetch count for priority queue fairness
                e.PrefetchCount = 4;

                // Retry policy: 3 retries with 5 second interval
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            // Configure priority queue for partial stock (same priority handling)
            cfg.ReceiveEndpoint("picking-partial-stock-reserved", e =>
            {
                e.ConfigureConsumer<PartialStockReservedConsumer>(context);

                // Priority queue configuration (same as full stock)
                e.SetQueueArgument("x-max-priority", 10);
                
                // Low prefetch count for priority queue fairness
                e.PrefetchCount = 4;

                // Retry policy: 3 retries with 5 second interval
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });
        });
    });

    // Register Worker
    builder.Services.AddHostedService<PickingService.Worker>();

    var host = builder.Build();

    Log.Information("PickingService started successfully");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PickingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
