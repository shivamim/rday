/*
    RaceDay Database Script
    Part 1 - SQL Server

 
*/

SET NOCOUNT ON;
GO

-- Create the RaceDay database (only if it doesn't already exist)
IF DB_ID('RaceDay') IS NULL
BEGIN
    CREATE DATABASE RaceDay;
END
GO

USE RaceDay;
GO

-- Drop tables if they already exist, in FK-safe (child-to-parent) order,
-- so this script can be run again on top of a previous run without erroring.
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

/* 1. ROLES*/

CREATE TABLE dbo.Roles
(
    RoleId INT IDENTITY(1,1) NOT NULL,
    RoleName VARCHAR(20) NOT NULL,

    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

/* 2. USERS*/

CREATE TABLE dbo.Users
(
    UserId INT IDENTITY(1,1) NOT NULL,
    RoleId INT NOT NULL,
    FirstName VARCHAR(50) NOT NULL,
    LastName VARCHAR(50) NOT NULL,
    Email VARCHAR(150) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    ProfilePictureUrl VARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT FK_Users_Roles
        FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

/* 3. EVENTS */

CREATE TABLE dbo.Events
(
    EventId INT IDENTITY(1,1) NOT NULL,
    OrganiserId INT NOT NULL,
    EventName VARCHAR(150) NOT NULL,
    Description VARCHAR(1000) NOT NULL,
    EventDate DATE NOT NULL,
    Location VARCHAR(200) NOT NULL,
    DistanceKm DECIMAL(6,2) NOT NULL,
    EventType VARCHAR(10) NOT NULL,
    BannerImageUrl VARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Events_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Organiser
        FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_Events_EventName UNIQUE (EventName),
    CONSTRAINT CK_Events_DistanceKm
        CHECK (DistanceKm > 0),
    CONSTRAINT CK_Events_EventType
        CHECK (EventType IN ('Run', 'Walk', 'Cycle'))
);
GO

/* 4. CATEGORIES*/

CREATE TABLE dbo.Categories
(
    CategoryId INT IDENTITY(1,1) NOT NULL,
    EventId INT NOT NULL,
    CategoryName VARCHAR(100) NOT NULL,
    CategoryType VARCHAR(20) NOT NULL,
    MinAge INT NULL,
    MaxAge INT NULL,
    CategoryDistanceKm DECIMAL(6,2) NULL,

    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Events
        FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId),
    CONSTRAINT UQ_Categories_Event_CategoryName
        UNIQUE (EventId, CategoryName),
    CONSTRAINT CK_Categories_CategoryType
        CHECK (CategoryType IN ('Age', 'Distance')),
    CONSTRAINT CK_Categories_AgeRange
        CHECK (
            (MinAge IS NULL AND MaxAge IS NULL)
            OR
            (MinAge IS NOT NULL AND MaxAge IS NOT NULL
             AND MinAge >= 0 AND MaxAge >= MinAge)
        ),
    CONSTRAINT CK_Categories_Distance
        CHECK (
            CategoryDistanceKm IS NULL
            OR CategoryDistanceKm > 0
        )
);
GO

/* 5. ENROLMENTS */

CREATE TABLE dbo.Enrolments
(
    EnrolmentId INT IDENTITY(1,1) NOT NULL,
    ParticipantId INT NOT NULL,
    EventId INT NOT NULL,
    CategoryId INT NOT NULL,
    EnrolmentStatus VARCHAR(20) NOT NULL
        CONSTRAINT DF_Enrolments_Status DEFAULT 'Pending',
    EnrolledAt DATETIME2 NOT NULL
        CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Participant
        FOREIGN KEY (ParticipantId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Event
        FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId),
    CONSTRAINT FK_Enrolments_Category
        FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Event
        UNIQUE (ParticipantId, EventId),
    CONSTRAINT CK_Enrolments_Status
        CHECK (EnrolmentStatus IN ('Pending', 'Confirmed'))
);
GO

/*   6. RESULTS */

CREATE TABLE dbo.Results
(
    ResultId INT IDENTITY(1,1) NOT NULL,
    EnrolmentId INT NOT NULL,
    FinishPosition INT NOT NULL,
    FinishTime TIME NOT NULL,
    RecordedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Results_RecordedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT FK_Results_Enrolment
        FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments(EnrolmentId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId),
    CONSTRAINT CK_Results_FinishPosition
        CHECK (FinishPosition > 0)
);
GO

/*   SAMPLE DATA */

/* Roles */
INSERT INTO dbo.Roles (RoleName)
VALUES
    ('Organiser'),
    ('Participant');
GO

/* Users
   PasswordHash values below are sample placeholders for Part 1
   database demonstration data. Part 2 will use a proper password
   hashing implementation in the API.
*/
INSERT INTO dbo.Users
(
    RoleId,
    FirstName,
    LastName,
    Email,
    PasswordHash,
    PhoneNumber,
    ProfilePictureUrl
)
VALUES
(
    (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Organiser'),
    'Thabo',
    'Mokoena',
    'thabo.organiser@raceday.co.za',
    'PART1_SAMPLE_HASH_001',
    '+27821234567',
    NULL
),
(
    (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Organiser'),
    'Naledi',
    'Dlamini',
    'naledi.organiser@raceday.co.za',
    'PART1_SAMPLE_HASH_002',
    '+27829876543',
    NULL
),
(
    (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Participant'),
    'Liam',
    'Jacobs',
    'liam.participant@example.com',
    'PART1_SAMPLE_HASH_003',
    '+27825550101',
    NULL
),
(
    (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Participant'),
    'Ayesha',
    'Naidoo',
    'ayesha.participant@example.com',
    'PART1_SAMPLE_HASH_004',
    '+27825550102',
    NULL
);
GO

/* Events */
INSERT INTO dbo.Events
(
    OrganiserId,
    EventName,
    Description,
    EventDate,
    Location,
    DistanceKm,
    EventType,
    BannerImageUrl
)
VALUES
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'thabo.organiser@raceday.co.za'),
    'Cape Town Spring Run',
    'A road running event for participants of different age groups.',
    '2026-08-30',
    'Cape Town, Western Cape',
    10.00,
    'Run',
    NULL
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'thabo.organiser@raceday.co.za'),
    'Durban Coastal Walk',
    'A coastal walking event open to registered participants.',
    '2026-10-18',
    'Durban, KwaZulu-Natal',
    8.00,
    'Walk',
    NULL
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'naledi.organiser@raceday.co.za'),
    'Johannesburg City Cycle',
    'A city cycling event with distance-based categories.',
    '2026-11-15',
    'Johannesburg, Gauteng',
    25.00,
    'Cycle',
    NULL
);
GO

/* Categories for Event 1 */
INSERT INTO dbo.Categories
(
    EventId,
    CategoryName,
    CategoryType,
    MinAge,
    MaxAge,
    CategoryDistanceKm
)
VALUES
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run'),
    'Junior',
    'Age',
    16,
    20,
    NULL
),
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run'),
    'Open',
    'Age',
    21,
    39,
    NULL
),
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run'),
    'Masters',
    'Age',
    40,
    99,
    NULL
);
GO

