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

var allowedOrigins = builder.Configuration.GetSection("Site:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

// Serve the static website so the page and the API share one origin.
var siteRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Site:Root"] ?? "wwwroot"));
if (Directory.Exists(siteRoot))
{
    var files = new PhysicalFileProvider(siteRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.MapControllers();

app.Run();
