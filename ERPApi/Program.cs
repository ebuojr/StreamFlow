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
    .WriteTo.Seq("http://localhost:5341",
        apiKey: null,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    Log.Information("Starting ERPApi");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// HttpClient for external API calls
builder.Services.AddHttpClient();

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
    
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlite();
        o.UseBusOutbox();
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
        cfg.ReceiveEndpoint("create-order-request", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.CreateOrderRequestConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
        
        // Configure receive endpoints for state update events
        cfg.ReceiveEndpoint("erp-stock-reserved", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.StockReservedConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
        
        cfg.ReceiveEndpoint("erp-stock-unavailable", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.StockUnavailableConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
        
        cfg.ReceiveEndpoint("erp-order-picked", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.OrderPickedConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
        
        cfg.ReceiveEndpoint("erp-order-packed", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.OrderPackedConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
        
        // Invalid Order Channel - catch validation failures
        cfg.ReceiveEndpoint("erp-invalid-order", e =>
        {
            e.ConfigureConsumer<ERPApi.Consumers.OrderInvalidConsumer>(context);
            // No retry needed - these are permanently invalid orders
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

// Database
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// services
builder.Services.AddScoped<IOrderRepository, OrderRepositroy>();
builder.Services.AddScoped<IOrderService, OrderService>();

// FluentValidation
builder.Services.AddScoped<IValidator<Entities.Model.Order>, ERPApi.Services.Validation.OrderValidator>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("database")
    .AddRabbitMQ(sp => 
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = rabbitMqSettings.Host,
            VirtualHost = rabbitMqSettings.Port,
            UserName = rabbitMqSettings.Username,
            Password = rabbitMqSettings.Password
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    },
    name: "rabbitmq",
    tags: new[] { "messaging" });

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseCors(); // Enable CORS middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health Check endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

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
