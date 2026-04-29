using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryTicketId",
                table: "weigh_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverWeight",
                table: "weigh_tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryDisplay",
                table: "weigh_tickets",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrinted",
                table: "weigh_tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MoocRegistrationExpirySnapshot",
                table: "weigh_tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoocRegistrationNoSnapshot",
                table: "weigh_tickets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordRole",
                table: "weigh_tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WORKING");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTicketId",
                table: "weigh_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SplitGroupId",
                table: "weigh_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "SplitSequence",
                table: "weigh_tickets",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TtcpWeightSnapshot",
                table: "weigh_tickets",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VehicleRegistrationExpirySnapshot",
                table: "weigh_tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegistrationNoSnapshot",
                table: "weigh_tickets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErpVehicleRegistrationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsOverWeight = table.Column<bool>(type: "bit", nullable: false),
                    IsPrinted = table.Column<bool>(type: "bit", nullable: false),
                    SplitGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SplitSequence = table.Column<byte>(type: "tinyint", nullable: true),
                    RecordRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "WORKING"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehiclePlate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MoocNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransportMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TtcpWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    VehicleRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehicleRegistrationExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MoocRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MoocRegistrationExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_record_role",
                table: "weigh_tickets",
                column: "RecordRole");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_split_group_id",
                table: "weigh_tickets",
                column: "SplitGroupId");

            migrationBuilder.CreateIndex(
                name: "UX_customers_code",
                table: "customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_erp_reg_id",
                table: "delivery_tickets",
                column: "ErpVehicleRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_is_printed",
                table: "delivery_tickets",
                column: "IsPrinted");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_split_group_id",
                table: "delivery_tickets",
                column: "SplitGroupId");

            migrationBuilder.CreateIndex(
                name: "UX_delivery_tickets_no",
                table: "delivery_tickets",
                column: "DeliveryNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_products_code",
                table: "products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_is_active",
                table: "vehicles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_plate",
                table: "vehicles",
                column: "VehiclePlate");

            migrationBuilder.CreateIndex(
                name: "UX_vehicles_plate_mooc",
                table: "vehicles",
                columns: new[] { "VehiclePlate", "MoocNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "delivery_tickets");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_weigh_tickets_record_role",
                table: "weigh_tickets");

            migrationBuilder.DropIndex(
                name: "IX_weigh_tickets_split_group_id",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "DeliveryTicketId",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "IsOverWeight",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "IsPrimaryDisplay",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "IsPrinted",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "MoocRegistrationExpirySnapshot",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "MoocRegistrationNoSnapshot",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "RecordRole",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "SourceTicketId",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "SplitGroupId",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "SplitSequence",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "TtcpWeightSnapshot",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationExpirySnapshot",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationNoSnapshot",
                table: "weigh_tickets");
        }
    }
}
