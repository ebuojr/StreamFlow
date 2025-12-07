using ERPApi.Configuration;
using ERPApi.Consumers;
using ERPApi.DBContext;
using ERPApi.Repository.Order;
using ERPApi.Services.Order;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Load configuration early to get Seq settings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .Build();

// Get Seq settings - throw exception if not found
var seqBaseUrl = configuration["Seq:BaseUrl"] 
    ?? throw new InvalidOperationException("Seq:BaseUrl configuration is missing in appsettings.json");
var seqApiKey = configuration["Seq:ApiKey"] 
    ?? throw new InvalidOperationException("Seq:ApiKey configuration is missing in appsettings.json");

// Configure Serilog with professional formatting
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ERPApi")
    .Enrich.WithProperty("Environment", "Development")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/erpapi-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .WriteTo.Seq(seqBaseUrl,
        apiKey: seqApiKey,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    Log.Information("Starting ERPApi with Seq configured at {SeqUrl}", seqBaseUrl);

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // HttpClient for external API calls
    builder.Services.AddHttpClient();

    // Configure SeqSettings from configuration
    var seqSettings = builder.Configuration.GetSection("Seq").Get<SeqSettings>()
        ?? throw new InvalidOperationException("Seq configuration is missing in appsettings.json");
    builder.Services.AddSingleton(seqSettings);

    // CORS - Allow Blazor client
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5035", "https://localhost:7103")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // MassTransit with RabbitMQ
    var rabbitMqSettings = builder.Configuration.GetSection("RabbitMQSettings").Get<RabbitMqSettings>()
        ?? throw new InvalidOperationException("RabbitMQ configuration is missing");
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();

        // Register consumers
        x.AddConsumer<ERPApi.Consumers.CreateOrderRequestConsumer>();
        x.AddConsumer<ERPApi.Consumers.StockReservedConsumer>();
        x.AddConsumer<ERPApi.Consumers.StockUnavailableConsumer>();
        x.AddConsumer<ERPApi.Consumers.OrderPickedConsumer>();
        x.AddConsumer<ERPApi.Consumers.OrderPackedConsumer>();
        x.AddConsumer<ERPApi.Consumers.OrderInvalidConsumer>();

        // Configure Entity Framework Outbox
        x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
        {
            o.UseSqlite();
            
            // Enable the bus outbox - this delivers messages from the outbox to the transport
            o.UseBusOutbox();
            
            // Query delay - how often to check for pending outbox messages
            o.QueryDelay = TimeSpan.FromSeconds(1);
        });

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitMqSettings.Host, rabbitMqSettings.Port, h =>
            {
                h.Username(rabbitMqSettings.Username);
                h.Password(rabbitMqSettings.Password);
            });

            cfg.Message<Contracts.Events.OrderCreated>(x => x.SetEntityName("Contracts.Events:OrderCreated"));
            cfg.Publish<Contracts.Events.OrderCreated>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.OrderInvalid>(x => x.SetEntityName("Contracts.Events:OrderInvalid"));
            cfg.Publish<Contracts.Events.OrderInvalid>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.StockReserved>(x => x.SetEntityName("Contracts.Events:StockReserved"));
            cfg.Publish<Contracts.Events.StockReserved>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.StockUnavailable>(x => x.SetEntityName("Contracts.Events:StockUnavailable"));
            cfg.Publish<Contracts.Events.StockUnavailable>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.OrderPicked>(x => x.SetEntityName("Contracts.Events:OrderPicked"));
            cfg.Publish<Contracts.Events.OrderPicked>(x => x.ExchangeType = "topic");

            cfg.Message<Contracts.Events.OrderPacked>(x => x.SetEntityName("Contracts.Events:OrderPacked"));
            cfg.Publish<Contracts.Events.OrderPacked>(x => x.ExchangeType = "topic");

            // Configure the receive endpoint for CreateOrderRequest (Request/Reply pattern)
            // UseEntityFrameworkOutbox enables the outbox for this endpoint
            cfg.ReceiveEndpoint("create-order-request", e =>
            {
                e.PrefetchCount = 1; // SQLite: process one message at a time
                e.UseEntityFrameworkOutbox<OrderDbContext>(context);
                e.ConfigureConsumer<ERPApi.Consumers.CreateOrderRequestConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            // Configure receive endpoints for state update events
            cfg.ReceiveEndpoint("erp-stock-reserved", e =>
            {
                e.PrefetchCount = 1; // SQLite: prevent database lock contention
                e.UseEntityFrameworkOutbox<OrderDbContext>(context);
                e.ConfigureConsumer<ERPApi.Consumers.StockReservedConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ReceiveEndpoint("erp-stock-unavailable", e =>
            {
                e.PrefetchCount = 1;
                e.UseEntityFrameworkOutbox<OrderDbContext>(context);
                e.ConfigureConsumer<ERPApi.Consumers.StockUnavailableConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ReceiveEndpoint("erp-order-picked", e =>
            {
                e.PrefetchCount = 1;
                e.UseEntityFrameworkOutbox<OrderDbContext>(context);
                e.ConfigureConsumer<ERPApi.Consumers.OrderPickedConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ReceiveEndpoint("erp-order-packed", e =>
            {
                e.PrefetchCount = 1;
                e.UseEntityFrameworkOutbox<OrderDbContext>(context);
                e.ConfigureConsumer<ERPApi.Consumers.OrderPackedConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            // Invalid Order Channel - catch validation failures
            cfg.ReceiveEndpoint("erp-invalid-order", e =>
            {
                e.PrefetchCount = 1;
                e.ConfigureConsumer<ERPApi.Consumers.OrderInvalidConsumer>(context);
            });

            // Dead Letter Channel - catch all faulted messages
            cfg.ReceiveEndpoint("erp-dead-letter", e =>
            {
                // Fault consumer for Request-Reply pattern
                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.CreateOrderRequest>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.CreateOrderRequest>>>()));

                // Fault consumers for event-driven state updates
                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderCreated>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderCreated>>>()));

                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.Events.StockReserved>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.Events.StockReserved>>>()));

                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.Events.StockUnavailable>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.Events.StockUnavailable>>>()));

                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderPicked>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderPicked>>>()));

                e.Consumer(() => new ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderPacked>(
                    context.GetRequiredService<ILogger<ERPApi.Consumers.FaultConsumer<Contracts.Events.OrderPacked>>>()));
            });

            cfg.ConfigureEndpoints(context);
        });
    });

    // Database with SQLite optimized for concurrent access
    builder.Services.AddDbContext<OrderDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            })
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
            .EnableDetailedErrors(builder.Environment.IsDevelopment());
    },
    ServiceLifetime.Scoped);

    // services
    builder.Services.AddScoped<IOrderRepository, OrderRepositroy>();
    builder.Services.AddScoped<IOrderService, OrderService>();

    // FluentValidation
    builder.Services.AddScoped<IValidator<Entities.Model.Order>, ERPApi.Services.Validation.OrderValidator>();
    var app = builder.Build();

    // Initialize SQLite for concurrent access (WAL mode)
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA busy_timeout=5000;
                PRAGMA synchronous=NORMAL;
            ";
                await command.ExecuteNonQueryAsync();
            }
            Log.Information("SQLite initialized with WAL mode for concurrent access");
        }
    }
    catch (Exception ex)
    {
        // Ignore errors during design-time (migrations)
        Log.Debug(ex, "Could not initialize SQLite (likely during design-time)");
    }

    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors(); // Enable CORS middleware
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("ERPApi started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ERPApi terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