/* Categories for Event 2 */
INSERT INTO dbo.Categories
(
    EventId,
    CategoryName,
    CategoryType,
    MinAge,
    MaxAge,
    CategoryDistanceKm
)
VALUES
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Durban Coastal Walk'),
    '5 KM Walk',
    'Distance',
    NULL,
    NULL,
    5.00
),
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Durban Coastal Walk'),
    '8 KM Walk',
    'Distance',
    NULL,
    NULL,
    8.00
);
GO

/* Categories for Event 3 */
INSERT INTO dbo.Categories
(
    EventId,
    CategoryName,
    CategoryType,
    MinAge,
    MaxAge,
    CategoryDistanceKm
)
VALUES
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Johannesburg City Cycle'),
    '15 KM Cycle',
    'Distance',
    NULL,
    NULL,
    15.00
),
(
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Johannesburg City Cycle'),
    '25 KM Cycle',
    'Distance',
    NULL,
    NULL,
    25.00
);
GO

/* Sample enrolments */
INSERT INTO dbo.Enrolments
(
    ParticipantId,
    EventId,
    CategoryId,
    EnrolmentStatus
)
VALUES
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'liam.participant@example.com'),
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run'),
    (SELECT CategoryId FROM dbo.Categories
     WHERE CategoryName = 'Open'
       AND EventId = (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run')),
    'Confirmed'
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'ayesha.participant@example.com'),
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run'),
    (SELECT CategoryId FROM dbo.Categories
     WHERE CategoryName = 'Masters'
       AND EventId = (SELECT EventId FROM dbo.Events WHERE EventName = 'Cape Town Spring Run')),
    'Confirmed'
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'liam.participant@example.com'),
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Durban Coastal Walk'),
    (SELECT CategoryId FROM dbo.Categories
     WHERE CategoryName = '8 KM Walk'
       AND EventId = (SELECT EventId FROM dbo.Events WHERE EventName = 'Durban Coastal Walk')),
    'Pending'
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'ayesha.participant@example.com'),
    (SELECT EventId FROM dbo.Events WHERE EventName = 'Johannesburg City Cycle'),
    (SELECT CategoryId FROM dbo.Categories
     WHERE CategoryName = '25 KM Cycle'
       AND EventId = (SELECT EventId FROM dbo.Events WHERE EventName = 'Johannesburg City Cycle')),
    'Confirmed'
);
GO

