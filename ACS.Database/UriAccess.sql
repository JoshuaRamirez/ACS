CREATE TABLE [dbo].[UriAccess] (
    [Id]                 INT NOT NULL,
    [ResourceId]         INT NULL,
    [VerbTypeId]         INT NULL,
    [PermissionSchemeId] INT NULL,
    [Grant]              BIT NULL,
    [Deny]               BIT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [chk_Grant_Deny] CHECK ([Grant]=(1) AND [Deny]=(0) OR [Grant]=(0) AND [Deny]=(1)),
    FOREIGN KEY ([PermissionSchemeId]) REFERENCES [dbo].[PermissionScheme] ([Id]),
    FOREIGN KEY ([ResourceId]) REFERENCES [dbo].[Resource] ([Id]),
    FOREIGN KEY ([VerbTypeId]) REFERENCES [dbo].[VerbType] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [idx_UriAccess_ResourceId]
    ON [dbo].[UriAccess]([ResourceId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_UriAccess_VerbTypeId]
    ON [dbo].[UriAccess]([VerbTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_UriAccess_PermissionSchemeId]
    ON [dbo].[UriAccess]([PermissionSchemeId] ASC);

