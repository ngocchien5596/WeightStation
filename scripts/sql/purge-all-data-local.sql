USE [StationAppLocal];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*
    WARNING:
    - Script nay chi xoa du lieu nghiep vu va du lieu dong bo trong DB local.
    - Giu lai cac bang cau hinh/du lieu nen can cho app chay:
      + users
      + app_config
      + print_template_profiles
      + stations
      + station_feature_flags
      + station_operation_settings
      + user_station_assignments
      + vehicles
      + customers
      + products
    - Xoa ca document_counters de reset lai sequence so chung tu cho du lieu moi.
    - Khong xoa schema, index, trigger, stored procedure, __EFMigrationsHistory.
    - Neu ten DB local cua ban khac StationAppLocal, sua lai dong USE o tren.
*/

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.weighing_session_images', N'U') IS NOT NULL ALTER TABLE dbo.weighing_session_images NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.delivery_tickets', N'U') IS NOT NULL ALTER TABLE dbo.delivery_tickets NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weigh_tickets', N'U') IS NOT NULL ALTER TABLE dbo.weigh_tickets NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weighing_session_lines', N'U') IS NOT NULL ALTER TABLE dbo.weighing_session_lines NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.cut_orders', N'U') IS NOT NULL ALTER TABLE dbo.cut_orders NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weighing_sessions', N'U') IS NOT NULL ALTER TABLE dbo.weighing_sessions NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.sync_outbox', N'U') IS NOT NULL ALTER TABLE dbo.sync_outbox NOCHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NOT NULL ALTER TABLE dbo.audit_logs NOCHECK CONSTRAINT ALL;

    IF OBJECT_ID(N'dbo.cut_orders', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.cut_orders
        SET
            CurrentPrimaryWeighTicketId = NULL,
            CurrentPrimaryDeliveryTicketId = NULL,
            WeighingSessionId = NULL;
    END;

    IF OBJECT_ID(N'dbo.weigh_tickets', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.weigh_tickets
        SET
            DeliveryTicketId = NULL,
            WeighingSessionId = NULL;
    END;

    IF OBJECT_ID(N'dbo.delivery_tickets', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.delivery_tickets
        SET
            WeighingSessionId = NULL,
            WeighingSessionLineId = NULL;
    END;

    IF OBJECT_ID(N'dbo.weighing_session_lines', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.weighing_session_lines
        SET DeliveryTicketId = NULL;
    END;

    IF OBJECT_ID(N'dbo.weighing_session_images', N'U') IS NOT NULL DELETE FROM dbo.weighing_session_images;
    IF OBJECT_ID(N'dbo.delivery_tickets', N'U') IS NOT NULL DELETE FROM dbo.delivery_tickets;
    IF OBJECT_ID(N'dbo.weigh_tickets', N'U') IS NOT NULL DELETE FROM dbo.weigh_tickets;
    IF OBJECT_ID(N'dbo.weighing_session_lines', N'U') IS NOT NULL DELETE FROM dbo.weighing_session_lines;
    IF OBJECT_ID(N'dbo.cut_orders', N'U') IS NOT NULL DELETE FROM dbo.cut_orders;
    IF OBJECT_ID(N'dbo.weighing_sessions', N'U') IS NOT NULL DELETE FROM dbo.weighing_sessions;
    IF OBJECT_ID(N'dbo.sync_outbox', N'U') IS NOT NULL DELETE FROM dbo.sync_outbox;
    IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NOT NULL DELETE FROM dbo.audit_logs;
    IF OBJECT_ID(N'dbo.document_counters', N'U') IS NOT NULL DELETE FROM dbo.document_counters;

    IF OBJECT_ID(N'dbo.weighing_session_images', N'U') IS NOT NULL ALTER TABLE dbo.weighing_session_images WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.delivery_tickets', N'U') IS NOT NULL ALTER TABLE dbo.delivery_tickets WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weigh_tickets', N'U') IS NOT NULL ALTER TABLE dbo.weigh_tickets WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weighing_session_lines', N'U') IS NOT NULL ALTER TABLE dbo.weighing_session_lines WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.cut_orders', N'U') IS NOT NULL ALTER TABLE dbo.cut_orders WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.weighing_sessions', N'U') IS NOT NULL ALTER TABLE dbo.weighing_sessions WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.sync_outbox', N'U') IS NOT NULL ALTER TABLE dbo.sync_outbox WITH CHECK CHECK CONSTRAINT ALL;
    IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NOT NULL ALTER TABLE dbo.audit_logs WITH CHECK CHECK CONSTRAINT ALL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