/* Sample result for the completed Cape Town event */
INSERT INTO dbo.Results
(
    EnrolmentId,
    FinishPosition,
    FinishTime
)
VALUES
(
    (
        SELECT e.EnrolmentId
        FROM dbo.Enrolments e
        INNER JOIN dbo.Users u
            ON e.ParticipantId = u.UserId
        INNER JOIN dbo.Events ev
            ON e.EventId = ev.EventId
        WHERE u.Email = 'liam.participant@example.com'
          AND ev.EventName = 'Cape Town Spring Run'
    ),
    47,
    '01:12:34'
);
GO


 --  VERIFICATION QUERIES


SELECT * FROM dbo.Roles;
SELECT * FROM dbo.Users;
SELECT * FROM dbo.Events;
SELECT * FROM dbo.Categories;
SELECT * FROM dbo.Enrolments;
SELECT * FROM dbo.Results;
GO

/* Relationship verification */
SELECT
    ev.EventName,
    u.FirstName + ' ' + u.LastName AS Participant,
    c.CategoryName,
    e.EnrolmentStatus,
    e.EnrolledAt
FROM dbo.Enrolments e
INNER JOIN dbo.Users u
    ON e.ParticipantId = u.UserId
INNER JOIN dbo.Events ev
    ON e.EventId = ev.EventId
INNER JOIN dbo.Categories c
    ON e.CategoryId = c.CategoryId
ORDER BY ev.EventDate, Participant;
GO

/* Result verification */
SELECT
    ev.EventName,
    u.FirstName + ' ' + u.LastName AS Participant,
    c.CategoryName,
    r.FinishPosition,
    r.FinishTime,
    r.RecordedAt
FROM dbo.Results r
INNER JOIN dbo.Enrolments e
    ON r.EnrolmentId = e.EnrolmentId
INNER JOIN dbo.Users u
    ON e.ParticipantId = u.UserId
INNER JOIN dbo.Events ev
    ON e.EventId = ev.EventId
INNER JOIN dbo.Categories c
    ON e.CategoryId = c.CategoryId
ORDER BY r.FinishPosition;
GO
