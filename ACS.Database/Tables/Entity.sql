CREATE TABLE [dbo].[Entity] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [EntityType] VARCHAR(50) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CHECK ([EntityType] IN ('User', 'Group', 'Role'))
);

GO
CREATE NONCLUSTERED INDEX [idx_Entity_EntityType]
    ON [dbo].[Entity]([EntityType] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_Entity_CreatedAt]
    ON [dbo].[Entity]([CreatedAt] ASC);

