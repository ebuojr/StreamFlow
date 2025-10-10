using ERPGateway;
using ERPGateway.Configuration;
using ERPGateway.Messaging;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Configuration bindings
var rabbitSection = builder.Configuration.GetSection("RabbitMQSettings");
var erpApiSection = builder.Configuration.GetSection("ErpApi");
builder.Services.Configure<RabbitMqSettings>(rabbitSection);
builder.Services.Configure<ErpApiSettings>(erpApiSection);

var rabbit = rabbitSection.Get<RabbitMqSettings>() ?? new RabbitMqSettings();
var erpApi = erpApiSection.Get<ErpApiSettings>() ?? new ErpApiSettings();

// HttpClient for ERPApi
builder.Services.AddHttpClient<CreateOrderRequestConsumer>(client =>
{
	client.BaseAddress = new Uri(erpApi.BaseUrl.TrimEnd('/') + "/");
});

// MassTransit setup
builder.Services.AddMassTransit(x =>
{
	x.SetKebabCaseEndpointNameFormatter();
	x.AddConsumer<CreateOrderRequestConsumer>();

	x.UsingRabbitMq((context, cfg) =>
	{
		cfg.Host(rabbit.Host, rabbit.Port, h =>
		{
			h.Username(rabbit.Username);
			h.Password(rabbit.Password);
		});

		cfg.ReceiveEndpoint("create-order-request", e =>
		{
			e.ConfigureConsumer<CreateOrderRequestConsumer>(context);
			e.PrefetchCount = 16;
			e.ConcurrentMessageLimit = 8;
		});
	});
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
