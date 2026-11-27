-- Group Policy Director — Slice 6: Break Glass sessions + Audit indexes
-- Apply with migrations tooling later; this file documents the intended model.

IF OBJECT_ID(N'dbo.BreakGlassSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BreakGlassSessions (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TrusteePrincipal NVARCHAR(512)    NOT NULL,
        ActivatedBy      NVARCHAR(256)    NOT NULL,
        ActivatedAt      DATETIMEOFFSET   NOT NULL,
        ExpiresAt        DATETIMEOFFSET   NOT NULL,
        Reason           NVARCHAR(1024)   NOT NULL,
        RevokedAt        DATETIMEOFFSET   NULL,
        RevokedBy        NVARCHAR(256)    NULL
    );
    CREATE INDEX IX_BreakGlassSessions_Trustee ON dbo.BreakGlassSessions(TrusteePrincipal);
    CREATE INDEX IX_BreakGlassSessions_Expires ON dbo.BreakGlassSessions(ExpiresAt DESC);
END
GO

IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditEvents_Actor' AND object_id = OBJECT_ID(N'dbo.AuditEvents'))
        CREATE INDEX IX_AuditEvents_Actor ON dbo.AuditEvents(Actor);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditEvents_Action' AND object_id = OBJECT_ID(N'dbo.AuditEvents'))
        CREATE INDEX IX_AuditEvents_Action ON dbo.AuditEvents(Action);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditEvents_ObjectKey' AND object_id = OBJECT_ID(N'dbo.AuditEvents'))
        CREATE INDEX IX_AuditEvents_ObjectKey ON dbo.AuditEvents(ObjectKey);
END
GO
