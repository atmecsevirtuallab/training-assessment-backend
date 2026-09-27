USE [TrainingAssessmentPortalDB]
GO

-- Reset default account passwords so legacy plain-text fallback converts them on first login
UPDATE Accounts SET PasswordHash = 'admin123' WHERE RoleId = 1 OR AccountId LIKE 'ADM%';
UPDATE Accounts SET PasswordHash = 'hod123' WHERE RoleId = 2 OR AccountId LIKE 'HOD%';
UPDATE Accounts SET PasswordHash = 'trainer123' WHERE RoleId = 3 OR AccountId LIKE 'TRN%';
UPDATE Accounts SET PasswordHash = 'student123' WHERE RoleId = 4 OR AccountId LIKE 'STD%';

GO
