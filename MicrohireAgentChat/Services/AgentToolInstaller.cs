using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using MicrohireAgentChat.Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed partial class AgentToolInstaller : IHostedService
{
    private readonly AIProjectClient _project;
    private readonly ILogger<AgentToolInstaller> _log;
    private readonly AzureAgentOptions _opts;
    private readonly IWebHostEnvironment _hostEnv;

    public AgentToolInstaller(
        AIProjectClient project,
        IOptions<AzureAgentOptions> opts,
        IWebHostEnvironment hostEnv,
        ILogger<AgentToolInstaller> log)
    {
        _project = project;
        _opts = opts.Value;
        _hostEnv = hostEnv;
        _log = log;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;


    private static object FunctionTool(string name, string description, object parametersSchema) =>
        new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters = parametersSchema
            }
        };
}
