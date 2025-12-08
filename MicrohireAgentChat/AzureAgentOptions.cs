namespace MicrohireAgentChat.Config;

public sealed class AzureAgentOptions
{
    public string? Endpoint { get; set; }
    public string? AgentId { get; set; }
}

public sealed class DevModeOptions
{
    public bool Enabled { get; set; }
}
