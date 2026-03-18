namespace MicrohireAgentChat.Config;

public sealed class AzureAgentOptions
{
    public string? Endpoint { get; set; }
    public string? AgentId { get; set; }
    public string? TenantId { get; set; }
}

public sealed class DevModeOptions
{
    public bool Enabled { get; set; }
}

public sealed class AutoTestOptions
{
    public const int DefaultMaxMessages = 30;
    public const int DefaultDelayBetweenMessagesMs = 3000;

    public int MaxMessages { get; set; } = DefaultMaxMessages;
    public int DelayBetweenMessagesMs { get; set; } = DefaultDelayBetweenMessagesMs;
    public bool Enabled { get; set; } = true;
}

public sealed class AzureOpenAIOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Deployment { get; set; }
}

public sealed class RentalPointDefaultsOptions
{
    public string Salesperson { get; set; } = "JEJ";
    public decimal OperatorId { get; set; } = 260;
}
