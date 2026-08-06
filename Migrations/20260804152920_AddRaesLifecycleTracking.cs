using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddRaesLifecycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TechnicianUserSystemId",
                table: "Maintenance",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Estado",
                table: "Desktop",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "License",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesktopId = table.Column<int>(type: "int", nullable: false),
                    SoftwareType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LicenseKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NoLicense = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_License", x => x.Id);
                    table.ForeignKey(
                        name: "FK_License_Desktop_DesktopId",
                        column: x => x.DesktopId,
                        principalTable: "Desktop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeripheralType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeripheralType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecChangeLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesktopId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ChangedByUserSystemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecChangeLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecChangeLog_Desktop_DesktopId",
                        column: x => x.DesktopId,
                        principalTable: "Desktop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecChangeLog_UserSystem_ChangedByUserSystemId",
                        column: x => x.ChangedByUserSystemId,
                        principalTable: "UserSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Peripheral",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesktopId = table.Column<int>(type: "int", nullable: false),
                    PeripheralTypeId = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Peripheral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Peripheral_Desktop_DesktopId",
                        column: x => x.DesktopId,
                        principalTable: "Desktop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Peripheral_PeripheralType_PeripheralTypeId",
                        column: x => x.PeripheralTypeId,
                        principalTable: "PeripheralType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeripheralObservation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeripheralId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeripheralObservation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeripheralObservation_Peripheral_PeripheralId",
                        column: x => x.PeripheralId,
                        principalTable: "Peripheral",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_TechnicianUserSystemId",
                table: "Maintenance",
                column: "TechnicianUserSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_License_DesktopId",
                table: "License",
                column: "DesktopId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripheral_DesktopId",
                table: "Peripheral",
                column: "DesktopId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripheral_PeripheralTypeId",
                table: "Peripheral",
                column: "PeripheralTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralObservation_PeripheralId",
                table: "PeripheralObservation",
                column: "PeripheralId");

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralType_Name",
                table: "PeripheralType",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecChangeLog_ChangedByUserSystemId",
                table: "SpecChangeLog",
                column: "ChangedByUserSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecChangeLog_DesktopId",
                table: "SpecChangeLog",
                column: "DesktopId");

            migrationBuilder.AddForeignKey(
                name: "FK_Maintenance_UserSystem_TechnicianUserSystemId",
                table: "Maintenance",
                column: "TechnicianUserSystemId",
                principalTable: "UserSystem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Maintenance_UserSystem_TechnicianUserSystemId",
                table: "Maintenance");

            migrationBuilder.DropTable(
                name: "License");

            migrationBuilder.DropTable(
                name: "PeripheralObservation");

            migrationBuilder.DropTable(
                name: "SpecChangeLog");

            migrationBuilder.DropTable(
                name: "Peripheral");

            migrationBuilder.DropTable(
                name: "PeripheralType");

            migrationBuilder.DropIndex(
                name: "IX_Maintenance_TechnicianUserSystemId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "TechnicianUserSystemId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Desktop");
        }
    }
}
