CREATE TABLE [dbo].[Group] (
    [Id]       INT           NOT NULL,
    [Name]     VARCHAR (100) NOT NULL,
    [EntityId] INT           NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entity] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [idx_Group_EntityId]
    ON [dbo].[Group]([EntityId] ASC);

