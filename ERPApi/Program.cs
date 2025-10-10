using ERPApi.Configuration;
using ERPApi.DBContext;
using ERPApi.Repository.Order;
using ERPApi.Services.Order;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

// Database
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// services
builder.Services.AddScoped<IOrderRepository, OrderRepositroy>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseCors(); // Enable CORS middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
