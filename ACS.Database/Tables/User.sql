CREATE TABLE [dbo].[User] (
    [Id]       INT           NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [EntityId] INT           NULL,
    [RoleId]   INT           NULL,
    [GroupId]  INT           NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]),
    FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]),
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [idx_User_EntityId]
    ON [dbo].[User]([EntityId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_User_RoleId]
    ON [dbo].[User]([RoleId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_User_GroupId]
    ON [dbo].[User]([GroupId] ASC);

