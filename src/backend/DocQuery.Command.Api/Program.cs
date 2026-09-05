using DocQuery.Application;
using DocQuery.Command.Api.Cors;
using DocQuery.Command.Api.Endpoints;
using DocQuery.Command.Api.ExceptionHandling;
using DocQuery.Command.Api.Options;
using DocQuery.Command.Api.RateLimiting;
using DocQuery.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddUploadOptions(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<BrokenCircuitExceptionHandler>();
builder.Services.AddUploadRateLimiting(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapEndpoints();

app.Run();

/// <summary>Exposed so integration tests can bootstrap the host via WebApplicationFactory.</summary>
public partial class Program;
