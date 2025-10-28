using MassTransit;
using PickingService.Consumers;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "PickingService")
    .Enrich.WithProperty("Environment", "Development")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/pickingservice-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .WriteTo.Seq("http://localhost:5341")
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
        
        // Register fault consumers for Dead Letter Channel
        x.AddConsumer<PickingService.Consumers.FaultConsumer<Contracts.Events.StockReserved>>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            // ✅ EXPLICITLY CONFIGURE TOPIC EXCHANGES (FIX FOR FANOUT ISSUE)
            // Events this service PUBLISHES
            cfg.Message<Contracts.Events.OrderPicked>(x => x.SetEntityName("Contracts.Events:OrderPicked"));
            cfg.Publish<Contracts.Events.OrderPicked>(x => x.ExchangeType = "topic");
            
            // ✅ CONFIGURE CONSUME TOPOLOGY (for events we consume)
            cfg.Message<Contracts.Events.StockReserved>(x => x.SetEntityName("Contracts.Events:StockReserved"));
            cfg.Publish<Contracts.Events.StockReserved>(x => x.ExchangeType = "topic");

            // Configure priority queue for picking (handles both full and partial stock)
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

            // Dead Letter Channel for failed picking operations
            cfg.ReceiveEndpoint("picking-dead-letter", e =>
            {
                e.ConfigureConsumer<PickingService.Consumers.FaultConsumer<Contracts.Events.StockReserved>>(context);
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
