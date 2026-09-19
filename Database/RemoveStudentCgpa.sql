USE [TrainingAssessmentPortalDB]
GO

IF COL_LENGTH('dbo.Students', 'CGPA') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Students] DROP COLUMN [CGPA];
END
GO
