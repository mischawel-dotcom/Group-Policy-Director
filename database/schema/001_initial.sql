-- Group Policy Director — Slice 1 schema stubs (SQL Server 2019+)
-- Apply with migrations tooling later; this file documents the intended model.

IF OBJECT_ID(N'dbo.Domains', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Domains (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DnsName         NVARCHAR(255)    NOT NULL,
        NetBiosName     NVARCHAR(64)     NOT NULL,
        RegisteredAt    DATETIMEOFFSET   NOT NULL,
        RegisteredBy    NVARCHAR(256)    NULL,
        IsActive        BIT              NOT NULL CONSTRAINT DF_Domains_IsActive DEFAULT (1),
        CONSTRAINT UQ_Domains_DnsName UNIQUE (DnsName)
    );
END
GO

IF OBJECT_ID(N'dbo.ControlledGpos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ControlledGpos (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DomainId          UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Domains(Id),
        ProductionGpoId   NVARCHAR(64)     NOT NULL,
        DisplayName       NVARCHAR(512)    NOT NULL,
        TempGpoId         NVARCHAR(64)     NULL,
        Status            INT              NOT NULL,
        CheckedOutBy      NVARCHAR(256)    NULL,
        CheckedOutAt      DATETIMEOFFSET   NULL,
        SettingsHash      NVARCHAR(128)    NULL,
        TakenControlAt    DATETIMEOFFSET   NOT NULL,
        TakenControlBy    NVARCHAR(256)    NULL,
        CONSTRAINT UQ_ControlledGpos_Production UNIQUE (ProductionGpoId)
    );
END
GO

IF OBJECT_ID(N'dbo.GpoVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GpoVersions (
        Id                   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ControlledGpoId      UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ControlledGpos(Id),
        VersionNumber        INT              NOT NULL,
        Comment              NVARCHAR(1024)   NOT NULL,
        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedAt            DATETIMEOFFSET   NOT NULL,
        SettingsSnapshotJson NVARCHAR(MAX)    NULL,
        SettingsHash         NVARCHAR(128)    NULL,
        Provenance           NVARCHAR(64)     NULL, -- GPD | AGPM
        CONSTRAINT UQ_GpoVersions_Number UNIQUE (ControlledGpoId, VersionNumber)
    );
END
GO

IF OBJECT_ID(N'dbo.ProductRoleAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductRoleAssignments (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Role             INT              NOT NULL, -- ProductAdministrator, GpoCreator, BreakGlass, Auditor
        TrusteePrincipal NVARCHAR(512)    NOT NULL, -- AD group preferred
        GrantedBy        NVARCHAR(256)    NOT NULL,
        GrantedAt        DATETIMEOFFSET   NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.DelegationGrants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegationGrants (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ScopeKind        INT              NOT NULL, -- Ou | Gpo
        ScopeKey         NVARCHAR(512)    NOT NULL, -- OU DN or GPO GUID
        Role             INT              NOT NULL, -- Editor | Approver | Deployer
        TrusteePrincipal NVARCHAR(512)    NOT NULL, -- AD admin role group
        InheritToChildren BIT             NOT NULL CONSTRAINT DF_Delegation_Inherit DEFAULT (1),
        GrantedBy        NVARCHAR(256)    NOT NULL,
        GrantedAt        DATETIMEOFFSET   NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEvents (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        OccurredAt  DATETIMEOFFSET   NOT NULL,
        Actor       NVARCHAR(256)    NOT NULL,
        Action      NVARCHAR(128)    NOT NULL,
        ObjectKey   NVARCHAR(512)    NULL,
        Outcome     NVARCHAR(64)     NULL,
        DetailJson  NVARCHAR(MAX)    NULL
    );
    CREATE INDEX IX_AuditEvents_OccurredAt ON dbo.AuditEvents(OccurredAt DESC);
END
GO

IF OBJECT_ID(N'dbo.OobDetections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OobDetections (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ControlledGpoId     UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ControlledGpos(Id),
        DetectedAt          DATETIMEOFFSET   NOT NULL,
        ProductionHash      NVARCHAR(128)    NOT NULL,
        LastControlledHash  NVARCHAR(128)    NOT NULL,
        DiffSummary         NVARCHAR(MAX)    NULL,
        EmailSent           BIT              NOT NULL CONSTRAINT DF_Oob_EmailSent DEFAULT (0),
        Resolution          NVARCHAR(64)     NULL -- PendingDecision | Rollback | CheckoutAnyway
    );
END
GO
