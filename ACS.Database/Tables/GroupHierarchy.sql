CREATE TABLE [dbo].[GroupHierarchy] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [ParentGroupId] INT NOT NULL,
    [ChildGroupId] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] VARCHAR(100) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([ParentGroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE NO ACTION,
    FOREIGN KEY ([ChildGroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CHECK ([ParentGroupId] != [ChildGroupId])
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_GroupHierarchy_Parent_Child]
    ON [dbo].[GroupHierarchy]([ParentGroupId] ASC, [ChildGroupId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_GroupHierarchy_Child]
    ON [dbo].[GroupHierarchy]([ChildGroupId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_GroupHierarchy_CreatedAt]
    ON [dbo].[GroupHierarchy]([CreatedAt] ASC);