CREATE TABLE [Inventories] (
    [Id] int NOT NULL IDENTITY,
    [ProductName] nvarchar(200) NOT NULL,
    [Stock] int NOT NULL,
    CONSTRAINT [PK_Inventories] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [CustomerName] nvarchar(200) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [PerformedBy] nvarchar(200) NOT NULL,
    [PerformedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLogs_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ProductName] nvarchar(200) NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_AuditLogs_OrderId] ON [AuditLogs] ([OrderId]);
GO


CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO


