IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AgentThreads') AND name = 'Email')
    ALTER TABLE dbo.AgentThreads ADD Email NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AgentThreads') AND name = 'DraftStateJson')
    ALTER TABLE dbo.AgentThreads ADD DraftStateJson NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AgentThreads') AND name = 'IX_AgentThreads_Email')
    CREATE NONCLUSTERED INDEX IX_AgentThreads_Email ON dbo.AgentThreads (Email) WHERE Email IS NOT NULL;
