using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceCategoryAndChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Maintenance",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MaintenanceChecklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceId = table.Column<int>(type: "int", nullable: false),
                    VerificacionSistemaMecanico = table.Column<bool>(type: "bit", nullable: false),
                    VerificacionSistemaElectrico = table.Column<bool>(type: "bit", nullable: false),
                    VerificacionSistemaSeguridad = table.Column<bool>(type: "bit", nullable: false),
                    SopladoInterno = table.Column<bool>(type: "bit", nullable: false),
                    LavadoInterno = table.Column<bool>(type: "bit", nullable: false),
                    Lubricacion = table.Column<bool>(type: "bit", nullable: false),
                    AjusteTornilleria = table.Column<bool>(type: "bit", nullable: false),
                    CambioAccesorios = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceChecklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceChecklist_Maintenance_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceType_Type",
                table: "MaintenanceType",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceChecklist_MaintenanceId",
                table: "MaintenanceChecklist",
                column: "MaintenanceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceChecklist");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceType_Type",
                table: "MaintenanceType");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Maintenance");
        }
    }
}
