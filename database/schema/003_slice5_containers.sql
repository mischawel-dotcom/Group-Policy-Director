-- Group Policy Director — Slice 5 schema stubs (SQL Server 2019+)
-- Controlled AD containers (OU/Site/Domain) and link version history.

IF OBJECT_ID(N'dbo.ControlledContainers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ControlledContainers (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DomainId          UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Domains(Id),
        Kind              INT              NOT NULL, -- OU=1 Domain=2 Site=3
        ContainerKey      NVARCHAR(1024)   NOT NULL,
        DisplayName       NVARCHAR(512)    NOT NULL,
        Status            INT              NOT NULL,
        CheckedOutBy      NVARCHAR(256)    NULL,
        CheckedOutAt      DATETIMEOFFSET   NULL,
        LinksHash         NVARCHAR(128)    NULL,
        TakenControlAt    DATETIMEOFFSET   NOT NULL,
        TakenControlBy    NVARCHAR(256)    NULL,
        PendingVersionId  UNIQUEIDENTIFIER NULL,
        ApprovedBy        NVARCHAR(256)    NULL,
        ApprovedAt        DATETIMEOFFSET   NULL,
        CONSTRAINT UQ_ControlledContainers_Key UNIQUE (ContainerKey)
    );
END
GO

IF OBJECT_ID(N'dbo.ContainerVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContainerVersions (
        Id                   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ControlledContainerId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ControlledContainers(Id),
        VersionNumber        INT              NOT NULL,
        Comment              NVARCHAR(1024)   NOT NULL,
        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedAt            DATETIMEOFFSET   NOT NULL,
        LinksSnapshotJson    NVARCHAR(MAX)    NULL,
        LinksHash            NVARCHAR(128)    NULL,
        Provenance           NVARCHAR(64)     NULL,
        WorkflowState        NVARCHAR(64)     NULL,
        ReviewedBy           NVARCHAR(256)    NULL,
        ReviewComment        NVARCHAR(1024)   NULL,
        CONSTRAINT UQ_ContainerVersions_Number UNIQUE (ControlledContainerId, VersionNumber)
    );
END
GO
