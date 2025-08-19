CREATE TABLE [dbo].[User] (
    [Id]                    INT           IDENTITY(1,1) NOT NULL,
    [Name]                  VARCHAR (100) NOT NULL,
    [Email]                 VARCHAR (256) NOT NULL,
    [PasswordHash]          VARCHAR (500) NOT NULL,
    [Salt]                  VARCHAR (100) NULL,
    [LastLoginAt]           DATETIME2     NULL,
    [FailedLoginAttempts]   INT           NOT NULL DEFAULT 0,
    [LockedOutUntil]        DATETIME2     NULL,
    [IsActive]              BIT           NOT NULL DEFAULT 1,
    [EntityId]              INT           NOT NULL,
    [CreatedAt]             DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]             DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_User_Email] UNIQUE ([Email]),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [idx_User_EntityId]
    ON [dbo].[User]([EntityId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_Name]
    ON [dbo].[User]([Name] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_Email]
    ON [dbo].[User]([Email] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_IsActive]
    ON [dbo].[User]([IsActive] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_CreatedAt]
    ON [dbo].[User]([CreatedAt] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_LastLoginAt]
    ON [dbo].[User]([LastLoginAt] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_LockedOutUntil]
    ON [dbo].[User]([LockedOutUntil] ASC) WHERE [LockedOutUntil] IS NOT NULL;

