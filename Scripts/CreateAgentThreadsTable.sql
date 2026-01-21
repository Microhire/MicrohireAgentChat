-- Create AgentThreads table for persisting conversation threads
-- This allows conversations to resume after session loss, app restarts, or Azure scaling

IF OBJECT_ID('dbo.AgentThreads', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgentThreads (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserKey NVARCHAR(200) NOT NULL,
        ThreadId NVARCHAR(200) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        LastSeenUtc DATETIME2 NOT NULL
    );
    
    CREATE UNIQUE INDEX IX_AgentThreads_UserKey ON dbo.AgentThreads(UserKey);
    CREATE UNIQUE INDEX IX_AgentThreads_ThreadId ON dbo.AgentThreads(ThreadId);
    
    PRINT 'AgentThreads table created successfully.';
END
ELSE
BEGIN
    PRINT 'AgentThreads table already exists.';
END
