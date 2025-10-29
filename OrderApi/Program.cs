using MassTransit;
using OrderApi.Services.Order;
using OrderApi.Configuration;
using Contracts;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "OrderApi")
    .Enrich.WithProperty("Environment", "Development")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/orderapi-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting OrderApi");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS - Allow all origins, methods, and headers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
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

    x.AddRequestClient<CreateOrderRequest>(new Uri("queue:create-order-request"), timeout: RequestTimeout.After(m: 1));
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMqSettings.Host, rabbitMqSettings.Port, h =>
        {
            h.Username(rabbitMqSettings.Username);
            h.Password(rabbitMqSettings.Password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Services
builder.Services.AddScoped<IOrderService, OrderService>();

// Health Checks
builder.Services.AddHealthChecks()
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

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderApi terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
