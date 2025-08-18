CREATE TABLE [dbo].[UserGroup] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [GroupId] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] VARCHAR(100) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserGroup_UserId_GroupId]
    ON [dbo].[UserGroup]([UserId] ASC, [GroupId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_UserGroup_GroupId]
    ON [dbo].[UserGroup]([GroupId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_UserGroup_CreatedAt]
    ON [dbo].[UserGroup]([CreatedAt] ASC);