/*
    ATME Training and Assessment Portal
    SQL Server Database Creation Script
    Database Name: TrainingAssessmentPortalDB
*/

USE [master]
GO

IF DB_ID(N'TrainingAssessmentPortalDB') IS NOT NULL
BEGIN
    ALTER DATABASE [TrainingAssessmentPortalDB]
        SET SINGLE_USER
        WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TrainingAssessmentPortalDB];
END
GO

CREATE DATABASE [TrainingAssessmentPortalDB]
GO

USE [TrainingAssessmentPortalDB]
GO

CREATE TABLE [Roles]
(
    [RoleId] TINYINT IDENTITY(1,1) CONSTRAINT [PK_Roles] PRIMARY KEY,
    [RoleName] VARCHAR(30) CONSTRAINT [UQ_Roles_RoleName] UNIQUE NOT NULL
)
GO

CREATE TABLE [Accounts]
(
    [AccountId] VARCHAR(30) CONSTRAINT [PK_Accounts] PRIMARY KEY,
    [RoleId] TINYINT CONSTRAINT [FK_Accounts_Roles] REFERENCES [Roles]([RoleId]) NOT NULL,
    [Name] VARCHAR(150) NOT NULL,
    [DepartmentOrBatch] VARCHAR(200) NOT NULL,
    [Email] VARCHAR(256) CONSTRAINT [UQ_Accounts_Email] UNIQUE NOT NULL,
    [ContactNo] VARCHAR(20) NOT NULL,
    [Status] VARCHAR(20) CONSTRAINT [CK_Accounts_Status] CHECK ([Status] IN ('Active', 'Pending', 'Inactive')) NOT NULL DEFAULT 'Active',
    [PasswordHash] VARCHAR(500) NULL,
    [LastLoginAt] DATETIME2(0) NULL,
    [CreatedAt] DATETIME2(0) CONSTRAINT [DF_Accounts_CreatedAt] DEFAULT SYSUTCDATETIME() NOT NULL,
    [UpdatedAt] DATETIME2(0) NULL
)
GO

CREATE TABLE [Students]
(
    [StudentId] INT IDENTITY(1,1) CONSTRAINT [PK_Students] PRIMARY KEY,
    [AccountId] VARCHAR(30) CONSTRAINT [FK_Students_Accounts] REFERENCES [Accounts]([AccountId]) NULL,
    [USN] VARCHAR(30) CONSTRAINT [UQ_Students_USN] UNIQUE NOT NULL,
    [Name] VARCHAR(150) NOT NULL,
    [CurrentSemester] VARCHAR(20) NOT NULL,
    [EmailId] VARCHAR(256) NOT NULL,
    [ContactNo] VARCHAR(20) NOT NULL,
    [Status] VARCHAR(20) CONSTRAINT [CK_Students_Status] CHECK ([Status] IN ('Active', 'Detained', 'Discontinued')) NOT NULL DEFAULT 'Active'
)
GO

CREATE TABLE [Trainers]
(
    [TrainerId] INT IDENTITY(1,1) CONSTRAINT [PK_Trainers] PRIMARY KEY,
    [AccountId] VARCHAR(30) CONSTRAINT [FK_Trainers_Accounts] REFERENCES [Accounts]([AccountId]) NULL,
    [Name] VARCHAR(150) NOT NULL,
    [Qualification] VARCHAR(200) NOT NULL,
    [Designation] VARCHAR(150) NOT NULL,
    [TeachingExperience] VARCHAR(50) NOT NULL,
    [IndustryExperience] VARCHAR(50) NOT NULL,
    [TotalExperience] VARCHAR(50) NOT NULL,
    [EmailId] VARCHAR(256) NOT NULL,
    [ContactNo] VARCHAR(20) NOT NULL,
    [IsActive] BIT CONSTRAINT [DF_Trainers_IsActive] DEFAULT 1 NOT NULL
)
GO

