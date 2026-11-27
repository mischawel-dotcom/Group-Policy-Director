-- Group Policy Director — Slice 2 workflow columns (Approve / Deploy / Settings metadata)
-- Apply after 001_initial.sql; migrations tooling later.

IF COL_LENGTH('dbo.ControlledGpos', 'PendingVersionId') IS NULL
BEGIN
    ALTER TABLE dbo.ControlledGpos ADD PendingVersionId UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('dbo.ControlledGpos', 'ApprovedBy') IS NULL
BEGIN
    ALTER TABLE dbo.ControlledGpos ADD ApprovedBy NVARCHAR(256) NULL;
END
GO

IF COL_LENGTH('dbo.ControlledGpos', 'ApprovedAt') IS NULL
BEGIN
    ALTER TABLE dbo.ControlledGpos ADD ApprovedAt DATETIMEOFFSET NULL;
END
GO

IF COL_LENGTH('dbo.GpoVersions', 'WorkflowState') IS NULL
BEGIN
    ALTER TABLE dbo.GpoVersions ADD WorkflowState NVARCHAR(64) NULL; -- Draft | PendingApproval | Approved | Rejected | Deployed
END
GO

IF COL_LENGTH('dbo.GpoVersions', 'ReviewedBy') IS NULL
BEGIN
    ALTER TABLE dbo.GpoVersions ADD ReviewedBy NVARCHAR(256) NULL;
END
GO

IF COL_LENGTH('dbo.GpoVersions', 'ReviewComment') IS NULL
BEGIN
    ALTER TABLE dbo.GpoVersions ADD ReviewComment NVARCHAR(1024) NULL;
END
GO

IF OBJECT_ID(N'dbo.AdGroupMembershipStubs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdGroupMembershipStubs (
        UserPrincipal  NVARCHAR(512) NOT NULL,
        GroupPrincipal NVARCHAR(512) NOT NULL,
        CONSTRAINT PK_AdGroupMembershipStubs PRIMARY KEY (UserPrincipal, GroupPrincipal)
    );
END
GO
