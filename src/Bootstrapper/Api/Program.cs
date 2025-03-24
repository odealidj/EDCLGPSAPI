var builder = WebApplication.CreateBuilder(args);

// add services to the container
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddGpsInterfaceModule(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app 
    .UseCatalogModule()
    .UseGpsInterfaceModule();
app.Run();