CREATE TABLE [TrainingPrograms]
(
    [TrainingId] INT IDENTITY(1,1) CONSTRAINT [PK_TrainingPrograms] PRIMARY KEY,
    [TrainingType] VARCHAR(150) NOT NULL,
    [Objectives] NVARCHAR(1000) NOT NULL,
    [Outcome] NVARCHAR(1000) NOT NULL,
    [TargetAudience] VARCHAR(200) NOT NULL,
    [AcademicYear] VARCHAR(20) NOT NULL,
    [Duration] VARCHAR(100) NOT NULL,
    [Mode] VARCHAR(20) CONSTRAINT [CK_TrainingPrograms_Mode] CHECK ([Mode] IN ('Online', 'Offline', 'Hybrid')) NOT NULL,
    [StartDate] DATE NOT NULL,
    [EndDate] DATE NOT NULL,
    [Status] VARCHAR(20) CONSTRAINT [CK_TrainingPrograms_Status] CHECK ([Status] IN ('Active', 'Closed')) NOT NULL DEFAULT 'Active',
    [CreatedByAccountId] VARCHAR(30) NULL,
    [Department] VARCHAR(200) NULL
)
GO

CREATE TABLE [TrainingProgramTrainers]
(
    [TrainingId] INT CONSTRAINT [FK_TPT_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NOT NULL,
    [TrainerId] INT CONSTRAINT [FK_TPT_Trainers] REFERENCES [Trainers]([TrainerId]) NOT NULL,
    CONSTRAINT [PK_TrainingProgramTrainers] PRIMARY KEY ([TrainingId], [TrainerId])
)
GO

CREATE TABLE [Batches]
(
    [BatchId] INT IDENTITY(1,1) CONSTRAINT [PK_Batches] PRIMARY KEY,
    [BatchCode] VARCHAR(30) CONSTRAINT [UQ_Batches_BatchCode] UNIQUE NOT NULL,
    [BatchName] VARCHAR(100) NOT NULL,
    [TrainingId] INT CONSTRAINT [FK_Batches_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NOT NULL,
    [IsActive] BIT CONSTRAINT [DF_Batches_IsActive] DEFAULT 1 NOT NULL,
    [Department] VARCHAR(200) NULL
)
GO

CREATE TABLE [TrainingProgramBatches]
(
    [TrainingId] INT CONSTRAINT [FK_TPB_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) ON DELETE CASCADE NOT NULL,
    [BatchId] INT CONSTRAINT [FK_TPB_Batches] REFERENCES [Batches]([BatchId]) NOT NULL,
    CONSTRAINT [PK_TrainingProgramBatches] PRIMARY KEY ([TrainingId], [BatchId])
)
GO

CREATE TABLE [BatchStudents]
(
    [BatchId] INT CONSTRAINT [FK_BatchStudents_Batches] REFERENCES [Batches]([BatchId]) NOT NULL,
    [StudentId] INT CONSTRAINT [FK_BatchStudents_Students] REFERENCES [Students]([StudentId]) NOT NULL,
    CONSTRAINT [PK_BatchStudents] PRIMARY KEY ([BatchId], [StudentId])
)
GO

CREATE TABLE [Announcements]
(
    [AnnouncementId] VARCHAR(50) CONSTRAINT [PK_Announcements] PRIMARY KEY,
    [TrainingId] INT CONSTRAINT [FK_Announcements_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NULL,
    [Subject] VARCHAR(300) NOT NULL,
    [Message] NVARCHAR(2000) NOT NULL,
    [PublishedBy] VARCHAR(150) NOT NULL,
    [PublishedTo] VARCHAR(300) NOT NULL,
    [PublishedAt] DATETIME2(0) NOT NULL
)
GO

CREATE TABLE [FeedbackForms]
(
    [FeedbackId] VARCHAR(50) CONSTRAINT [PK_FeedbackForms] PRIMARY KEY,
    [TrainingId] INT CONSTRAINT [FK_FeedbackForms_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NOT NULL,
    [BatchId] INT CONSTRAINT [FK_FeedbackForms_Batches] REFERENCES [Batches]([BatchId]) NOT NULL,
    [SessionId] VARCHAR(30) NULL,
    [PublishedAt] DATETIME2(0) NOT NULL,
    [DeadlineAt] DATETIME2(0) NOT NULL,
    [Suggestions] NVARCHAR(1000) NULL
)
GO

CREATE TABLE [FeedbackQuestions]
(
    [QuestionId] INT IDENTITY(1,1) CONSTRAINT [PK_FeedbackQuestions] PRIMARY KEY,
    [FeedbackId] VARCHAR(50) CONSTRAINT [FK_FeedbackQuestions_FeedbackForms] REFERENCES [FeedbackForms]([FeedbackId]) NOT NULL,
    [QuestionNo] INT NOT NULL,
    [QuestionText] NVARCHAR(1000) NOT NULL
    ,[OptionsJson] NVARCHAR(MAX) NOT NULL DEFAULT('["Strongly Agree","Agree","Disagree","Strongly Disagree"]')
)
GO

CREATE TABLE [FeedbackResponses]
(
    [ResponseId] BIGINT IDENTITY(1,1) CONSTRAINT [PK_FeedbackResponses] PRIMARY KEY,
    [FeedbackId] VARCHAR(50) CONSTRAINT [FK_FeedbackResponses_FeedbackForms] REFERENCES [FeedbackForms]([FeedbackId]) NOT NULL,
    [StudentId] INT CONSTRAINT [FK_FeedbackResponses_Students] REFERENCES [Students]([StudentId]) NOT NULL,
    [QuestionNo] INT NOT NULL,
    [SelectedOption] VARCHAR(50) NOT NULL,
    [SubmittedAt] DATETIME2(0) NULL,
    [Attendance] VARCHAR(20) CONSTRAINT [CK_FeedbackResponses_Attendance] CHECK ([Attendance] IN ('Present', 'Absent')) NOT NULL DEFAULT 'Present',
    [IsConsidered] BIT NOT NULL DEFAULT 1
    ,[StudentSuggestions] NVARCHAR(1000) NULL
)
GO

CREATE TABLE [TrainingSessions]
(
    [SessionId] VARCHAR(30) CONSTRAINT [PK_TrainingSessions] PRIMARY KEY,
    [TrainingId] INT CONSTRAINT [FK_TrainingSessions_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NOT NULL,
    [SessionName] VARCHAR(200) NOT NULL,
    [SessionDate] DATE NOT NULL,
    [StartTime] TIME(0) NOT NULL,
    [EndTime] TIME(0) NOT NULL,
    [Venue] VARCHAR(150) NOT NULL,
    [Status] VARCHAR(20) CONSTRAINT [CK_TrainingSessions_Status] CHECK ([Status] IN ('Open', 'Closed')) NOT NULL DEFAULT 'Open',
    [BatchesJson] NVARCHAR(MAX) NULL,
    [AssessmentsJson] NVARCHAR(MAX) NULL,
    [BatchSchedulesJson] NVARCHAR(MAX) NULL,
    [BatchAccessControlJson] NVARCHAR(MAX) NULL
)
GO

CREATE TABLE [Assessments]
(
    [AssessmentId] VARCHAR(30) CONSTRAINT [PK_Assessments] PRIMARY KEY,
    [TrainingId] INT CONSTRAINT [FK_Assessments_TrainingPrograms] REFERENCES [TrainingPrograms]([TrainingId]) NOT NULL,
    [Title] VARCHAR(200) NOT NULL,
    [Description] NVARCHAR(1000) NOT NULL,
    [AssessmentType] VARCHAR(50) NOT NULL,
    [Status] VARCHAR(20) CONSTRAINT [CK_Assessments_Status] CHECK ([Status] IN ('Open', 'Closed')) NOT NULL DEFAULT 'Open',
    [MaxScore] VARCHAR(30) NULL,
    [PublishedAt] DATETIME2(0) NULL
)
GO

CREATE TABLE [Attendance]
(
    [AttendanceId] BIGINT IDENTITY(1,1) CONSTRAINT [PK_Attendance] PRIMARY KEY,
    [SessionId] VARCHAR(30) CONSTRAINT [FK_Attendance_TrainingSessions] REFERENCES [TrainingSessions]([SessionId]) NOT NULL,
    [StudentId] INT CONSTRAINT [FK_Attendance_Students] REFERENCES [Students]([StudentId]) NOT NULL,
    [IsPresent] BIT NOT NULL,
    [Remarks] VARCHAR(500) NULL,
    [RecordedAt] DATETIME2(0) NOT NULL
)
GO

CREATE TABLE [AssessmentSubmissions]
(
    [SubmissionId] BIGINT IDENTITY(1,1) CONSTRAINT [PK_AssessmentSubmissions] PRIMARY KEY,
    [AssessmentId] VARCHAR(30) CONSTRAINT [FK_AssessmentSubmissions_Assessments] REFERENCES [Assessments]([AssessmentId]) NOT NULL,
    [StudentId] INT CONSTRAINT [FK_AssessmentSubmissions_Students] REFERENCES [Students]([StudentId]) NOT NULL,
    [SubmissionStatus] VARCHAR(30) NOT NULL,
    [SubmittedAt] DATETIME2(0) NULL,
    [Score] VARCHAR(30) NULL
)
GO

CREATE TABLE [AssessmentBatchAccess]
(
    [AssessmentId] VARCHAR(30) NOT NULL CONSTRAINT [FK_AssessmentBatchAccess_Assessments] REFERENCES [Assessments]([AssessmentId]) ON DELETE CASCADE,
    [BatchId] INT NOT NULL CONSTRAINT [FK_AssessmentBatchAccess_Batches] REFERENCES [Batches]([BatchId]) ON DELETE CASCADE,
    [GrantedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_AssessmentBatchAccess_GrantedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_AssessmentBatchAccess] PRIMARY KEY ([AssessmentId], [BatchId])
)
GO

CREATE TABLE [LoginHistory]
(
    [LoginHistoryId] BIGINT IDENTITY(1,1) CONSTRAINT [PK_LoginHistory] PRIMARY KEY,
    [AccountId] VARCHAR(30) CONSTRAINT [FK_LoginHistory_Accounts] REFERENCES [Accounts]([AccountId]) NOT NULL,
    [LoggedInAt] DATETIME2(0) NOT NULL,
    [LoggedOutAt] DATETIME2(0) NULL,
    [StayOnPortalMinutes] INT NOT NULL
)
GO

CREATE TABLE [Reports]
(
    [ReportId] INT IDENTITY(1,1) CONSTRAINT [PK_Reports] PRIMARY KEY,
    [ReportTitle] VARCHAR(200) NOT NULL,
    [Description] NVARCHAR(1000) NOT NULL,
    [UpdatedAt] DATETIME2(0) NOT NULL
)
GO

CREATE TABLE [SessionNotes]
(
    [SessionId] NVARCHAR(30) NOT NULL CONSTRAINT [PK_SessionNotes] PRIMARY KEY,
    [ContentHtml] NVARCHAR(MAX) NOT NULL,
    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SessionNotes_UpdatedAt] DEFAULT SYSUTCDATETIME()
)
GO

CREATE TABLE [SessionQuizQuestions]
(
    [SessionQuizQuestionId] INT IDENTITY(1,1) CONSTRAINT [PK_SessionQuizQuestions] PRIMARY KEY,
    [SessionId] NVARCHAR(30) NOT NULL,
    [QuizType] NVARCHAR(30) NOT NULL,
    [DisplayOrder] INT NOT NULL,
    [QuestionLabel] NVARCHAR(100) NOT NULL,
    [QuestionText] NVARCHAR(MAX) NOT NULL,
    [OptionsJson] NVARCHAR(MAX) NOT NULL,
    [AnswerTimeSeconds] INT NOT NULL,
    [Marks] INT NOT NULL,
    [CorrectOptionIndex] INT NULL,
    [QuestionType] NVARCHAR(30) NOT NULL CONSTRAINT [DF_SessionQuizQuestions_QuestionType] DEFAULT 'Single Choice',
    [CorrectOptionIndexesJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_SessionQuizQuestions_CorrectOptionIndexesJson] DEFAULT '[]',
    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SessionQuizQuestions_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [UQ_SessionQuizQuestions] UNIQUE ([SessionId], [QuizType], [DisplayOrder])
)
GO

CREATE TABLE [SessionProgrammingExercises]
(
    [SessionProgrammingExerciseId] INT IDENTITY(1,1) CONSTRAINT [PK_SessionProgrammingExercises] PRIMARY KEY,
    [SessionId] NVARCHAR(30) NOT NULL,
    [ItemLabel] NVARCHAR(100) NOT NULL,
    [Question] NVARCHAR(MAX) NOT NULL,
    [TestCasesJson] NVARCHAR(MAX) NOT NULL,
    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SessionProgrammingExercises_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [UQ_SessionProgrammingExercises] UNIQUE ([SessionId], [ItemLabel])
)
GO

CREATE TABLE [ProgrammingExecutionSubmissions]
(
    [SubmissionId] BIGINT IDENTITY(1,1) CONSTRAINT [PK_ProgrammingExecutionSubmissions] PRIMARY KEY,
    [SessionId] NVARCHAR(30) NOT NULL,
    [ItemLabel] NVARCHAR(100) NOT NULL,
    [Language] NVARCHAR(20) NOT NULL,
    [SourceCode] NVARCHAR(MAX) NOT NULL,
    [TestCasesJson] NVARCHAR(MAX) NOT NULL,
    [ResultsJson] NVARCHAR(MAX) NOT NULL,
    [CompileError] NVARCHAR(MAX) NULL,
    [SubmittedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ProgrammingExecutionSubmissions_SubmittedAt] DEFAULT SYSUTCDATETIME()
)
GO

CREATE INDEX [IX_Accounts_RoleId] ON [Accounts]([RoleId])
GO
CREATE INDEX [IX_LoginHistory_AccountId_LoggedInAt] ON [LoginHistory]([AccountId], [LoggedInAt])
GO
CREATE INDEX [IX_FeedbackResponses_Feedback_Student] ON [FeedbackResponses]([FeedbackId], [StudentId])
GO

INSERT INTO [Roles] ([RoleName]) VALUES ('Admin'), ('HOD'), ('Trainer'), ('Student')
GO

INSERT INTO [Accounts] ([AccountId], [RoleId], [Name], [DepartmentOrBatch], [Email], [ContactNo], [Status], [LastLoginAt]) VALUES
('ADM001', 1, 'Administrator', 'Training Cell', 'admin@atme.edu.in', '9876500000', 'Active', '2026-07-28T09:00:00'),
('HOD001', 2, 'Dr. Puttegowda D', 'Computer Science and Engineering', 'hod.cse@atme.edu.in', '9876543210', 'Active', '2026-07-28T10:20:00'),
('TRN001', 3, 'Dr. Drakshayini K B', 'Training Cell', 'trainer.dsa@atme.edu.in', '9876501234', 'Active', '2026-07-27T16:45:00'),
('TRN002', 3, 'Prof. Theja N', 'Training Cell', 'thejan.cs@atme.edu.in', '9036989521', 'Active', '2026-07-27T15:20:00'),
('STD001', 4, 'Ananya R', 'CSE - 5th Semester', 'ananya.23cs001@atme.edu.in', '9876123450', 'Active', '2026-07-28T09:15:00'),
('STD002', 4, 'Karthik S', 'CSE - 5th Semester', 'karthik.23cs002@atme.edu.in', '9845123000', 'Pending', '2026-07-27T15:10:00'),
('STD003', 4, 'Rahul M', 'CSE - 5th Semester', 'rahul.23cs003@atme.edu.in', '9845123001', 'Active', '2026-07-26T14:05:00'),
('STD004', 4, 'Sneha K', 'CSE - 5th Semester', 'sneha.23cs004@atme.edu.in', '9845123002', 'Active', NULL),
('STD005', 4, 'Vikas N', 'CSE - 5th Semester', 'vikas.23cs005@atme.edu.in', '9845123003', 'Active', '2026-07-25T13:40:00')
GO

INSERT INTO [Students] ([AccountId], [USN], [Name], [CurrentSemester], [EmailId], [ContactNo], [Status]) VALUES
('STD001', '4AD23CS001', 'Ananya R', '5th', 'ananya.cs@atme.edu.in', '9876543210', 'Active'),
('STD002', '4AD23CS014', 'Karthik S', '5th', 'karthik.cs@atme.edu.in', '9876501234', 'Active'),
('STD003', '4AD23CS003', 'Rahul M', '5th', 'rahul.cs@atme.edu.in', '9876501235', 'Active'),
('STD004', '4AD23CS004', 'Sneha K', '5th', 'sneha.cs@atme.edu.in', '9876501236', 'Active'),
('STD005', '4AD23CS005', 'Vikas N', '5th', 'vikas.cs@atme.edu.in', '9876501237', 'Active'),
(NULL, '4AD23CS041', 'Nisha M', '6th', 'nisha.cs@atme.edu.in', '9876123450', 'Detained')
GO

INSERT INTO [Trainers] ([AccountId], [Name], [Qualification], [Designation], [TeachingExperience], [IndustryExperience], [TotalExperience], [EmailId], [ContactNo]) VALUES
('TRN001', 'Dr. Drakshayini K B', 'BE. M.Tech., Ph.D.', 'Associate Professor', '16 Years 6 Months', '1 Year 0 Months', '17 Years 6 Months', 'drakshayinikb.cs@atme.edu.in', '9008304136'),
('TRN002', 'Prof. Theja N', 'BE, M.Tech.', 'Assistant Professor', '10 Years 0 Months', '5 Years 4 Months', '15 Years 4 Months', 'thejan.cs@atme.edu.in', '9036989521')
GO

INSERT INTO [TrainingPrograms] ([TrainingType], [Objectives], [Outcome], [TargetAudience], [AcademicYear], [Duration], [Mode], [StartDate], [EndDate], [Status]) VALUES
('DSA Training', 'Strengthen core data structures, algorithms, and coding problem-solving skills.', 'Students solve placement-focused DSA problems with better speed and accuracy.', '3rd Year / 5th & 6th Semester', '2025-26', '15 Weeks / 15 Days / 30 Hours', 'Offline', '2025-08-01', '2025-09-30', 'Active')
GO

INSERT INTO [TrainingProgramTrainers] ([TrainingId], [TrainerId]) VALUES (1, 1), (1, 2)
GO

INSERT INTO [Batches] ([BatchCode], [BatchName], [TrainingId], [IsActive]) VALUES
('TAB-001', 'DSA-5A-B1', 1, 1),
('TAB-002', 'DSA-5A1', 1, 1),
('TAB-003', 'DSA-5A2', 1, 1),
('TAB-004', 'DSA-5B1', 1, 1),
('TAB-005', 'DSA-B2', 1, 1),
('TAB-006', 'DSA-C1', 1, 1),
('TAB-007', 'DSA-C2', 1, 1)
GO

INSERT INTO [TrainingProgramBatches] ([TrainingId], [BatchId])
SELECT [TrainingId], [BatchId] FROM [Batches]
GO

INSERT INTO [BatchStudents] ([BatchId], [StudentId]) VALUES (1,1), (1,2), (2,1), (2,2), (2,3)
GO

INSERT INTO [Announcements] ([AnnouncementId], [TrainingId], [Subject], [Message], [PublishedBy], [PublishedTo], [PublishedAt]) VALUES
('DSA_Announcement_1', 1, 'DSA Training Session Schedule Updated', 'Session notes and assessments for array fundamentals are now available in your portal.', 'Trainer - Dr. Drakshayini K B', 'DSA Training => DSA-5A-B1', '2026-07-25T10:30:00')
GO

INSERT INTO [FeedbackForms] ([FeedbackId], [TrainingId], [BatchId], [PublishedAt], [DeadlineAt], [Suggestions]) VALUES
('DSAF_1', 1, 1, '2026-07-25T09:00:00', '2026-07-25T17:00:00', 'Overall very insightful and interactive sessions.')
GO

INSERT INTO [FeedbackQuestions] ([FeedbackId], [QuestionNo], [QuestionText]) VALUES
('DSAF_1', 1, 'The session started and concluded on time.'),
('DSAF_1', 2, 'The trainer explained the concepts clearly.'),
('DSAF_1', 3, 'The hands-on activity was conducted at a steady pace for clear understanding.'),
('DSAF_1', 4, 'The trainer answered questions and queries effectively.'),
('DSAF_1', 5, 'Efforts were made to ensure all participants understood the concepts.'),
('DSAF_1', 6, 'The session was engaging and effective overall.'),
('DSAF_1', 7, 'The examples used were relevant and helped in understanding the topic.'),
('DSAF_1', 8, 'The session balanced theory and practice appropriately.'),
('DSAF_1', 9, 'The trainer encouraged participation and interaction.'),
('DSAF_1', 10, 'The learning objectives of the session were achieved.'),
('DSAF_1', 11, 'The session improved my confidence in applying the concepts.'),
('DSAF_1', 12, 'The materials/resources provided were useful and easy to follow.')
GO

INSERT INTO [FeedbackResponses] ([FeedbackId], [StudentId], [QuestionNo], [SelectedOption], [SubmittedAt], [Attendance], [IsConsidered])
SELECT 'DSAF_1', s.StudentId, q.QuestionNo,
       CASE WHEN s.StudentId IN (1,5) AND q.QuestionNo % 2 = 1 THEN 'Strongly Agree'
            WHEN s.StudentId IN (1,5) THEN 'Agree'
            WHEN s.StudentId = 2 AND q.QuestionNo % 3 = 0 THEN 'Strongly Agree'
            WHEN s.StudentId = 3 AND q.QuestionNo % 2 = 0 THEN 'Agree'
            WHEN s.StudentId = 3 THEN 'Disagree'
            ELSE '-' END,
       CASE WHEN s.StudentId IN (1,2,3,5) THEN DATEADD(MINUTE, s.StudentId * 35, '2026-07-25T09:40:00') ELSE NULL END,
       CASE WHEN s.StudentId = 3 THEN 'Absent' ELSE 'Present' END,
       CASE WHEN s.StudentId IN (1,2,5) THEN 1 ELSE 0 END
FROM [Students] s
CROSS JOIN [FeedbackQuestions] q
WHERE q.FeedbackId = 'DSAF_1' AND s.StudentId IN (1,2,3,4,5)
GO

INSERT INTO [TrainingSessions] ([SessionId], [TrainingId], [SessionName], [SessionDate], [StartTime], [EndTime], [Venue], [Status]) VALUES
('DSA-S1', 1, 'Array Fundamentals', '2026-07-24', '09:00', '10:30', 'CSE Seminar Hall', 'Open')
GO

INSERT INTO [Assessments] ([AssessmentId], [TrainingId], [Title], [Description], [AssessmentType], [Status], [MaxScore], [PublishedAt]) VALUES
('ASM-001', 1, 'Konnect Quiz', 'Quiz on array fundamentals.', 'Quiz', 'Open', '20', '2026-07-24T10:00:00'),
('ASM-002', 1, 'Programming Exercise-1', 'Solve placement-style array problems.', 'Programming Assignment', 'Open', 'Passed', '2026-07-24T10:15:00'),
('Assessment-1', 1, 'DSA_Pre-Assessment_Quiz', '50-question pre-assessment quiz for evaluating DSA readiness.', 'Quiz', 'Closed', '50 Marks', NULL),
('Assessment-2', 1, 'DSA Pre-assessment_Programming', 'Two programming questions: 15 minutes and 10 marks per question.', 'Programming Assignment', 'Closed', '20 Marks', NULL),
('Assessment-3', 1, 'DSA Descriptive Pre-assessment', 'Five descriptive questions carrying 6 marks each.', 'Descriptive Assignment', 'Closed', '30 Marks', NULL)
GO

INSERT INTO [Attendance] ([SessionId], [StudentId], [IsPresent], [Remarks], [RecordedAt]) VALUES
('DSA-S1', 1, 0, '', '2026-07-24T09:00:00'),
('DSA-S1', 2, 0, '', '2026-07-24T09:00:00')
GO

INSERT INTO [AssessmentSubmissions] ([AssessmentId], [StudentId], [SubmissionStatus], [SubmittedAt], [Score]) VALUES
('ASM-001', 1, 'Submitted', '2026-07-24T10:45:00', '18/20'),
('ASM-001', 2, 'Pending', NULL, '-'),
('ASM-002', 1, 'Submitted', '2026-07-24T11:40:00', 'Passed'),
('ASM-002', 2, 'Pending', NULL, '-')
GO

INSERT INTO [LoginHistory] ([AccountId], [LoggedInAt], [LoggedOutAt], [StayOnPortalMinutes]) VALUES
('HOD001', '2026-07-28T10:20:00', '2026-07-28T11:15:00', 55),
('TRN001', '2026-07-27T16:45:00', '2026-07-27T18:05:00', 80),
('STD001', '2026-07-28T09:15:00', '2026-07-28T09:52:00', 37),
('STD001', '2026-07-27T09:10:00', '2026-07-27T10:05:00', 55),
('STD002', '2026-07-27T15:10:00', '2026-07-27T15:44:00', 34),
('STD002', '2026-07-26T14:20:00', '2026-07-26T15:02:00', 42),
('STD003', '2026-07-26T14:05:00', '2026-07-26T14:42:00', 37)
GO

INSERT INTO [Reports] ([ReportTitle], [Description], [UpdatedAt]) VALUES
('Accounts Report', 'Role-wise HOD, trainer, and student account status.', '2026-07-28T10:30:00'),
('Training Report', 'Training-wise enrolment and batch status.', '2026-07-28T10:35:00'),
('Feedback Report', 'Feedback submissions and average response analysis.', '2026-07-28T10:40:00')
GO

CREATE VIEW [vw_LoginHistoryWithTotals]
AS
SELECT
    lh.[LoginHistoryId],
    r.[RoleName] AS [AccountType],
    a.[AccountId],
    a.[Name],
    lh.[LoggedInAt] AS [LastLoggedIn],
    lh.[LoggedOutAt] AS [LastLoggedOut],
    lh.[StayOnPortalMinutes],
    SUM(lh.[StayOnPortalMinutes]) OVER (PARTITION BY lh.[AccountId]) AS [TotalStayOnPortalMinutes]
FROM [LoginHistory] lh
INNER JOIN [Accounts] a ON a.[AccountId] = lh.[AccountId]
INNER JOIN [Roles] r ON r.[RoleId] = a.[RoleId]
GO

CREATE VIEW [vw_DashboardSummary]
AS
SELECT
    (SELECT COUNT(*) FROM [TrainingPrograms]) AS [TotalTrainings],
    (SELECT COUNT(*) FROM [TrainingPrograms] WHERE [Status] = 'Active') AS [ActiveTrainings],
    (SELECT COUNT(*) FROM [TrainingPrograms] WHERE [Status] = 'Closed') AS [ClosedTrainings],
    (SELECT COUNT(*) FROM [Students]) AS [TotalStudents],
    (SELECT COUNT(*) FROM [Students] WHERE [Status] = 'Active') AS [ActiveStudents],
    (SELECT COUNT(*) FROM [Trainers]) AS [TotalTrainers],
    (SELECT COUNT(*) FROM [Trainers] WHERE [IsActive] = 1) AS [ActiveTrainers],
    (SELECT COUNT(*) FROM [Batches]) AS [TotalBatches],
    (SELECT COUNT(*) FROM [Batches] WHERE [IsActive] = 1) AS [ActiveBatches],
    (SELECT COUNT(*) FROM [Reports]) AS [TotalReports],
    (SELECT COUNT(*) FROM [Announcements]) AS [TotalAnnouncements],
    (SELECT COUNT(*) FROM [FeedbackForms]) AS [TotalFeedbacks]
GO

-- Set default account passwords for initial plain-text fallback conversion on login
UPDATE [Accounts] SET [PasswordHash] = 'admin123' WHERE [RoleId] = 1 OR [AccountId] LIKE 'ADM%';
UPDATE [Accounts] SET [PasswordHash] = 'hod123' WHERE [RoleId] = 2 OR [AccountId] LIKE 'HOD%';
UPDATE [Accounts] SET [PasswordHash] = 'trainer123' WHERE [RoleId] = 3 OR [AccountId] LIKE 'TRN%';
UPDATE [Accounts] SET [PasswordHash] = 'student123' WHERE [RoleId] = 4 OR [AccountId] LIKE 'STD%';
GO



