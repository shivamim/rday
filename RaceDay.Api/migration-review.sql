IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Roles] (
    [RoleId] int NOT NULL IDENTITY,
    [RoleName] varchar(20) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
);
GO

CREATE TABLE [Users] (
    [UserId] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [FirstName] varchar(50) NOT NULL,
    [LastName] varchar(50) NOT NULL,
    [Email] varchar(150) NOT NULL,
    [PasswordHash] varchar(255) NOT NULL,
    [PhoneNumber] varchar(20) NULL,
    [ProfilePictureUrl] varchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId])
);
GO

CREATE TABLE [Events] (
    [EventId] int NOT NULL IDENTITY,
    [OrganiserId] int NOT NULL,
    [EventName] varchar(150) NOT NULL,
    [Description] varchar(1000) NOT NULL,
    [EventDate] date NOT NULL,
    [Location] varchar(200) NOT NULL,
    [DistanceKm] decimal(6,2) NOT NULL,
    [EventType] varchar(10) NOT NULL,
    [BannerImageUrl] varchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Events] PRIMARY KEY ([EventId]),
    CONSTRAINT [CK_Events_DistanceKm] CHECK ([DistanceKm] > 0),
    CONSTRAINT [CK_Events_EventType] CHECK ([EventType] IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT [FK_Events_Organiser] FOREIGN KEY ([OrganiserId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [Categories] (
    [CategoryId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [CategoryName] varchar(100) NOT NULL,
    [CategoryType] varchar(20) NOT NULL,
    [MinAge] int NULL,
    [MaxAge] int NULL,
    [CategoryDistanceKm] decimal(6,2) NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId]),
    CONSTRAINT [CK_Categories_AgeRange] CHECK ((([MinAge] IS NULL AND [MaxAge] IS NULL) OR ([MinAge] IS NOT NULL AND [MaxAge] IS NOT NULL AND [MinAge] >= 0 AND [MaxAge] >= [MinAge]))),
    CONSTRAINT [CK_Categories_CategoryType] CHECK ([CategoryType] IN ('Age', 'Distance')),
    CONSTRAINT [CK_Categories_Distance] CHECK ([CategoryDistanceKm] IS NULL OR [CategoryDistanceKm] > 0),
    CONSTRAINT [FK_Categories_Events] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId])
);
GO

CREATE TABLE [Enrolments] (
    [EnrolmentId] int NOT NULL IDENTITY,
    [ParticipantId] int NOT NULL,
    [EventId] int NOT NULL,
    [CategoryId] int NOT NULL,
    [EnrolmentStatus] varchar(20) NOT NULL DEFAULT 'Pending',
    [EnrolledAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Enrolments] PRIMARY KEY ([EnrolmentId]),
    CONSTRAINT [CK_Enrolments_Status] CHECK (([EnrolmentStatus] = 'Confirmed' OR [EnrolmentStatus] = 'Pending')),
    CONSTRAINT [FK_Enrolments_Category] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId]),
    CONSTRAINT [FK_Enrolments_Event] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]),
    CONSTRAINT [FK_Enrolments_Participant] FOREIGN KEY ([ParticipantId]) REFERENCES [Users] ([UserId])
);
GO

CREATE TABLE [Results] (
    [ResultId] int NOT NULL IDENTITY,
    [EnrolmentId] int NOT NULL,
    [FinishPosition] int NOT NULL,
    [FinishTime] time NOT NULL,
    [RecordedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Results] PRIMARY KEY ([ResultId]),
    CONSTRAINT [CK_Results_FinishPosition] CHECK ([FinishPosition] > 0),
    CONSTRAINT [FK_Results_Enrolment] FOREIGN KEY ([EnrolmentId]) REFERENCES [Enrolments] ([EnrolmentId])
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'RoleName') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] ON;
INSERT INTO [Roles] ([RoleId], [RoleName])
VALUES (1, 'Organiser'),
(2, 'Participant');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'RoleName') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] OFF;
GO

CREATE UNIQUE INDEX [UQ_Categories_Event_CategoryName] ON [Categories] ([EventId], [CategoryName]);
GO

CREATE INDEX [IX_Enrolments_CategoryId] ON [Enrolments] ([CategoryId]);
GO

CREATE INDEX [IX_Enrolments_EventId] ON [Enrolments] ([EventId]);
GO

CREATE UNIQUE INDEX [UQ_Enrolments_Participant_Event] ON [Enrolments] ([ParticipantId], [EventId]);
GO

CREATE INDEX [IX_Events_OrganiserId] ON [Events] ([OrganiserId]);
GO

CREATE UNIQUE INDEX [UQ_Events_EventName] ON [Events] ([EventName]);
GO

CREATE UNIQUE INDEX [UQ_Results_Enrolment] ON [Results] ([EnrolmentId]);
GO

CREATE UNIQUE INDEX [UQ_Roles_RoleName] ON [Roles] ([RoleName]);
GO

CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
GO

CREATE UNIQUE INDEX [UQ_Users_Email] ON [Users] ([Email]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261018055000_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

