CREATE TABLE [dbo].[GroupRole] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [GroupId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] VARCHAR(100) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([Id]) ON DELETE CASCADE
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_GroupRole_GroupId_RoleId]
    ON [dbo].[GroupRole]([GroupId] ASC, [RoleId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_GroupRole_RoleId]
    ON [dbo].[GroupRole]([RoleId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_GroupRole_CreatedAt]
    ON [dbo].[GroupRole]([CreatedAt] ASC);