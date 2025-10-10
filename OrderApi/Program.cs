using MassTransit;
using OrderApi.Services.Order;
using OrderApi.Configuration;
using Contracts;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

    x.AddRequestClient<CreateOrderRequest>(new Uri("queue:create-order-request"));
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

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseCors(); // Enable CORS middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
