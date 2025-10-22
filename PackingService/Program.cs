using MassTransit;
using PackingService.Consumers;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "PackingService")
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/packingservice-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting PackingService");

    var builder = Host.CreateApplicationBuilder(args);

    // Use Serilog for logging
    builder.Services.AddSerilog();

    // MassTransit with RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        // Register consumers
        x.AddConsumer<OrderPickedConsumer>();
        
        // Register fault consumer for Dead Letter Channel
        x.AddConsumer<PackingService.Consumers.FaultConsumer<Contracts.Events.OrderPicked>>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            // ✅ EXPLICITLY CONFIGURE TOPIC EXCHANGES (FIX FOR FANOUT ISSUE)
            cfg.Publish<Contracts.Events.OrderPacked>(x => x.ExchangeType = "topic");
            
            // ✅ CONFIGURE CONSUME TOPOLOGY (for events we consume)
            cfg.Message<Contracts.Events.OrderPicked>(x => x.SetEntityName("Contracts.Events:OrderPicked"));
            cfg.Publish<Contracts.Events.OrderPicked>(x => x.ExchangeType = "topic");

            // Configure receive endpoint for packing
            cfg.ReceiveEndpoint("packing-order-picked", e =>
            {
                e.ConfigureConsumer<OrderPickedConsumer>(context);

                // Standard prefetch count (no priority queue needed for packing)
                e.PrefetchCount = 16;

                // Retry policy: 3 retries with 5 second interval
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            // Dead Letter Channel for failed packing operations
            cfg.ReceiveEndpoint("packing-dead-letter", e =>
            {
                e.ConfigureConsumer<PackingService.Consumers.FaultConsumer<Contracts.Events.OrderPicked>>(context);
            });
        });
    });

    // Register Worker
    builder.Services.AddHostedService<PackingService.Worker>();

    var host = builder.Build();

    Log.Information("PackingService started successfully");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PackingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
