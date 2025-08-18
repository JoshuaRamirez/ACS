CREATE TABLE [dbo].[Group] (
    [Id]       INT           IDENTITY(1,1) NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [EntityId] INT           NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [idx_Group_EntityId]
    ON [dbo].[Group]([EntityId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_Group_Name]
    ON [dbo].[Group]([Name] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_Group_CreatedAt]
    ON [dbo].[Group]([CreatedAt] ASC);

