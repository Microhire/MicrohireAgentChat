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

public sealed class LeadEmailOptions
{
    /// <summary>When <see cref="ChatBaseUrl"/> is empty: use <see cref="LocalChatBaseUrl"/> for lead emails (localhost dev).</summary>
    public bool LocalDevelopment { get; set; }

    /// <summary>Used for lead emails when <see cref="ChatBaseUrl"/> is empty and <see cref="LocalDevelopment"/> is true.</summary>
    public string? LocalChatBaseUrl { get; set; }

    /// <summary>Public chat URL for links in lead emails. When set, always wins (use even if the API request is localhost).</summary>
    public string? ChatBaseUrl { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
}
