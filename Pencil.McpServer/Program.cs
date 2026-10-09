using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pencil.McpServer.Services;
using Pencil.McpServer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
	options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<ScenarioBuilder>();
builder.Services.AddSingleton<IFtpUploader, FtpUploader>();
builder.Services.AddSingleton<PencilRenderer>();
builder.Services
	.AddMcpServer()
	.WithStdioServerTransport()
	.WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
