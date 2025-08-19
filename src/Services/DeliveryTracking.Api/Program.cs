using Carter;
using Delivery;
using Shared.Exceptions.Handler;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

var deliveryAssembly = typeof(DeliveryModule).Assembly;

// 1. Carter & MediatR assembly scanning
builder.Services.AddCarterWithAssemblies(deliveryAssembly);
builder.Services.AddMediatRWithAssemblies(deliveryAssembly);

// 2. Register Delivery Module
builder.Services.AddDeliveryModule(builder.Configuration);

// 3. Exception Handling & OpenAPI/Swagger
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(options => { });

app.UseDeliveryModule();
app.MapCarter();
app.MapHealthChecks("/hc");

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Delivery Tracking Service",
    Version = "v1.0",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.Run();
