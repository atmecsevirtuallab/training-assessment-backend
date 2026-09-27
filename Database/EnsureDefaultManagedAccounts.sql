USE [TrainingAssessmentPortalDB]
GO

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountId = 'HOD001')
    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
    VALUES ('HOD001', 2, 'Dr. Puttegowda D', 'Computer Science and Engineering', 'hod.cse@atme.edu.in', '9876543210', 'Active', NULL, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountId = 'TRN001')
    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
    VALUES ('TRN001', 3, 'Dr. Drakshayini K B', 'Training Cell', 'trainer.dsa@atme.edu.in', '9876501234', 'Active', NULL, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Trainers WHERE AccountId = 'TRN001')
    INSERT INTO Trainers (AccountId, Name, Qualification, Designation, TeachingExperience, IndustryExperience, TotalExperience, EmailId, ContactNo, IsActive)
    VALUES ('TRN001', 'Dr. Drakshayini K B', 'BE, M.Tech., Ph.D.', 'Associate Professor', '16 Years 6 Months', '1 Year', '17 Years 6 Months', 'trainer.dsa@atme.edu.in', '9876501234', 1);

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountId = 'STD001')
    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
    VALUES ('STD001', 4, 'Ananya R', 'CSE - 5th Semester', 'ananya.23cs001@atme.edu.in', '9876123450', 'Active', NULL, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Students WHERE AccountId = 'STD001')
    INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
    VALUES ('STD001', '4AD23CS001', 'Ananya R', '5th', 'ananya.23cs001@atme.edu.in', '9876123450', 'Active');

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountId = 'STD002')
    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
    VALUES ('STD002', 4, 'Karthik S', 'CSE - 5th Semester', 'karthik.23cs002@atme.edu.in', '9845123000', 'Pending', NULL, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Students WHERE AccountId = 'STD002')
    INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
    VALUES ('STD002', '4AD23CS014', 'Karthik S', '5th', 'karthik.23cs002@atme.edu.in', '9845123000', 'Active');

-- Set default account passwords for initial plain-text fallback conversion on login
UPDATE Accounts SET PasswordHash = 'admin123' WHERE RoleId = 1 OR AccountId LIKE 'ADM%';
UPDATE Accounts SET PasswordHash = 'hod123' WHERE RoleId = 2 OR AccountId LIKE 'HOD%';
UPDATE Accounts SET PasswordHash = 'trainer123' WHERE RoleId = 3 OR AccountId LIKE 'TRN%';
UPDATE Accounts SET PasswordHash = 'student123' WHERE RoleId = 4 OR AccountId LIKE 'STD%';
GO

