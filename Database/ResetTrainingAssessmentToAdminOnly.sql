USE [TrainingAssessmentPortalDB]
GO

IF COL_LENGTH('Accounts', 'Username') IS NULL
BEGIN
    ALTER TABLE [Accounts] ADD [Username] VARCHAR(100) NULL;
END
GO

DELETE FROM [AssessmentSubmissions]
DELETE FROM [Attendance]
DELETE FROM [Assessments]
DELETE FROM [TrainingSessions]
DELETE FROM [FeedbackResponses]
DELETE FROM [FeedbackQuestions]
DELETE FROM [FeedbackForms]
DELETE FROM [Announcements]
DELETE FROM [BatchStudents]
DELETE FROM [Batches]
DELETE FROM [TrainingProgramTrainers]
DELETE FROM [TrainingPrograms]
DELETE FROM [Reports]
DELETE FROM [LoginHistory]
DELETE FROM [Students]
DELETE FROM [Trainers]
DELETE FROM [Accounts]
DELETE FROM [Roles]
GO

DBCC CHECKIDENT ('Students', RESEED, 0)
DBCC CHECKIDENT ('Trainers', RESEED, 0)
DBCC CHECKIDENT ('TrainingPrograms', RESEED, 0)
DBCC CHECKIDENT ('Batches', RESEED, 0)
DBCC CHECKIDENT ('FeedbackQuestions', RESEED, 0)
DBCC CHECKIDENT ('FeedbackResponses', RESEED, 0)
DBCC CHECKIDENT ('Attendance', RESEED, 0)
DBCC CHECKIDENT ('AssessmentSubmissions', RESEED, 0)
DBCC CHECKIDENT ('LoginHistory', RESEED, 0)
DBCC CHECKIDENT ('Reports', RESEED, 0)
DBCC CHECKIDENT ('Roles', RESEED, 0)
GO

INSERT INTO [Roles] ([RoleName]) VALUES ('Admin'), ('HOD'), ('Trainer'), ('Student')
GO

INSERT INTO [Accounts]
(
    [AccountId],
    [RoleId],
    [Username],
    [Name],
    [DepartmentOrBatch],
    [Email],
    [ContactNo],
    [Status],
    [PasswordHash],
    [LastLoginAt]
)
VALUES
(
    'ADM001',
    (SELECT [RoleId] FROM [Roles] WHERE [RoleName] = 'Admin'),
    'atmeceadmin',
    'Administrator',
    'Training Cell',
    'admin@atme.edu.in',
    '570028',
    'Active',
    'atmeceadmin@570028',
    NULL
)
GO

SELECT
    a.[AccountId],
    a.[Username],
    r.[RoleName],
    a.[Name],
    a.[Status]
FROM [Accounts] a
INNER JOIN [Roles] r ON r.[RoleId] = a.[RoleId]
GO


