using InventoryService.Configuration;
using MassTransit;
using Serilog;
using Serilog.Events;

// Load configuration early to get Seq settings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .Build();

// Get Seq settings - throw exception if not found
var seqBaseUrl = configuration["Seq:BaseUrl"] 
    ?? throw new InvalidOperationException("Seq:BaseUrl configuration is missing in appsettings.json");
var seqApiKey = configuration["Seq:ApiKey"] 
    ?? throw new InvalidOperationException("Seq:ApiKey configuration is missing in appsettings.json");

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
    .WriteTo.Seq(seqBaseUrl,
        apiKey: seqApiKey,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    Log.Information("Starting InventoryService with Seq configured at {SeqUrl}", seqBaseUrl);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<InventoryService.Consumers.OrderCreatedConsumer>();
        x.AddConsumer<InventoryService.Consumers.FaultConsumer<Contracts.Events.OrderCreated>>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("localhost", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });

            cfg.Message<Contracts.Events.StockReserved>(x => x.SetEntityName("Contracts.Events:StockReserved"));
            cfg.Publish<Contracts.Events.StockReserved>(x => x.ExchangeType = "topic");
            
            cfg.Message<Contracts.Events.StockUnavailable>(x => x.SetEntityName("Contracts.Events:StockUnavailable"));
            cfg.Publish<Contracts.Events.StockUnavailable>(x => x.ExchangeType = "topic");
            
            cfg.Message<Contracts.Events.OrderCreated>(x => x.SetEntityName("Contracts.Events:OrderCreated"));
            cfg.Publish<Contracts.Events.OrderCreated>(x => x.ExchangeType = "topic");

            cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

            cfg.ReceiveEndpoint("inventory-check", e =>
            {
                e.PrefetchCount = 16;
                e.ConfigureConsumer<InventoryService.Consumers.OrderCreatedConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ReceiveEndpoint("inventory-dead-letter", e =>
            {
                e.ConfigureConsumer<InventoryService.Consumers.FaultConsumer<Contracts.Events.OrderCreated>>(context);
            });

            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddHealthChecks()
        .AddRabbitMQ();

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
