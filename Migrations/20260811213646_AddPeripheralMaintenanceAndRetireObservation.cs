using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddPeripheralMaintenanceAndRetireObservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeripheralMaintenance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceTypeId = table.Column<int>(type: "int", nullable: false),
                    PeripheralId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TechnicianUserSystemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeripheralMaintenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeripheralMaintenance_MaintenanceType_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeripheralMaintenance_Peripheral_PeripheralId",
                        column: x => x.PeripheralId,
                        principalTable: "Peripheral",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeripheralMaintenance_UserSystem_TechnicianUserSystemId",
                        column: x => x.TechnicianUserSystemId,
                        principalTable: "UserSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralMaintenance_MaintenanceTypeId",
                table: "PeripheralMaintenance",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralMaintenance_PeripheralId",
                table: "PeripheralMaintenance",
                column: "PeripheralId");

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralMaintenance_TechnicianUserSystemId",
                table: "PeripheralMaintenance",
                column: "TechnicianUserSystemId");

            // Best-effort preservation of historical PeripheralObservation data before dropping the
            // table. Type 0=Reparacion, 1=Cambio are migrated to PeripheralMaintenance rows (using a
            // clearly-labeled placeholder MaintenanceType and the first Administrador UserSystem as a
            // stand-in technician, since old observations never recorded who did the work). Type
            // 2=BajaRAES rows are intentionally skipped — already reflected in the Peripheral.Estado
            // bool migrated by ChangePeripheralEstadoToBool. If no Administrador UserSystem exists yet
            // (e.g. a brand-new database where this runs before the first admin login), the historical
            // rows are silently left unmigrated rather than failing the whole migration — as of this
            // migration's authoring, the local dev database has zero PeripheralObservation rows, so
            // this path is defensive for other environments, not exercised here.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [PeripheralObservation] WHERE [Type] IN (0, 1))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [MaintenanceType] WHERE [Type] = N'Migrado de periférico')
        INSERT INTO [MaintenanceType] ([Type]) VALUES (N'Migrado de periférico');

    DECLARE @fallbackTypeId INT = (SELECT TOP 1 [Id] FROM [MaintenanceType] WHERE [Type] = N'Migrado de periférico');
    DECLARE @fallbackTechId INT = (
        SELECT TOP 1 [us].[Id]
        FROM [UserSystem] [us]
        INNER JOIN [Rol] [r] ON [us].[RolId] = [r].[Id]
        WHERE [r].[Name] = N'Administrador'
        ORDER BY [us].[Id]);

    IF @fallbackTechId IS NOT NULL
    BEGIN
        INSERT INTO [PeripheralMaintenance] ([PeripheralId], [MaintenanceTypeId], [TechnicianUserSystemId], [Date], [Description])
        SELECT [PeripheralId], @fallbackTypeId, @fallbackTechId, [Date],
               N'[Migrado automáticamente desde observación histórica, sin técnico registrado] ' + [Description]
        FROM [PeripheralObservation]
        WHERE [Type] IN (0, 1);
    END
END");

            migrationBuilder.DropTable(
                name: "PeripheralObservation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeripheralObservation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeripheralId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
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
                name: "IX_PeripheralObservation_PeripheralId",
                table: "PeripheralObservation",
                column: "PeripheralId");

            // Data migrated into PeripheralMaintenance by Up() is not migrated back — this is a
            // best-effort inverse for schema rollback only, matching Down()'s convention elsewhere
            // in this migration set (e.g. ChangePeripheralEstadoToBool).
            migrationBuilder.DropTable(
                name: "PeripheralMaintenance");
        }
    }
}
