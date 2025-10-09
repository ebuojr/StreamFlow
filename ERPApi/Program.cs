using ERPApi.DBContext;
using ERPApi.Repository.Order;
using ERPApi.Services.Order;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// services
builder.Services.AddScoped<IOrderRepository, OrderRepositroy>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
