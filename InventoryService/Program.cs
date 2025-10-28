using MassTransit;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "InventoryService")
    .Enrich.WithProperty("Environment", "Development")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/inventoryservice-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting InventoryService");

    var builder = Host.CreateApplicationBuilder(args);

    // Use Serilog for logging
    builder.Services.AddSerilog();

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register consumers here
    x.AddConsumer<InventoryService.Consumers.OrderCreatedConsumer>();
    x.AddConsumer<InventoryService.Consumers.FaultConsumer<Contracts.Events.OrderCreated>>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // ✅ EXPLICITLY CONFIGURE TOPIC EXCHANGES (FIX FOR FANOUT ISSUE)
        // Events this service PUBLISHES
        cfg.Message<Contracts.Events.StockReserved>(x => x.SetEntityName("Contracts.Events:StockReserved"));
        cfg.Publish<Contracts.Events.StockReserved>(x => x.ExchangeType = "topic");
        
        cfg.Message<Contracts.Events.StockUnavailable>(x => x.SetEntityName("Contracts.Events:StockUnavailable"));
        cfg.Publish<Contracts.Events.StockUnavailable>(x => x.ExchangeType = "topic");
        
        // ✅ CONFIGURE CONSUME TOPOLOGY (for events we consume)
        cfg.Message<Contracts.Events.OrderCreated>(x => x.SetEntityName("Contracts.Events:OrderCreated"));
        cfg.Publish<Contracts.Events.OrderCreated>(x => x.ExchangeType = "topic");

        // Configure retry policy for all consumers
        cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        // Main queue: inventory-check
        cfg.ReceiveEndpoint("inventory-check", e =>
        {
            e.ConfigureConsumer<InventoryService.Consumers.OrderCreatedConsumer>(context);
        });

        // Dead Letter Channel (DLC): inventory-dead-letter
        cfg.ReceiveEndpoint("inventory-dead-letter", e =>
        {
            e.ConfigureConsumer<InventoryService.Consumers.FaultConsumer<Contracts.Events.OrderCreated>>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddRabbitMQ();

    // Register Worker
    builder.Services.AddHostedService<InventoryService.Worker>();

    var host = builder.Build();

    Log.Information("InventoryService started successfully");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "InventoryService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
