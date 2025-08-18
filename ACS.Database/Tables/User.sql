CREATE TABLE [dbo].[User] (
    [Id]       INT           IDENTITY(1,1) NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [EntityId] INT           NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [idx_User_EntityId]
    ON [dbo].[User]([EntityId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_Name]
    ON [dbo].[User]([Name] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_User_CreatedAt]
    ON [dbo].[User]([CreatedAt] ASC);

