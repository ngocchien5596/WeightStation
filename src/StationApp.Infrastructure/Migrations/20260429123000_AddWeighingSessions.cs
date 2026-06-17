using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StationApp.Infrastructure.Persistence;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    [DbContext(typeof(StationDbContext))]
    [Migration("20260429123000_AddWeighingSessions")]
    public partial class AddWeighingSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'WeighingSessionId') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [WeighingSessionId] uniqueidentifier NULL;
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'WeighingSessionId') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [WeighingSessionId] uniqueidentifier NULL;
END

IF COL_LENGTH('weigh_tickets', 'WeighingSessionId') IS NULL
BEGIN
    ALTER TABLE [weigh_tickets] ADD [WeighingSessionId] uniqueidentifier NULL;
END

IF COL_LENGTH('delivery_tickets', 'WeighingSessionId') IS NULL
BEGIN
    ALTER TABLE [delivery_tickets] ADD [WeighingSessionId] uniqueidentifier NULL;
END

IF COL_LENGTH('delivery_tickets', 'WeighingSessionLineId') IS NULL
BEGIN
    ALTER TABLE [delivery_tickets] ADD [WeighingSessionLineId] uniqueidentifier NULL;
END

IF OBJECT_ID(N'[weighing_sessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [weighing_sessions](
        [Id] uniqueidentifier NOT NULL,
        [SessionNo] nvarchar(50) NOT NULL,
        [TransactionType] nvarchar(30) NOT NULL,
        [VehiclePlate] nvarchar(30) NOT NULL,
        [MoocNumber] nvarchar(30) NULL,
        [DriverName] nvarchar(150) NULL,
        [Weight1] decimal(18,3) NULL,
        [Weight2] decimal(18,3) NULL,
        [NetWeight] decimal(18,3) NULL,
        [Weight1Time] datetime2 NULL,
        [Weight2Time] datetime2 NULL,
        [SessionStatus] nvarchar(30) NOT NULL,
        [IsCancelled] bit NOT NULL DEFAULT ((0)),
        [HasPrintedMasterWeighTicket] bit NOT NULL DEFAULT ((0)),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_weighing_sessions] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NULL
BEGIN
    CREATE TABLE [weighing_session_lines](
        [Id] uniqueidentifier NOT NULL,
        [WeighingSessionId] uniqueidentifier NOT NULL,
        [VehicleRegistrationId] uniqueidentifier NOT NULL,
        [SequenceNo] int NOT NULL,
        [CustomerCode] nvarchar(50) NULL,
        [CustomerName] nvarchar(255) NULL,
        [DistributorCode] nvarchar(50) NULL,
        [DistributorName] nvarchar(255) NULL,
        [ProductCode] nvarchar(50) NULL,
        [ProductName] nvarchar(255) NULL,
        [PlannedWeight] decimal(18,3) NULL,
        [PlannedBagCount] int NULL,
        [ActualAllocatedWeight] decimal(18,3) NULL,
        [ActualAllocatedBagCount] int NULL,
        [LineStatus] nvarchar(30) NOT NULL,
        [HasPrintedDeliveryTicket] bit NOT NULL DEFAULT ((0)),
        [DeliveryTicketId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_weighing_session_lines] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_weighing_session_id' AND object_id = OBJECT_ID('vehicle_registrations'))
BEGIN
    CREATE INDEX [IX_vehicle_registrations_weighing_session_id] ON [vehicle_registrations]([WeighingSessionId]);
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_weighing_session_id' AND object_id = OBJECT_ID('cut_orders'))
BEGIN
    CREATE INDEX [IX_cut_orders_weighing_session_id] ON [cut_orders]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_line_id' AND object_id = OBJECT_ID('delivery_tickets'))
BEGIN
    CREATE INDEX [IX_delivery_tickets_weighing_session_line_id] ON [delivery_tickets]([WeighingSessionLineId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_id' AND object_id = OBJECT_ID('delivery_tickets'))
BEGIN
    CREATE INDEX [IX_delivery_tickets_weighing_session_id] ON [delivery_tickets]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weigh_tickets_weighing_session_id' AND object_id = OBJECT_ID('weigh_tickets'))
BEGIN
    CREATE INDEX [IX_weigh_tickets_weighing_session_id] ON [weigh_tickets]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_sessions_session_no' AND object_id = OBJECT_ID('weighing_sessions'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_sessions_session_no] ON [weighing_sessions]([SessionNo]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_vehicle_plate' AND object_id = OBJECT_ID('weighing_sessions'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_vehicle_plate] ON [weighing_sessions]([VehiclePlate]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_status' AND object_id = OBJECT_ID('weighing_sessions'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_status] ON [weighing_sessions]([SessionStatus]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_created_at' AND object_id = OBJECT_ID('weighing_sessions'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_created_at] ON [weighing_sessions]([CreatedAt]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_session_id' AND object_id = OBJECT_ID('weighing_session_lines'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_session_id] ON [weighing_session_lines]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_registration_id' AND object_id = OBJECT_ID('weighing_session_lines'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_registration_id] ON [weighing_session_lines]([VehicleRegistrationId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_session_lines_session_registration' AND object_id = OBJECT_ID('weighing_session_lines'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_session_lines_session_registration] ON [weighing_session_lines]([WeighingSessionId], [VehicleRegistrationId]);
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "weighing_session_lines");
            migrationBuilder.DropTable(name: "weighing_sessions");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_weighing_session_id' AND object_id = OBJECT_ID('vehicle_registrations'))
BEGIN
    DROP INDEX [IX_vehicle_registrations_weighing_session_id] ON [vehicle_registrations];
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_weighing_session_id' AND object_id = OBJECT_ID('cut_orders'))
BEGIN
    DROP INDEX [IX_cut_orders_weighing_session_id] ON [cut_orders];
END
""");

            migrationBuilder.DropIndex(name: "IX_delivery_tickets_weighing_session_line_id", table: "delivery_tickets");
            migrationBuilder.DropIndex(name: "IX_delivery_tickets_weighing_session_id", table: "delivery_tickets");
            migrationBuilder.DropIndex(name: "IX_weigh_tickets_weighing_session_id", table: "weigh_tickets");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'WeighingSessionId') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [WeighingSessionId];
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'WeighingSessionId') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [WeighingSessionId];
END
""");

            migrationBuilder.DropColumn(name: "WeighingSessionId", table: "weigh_tickets");
            migrationBuilder.DropColumn(name: "WeighingSessionId", table: "delivery_tickets");
            migrationBuilder.DropColumn(name: "WeighingSessionLineId", table: "delivery_tickets");
        }
    }
}
