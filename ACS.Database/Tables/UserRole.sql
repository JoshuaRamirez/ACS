CREATE TABLE [dbo].[UserRole] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] VARCHAR(100) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([Id]) ON DELETE CASCADE
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserRole_UserId_RoleId]
    ON [dbo].[UserRole]([UserId] ASC, [RoleId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_UserRole_RoleId]
    ON [dbo].[UserRole]([RoleId] ASC);

GO
CREATE NONCLUSTERED INDEX [idx_UserRole_CreatedAt]
    ON [dbo].[UserRole]([CreatedAt] ASC);