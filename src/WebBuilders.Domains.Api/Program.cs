using Microsoft.Extensions.FileProviders;
using WebBuilders.Domains.Api.Infrastructure;
using WebBuilders.Domains.Business;
using WebBuilders.Domains.Data;

var builder = WebApplication.CreateBuilder(args);

// Composition root: presentation wires the business and data tiers together.
builder.Services.AddBusinessLayer(builder.Configuration);
builder.Services.AddDataLayer(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainsExceptionHandler>();

// Page and API share one origin, so CORS is only needed for extra local tools (e.g. VS Code Live Server).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

// Serve the static website next to the API: from /website locally, from wwwroot on the server.
// Leave Website:Root empty to run the API on its own.
var websiteRoot = builder.Configuration["Website:Root"];
if (!string.IsNullOrWhiteSpace(websiteRoot))
{
    var fullPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, websiteRoot));
    if (Directory.Exists(fullPath))
    {
        var files = new PhysicalFileProvider(fullPath);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    }
}
else
{
    app.MapGet("/", () => Results.Ok(new { service = "Web Builders Domains API", status = "ok" }));
}

app.MapControllers();

app.Run();
