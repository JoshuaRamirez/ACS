CREATE TABLE [dbo].[Role] (
    [Id]       INT           IDENTITY(1,1) NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [EntityId] INT           NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [idx_Role_EntityId]
    ON [dbo].[Role]([EntityId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_Role_Name]
    ON [dbo].[Role]([Name] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_Role_CreatedAt]
    ON [dbo].[Role]([CreatedAt] ASC);

