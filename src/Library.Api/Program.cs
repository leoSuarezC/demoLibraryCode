using Library.Api;
using Library.Api.Endpoints;
using Library.Domain;
using Library.Infrastructure;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:3000"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        // The frontend reads this to tell a replayed check-out from a fresh one.
        // Custom response headers are invisible to browser JS unless exposed.
        .WithExposedHeaders("Idempotent-Replay")));

var app = builder.Build();

// A domain rule broken here is the caller's mistake, not a server fault, so it is
// reported as 400 rather than 500. Anything else stays a 500 and gets logged.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    if (error is DomainException domainError)
    {
        await Results
            .Problem(detail: domainError.Message,
                     statusCode: StatusCodes.Status400BadRequest,
                     title: "Invalid request")
            .ExecuteAsync(context);
        return;
    }

    await Results
        .Problem(detail: "An unexpected error occurred.",
                 statusCode: StatusCodes.Status500InternalServerError)
        .ExecuteAsync(context);
}));

await DatabaseInitializer.InitialiseAsync(app.Services);

app.UseCors(FrontendCorsPolicy);

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithTags("Diagnostics")
    .WithName("Health");

app.MapBookEndpoints();
app.MapLoanEndpoints();
app.MapMemberEndpoints();
app.MapAiEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
