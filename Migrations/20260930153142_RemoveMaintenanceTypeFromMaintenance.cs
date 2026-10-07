using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMaintenanceTypeFromMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Maintenance_MaintenanceType_MaintenanceTypeId",
                table: "Maintenance");

            migrationBuilder.DropIndex(
                name: "IX_Maintenance_MaintenanceTypeId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeId",
                table: "Maintenance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaintenanceTypeId",
                table: "Maintenance",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_MaintenanceTypeId",
                table: "Maintenance",
                column: "MaintenanceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Maintenance_MaintenanceType_MaintenanceTypeId",
                table: "Maintenance",
                column: "MaintenanceTypeId",
                principalTable: "MaintenanceType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
