using MassTransit;
using PickingService.Configuration;
using PickingService.Consumers;
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
    .WriteTo.Seq(seqBaseUrl,
        apiKey: seqApiKey,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    Log.Information("Starting PickingService with Seq configured at {SeqUrl}", seqBaseUrl);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<StockReservedConsumer>();
        x.AddConsumer<PickingService.Consumers.FaultConsumer<Contracts.Events.StockReserved>>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.Message<Contracts.Events.OrderPicked>(x => x.SetEntityName("Contracts.Events:OrderPicked"));
            cfg.Publish<Contracts.Events.OrderPicked>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.StockReserved>(x => x.SetEntityName("Contracts.Events:StockReserved"));
            cfg.Publish<Contracts.Events.StockReserved>(x => x.ExchangeType = "topic");

            cfg.ReceiveEndpoint("picking-stock-reserved", e =>
            {
                e.PrefetchCount = 1;
                e.ConfigureConsumer<StockReservedConsumer>(context);

                // Enable priority queue (0-10 scale)
                e.Bind("Contracts.Events:StockReserved", s =>
                {
                    s.ExchangeType = "topic";
                });

                // RabbitMQ priority queue argument
                e.SetQueueArgument("x-max-priority", 10);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ReceiveEndpoint("picking-dead-letter", e =>
            {
                e.ConfigureConsumer<PickingService.Consumers.FaultConsumer<Contracts.Events.StockReserved>>(context);
            });
        });
    });

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
