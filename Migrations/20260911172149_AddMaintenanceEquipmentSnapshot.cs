using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceEquipmentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotHolderArea",
                table: "Maintenance",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHolderName",
                table: "Maintenance",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceEquipmentSnapshotItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceEquipmentSnapshotItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceEquipmentSnapshotItem_Maintenance_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEquipmentSnapshotItem_MaintenanceId",
                table: "MaintenanceEquipmentSnapshotItem",
                column: "MaintenanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceEquipmentSnapshotItem");

            migrationBuilder.DropColumn(
                name: "SnapshotHolderArea",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "SnapshotHolderName",
                table: "Maintenance");
        }
    }
}
