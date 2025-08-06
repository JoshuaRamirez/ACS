CREATE TABLE [dbo].[PermissionScheme] (
    [Id]           INT NOT NULL,
    [EntityId]     INT NULL,
    [SchemeTypeId] INT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]),
    FOREIGN KEY ([SchemeTypeId]) REFERENCES [dbo].[SchemeType] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [idx_PermissionScheme_EntityId]
    ON [dbo].[PermissionScheme]([EntityId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_PermissionScheme_SchemeTypeId]
    ON [dbo].[PermissionScheme]([SchemeTypeId] ASC);

