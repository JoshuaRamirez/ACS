CREATE TABLE [dbo].[Role] (
    [Id]       INT           NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [GroupId]  INT           NULL,
    [EntityId] INT           NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id]),
    FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [idx_Role_EntityId]
    ON [dbo].[Role]([EntityId] ASC);


GO
CREATE NONCLUSTERED INDEX [idx_Role_GroupId]
    ON [dbo].[Role]([GroupId] ASC);

