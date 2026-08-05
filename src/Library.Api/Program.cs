using Library.Infrastructure;
using Library.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

await DatabaseInitializer.InitialiseAsync(app.Services);

app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
