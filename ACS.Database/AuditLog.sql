CREATE TABLE [dbo].[AuditLog] (
    [Id]            INT           IDENTITY (1, 1) NOT NULL,
    [EntityType]    VARCHAR (50)  NOT NULL,
    [EntityId]      INT           NOT NULL,
    [ChangeType]    VARCHAR (10)  NOT NULL,
    [ChangedBy]     VARCHAR (100) NOT NULL,
    [ChangeDate]    DATETIME      DEFAULT (getdate()) NOT NULL,
    [ChangeDetails] TEXT          NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [idx_AuditLog_ChangeDate]
    ON [dbo].[AuditLog]([ChangeDate] ASC);

