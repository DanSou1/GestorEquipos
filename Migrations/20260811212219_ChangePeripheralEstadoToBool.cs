using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class ChangePeripheralEstadoToBool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The naive AlterColumn<bool> EF would generate here casts int->bit directly, which
            // fails against real data: PeripheralEstado.Raes (2) has no valid bit representation.
            // Add-column/backfill/drop/rename instead, so existing rows are remapped safely:
            // Activo (0) and Inactivo (1) -> true (Inactivo was never reachable via any UI flow,
            // confirmed against Views/Peripheral/AddObservation.cshtml), Raes (2) -> false.
            migrationBuilder.AddColumn<bool>(
                name: "EstadoBool",
                table: "Peripheral",
                type: "bit",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Peripheral] SET [EstadoBool] = CASE WHEN [Estado] = 2 THEN 0 ELSE 1 END");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Peripheral");

            migrationBuilder.RenameColumn(
                name: "EstadoBool",
                table: "Peripheral",
                newName: "Estado");

            migrationBuilder.AlterColumn<bool>(
                name: "Estado",
                table: "Peripheral",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort inverse: true -> Activo (0), false -> Raes (2). The Inactivo (1) value
            // is permanently lost, matching Up()'s mapping — it was never reachable via the UI.
            migrationBuilder.AddColumn<int>(
                name: "EstadoInt",
                table: "Peripheral",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Peripheral] SET [EstadoInt] = CASE WHEN [Estado] = 1 THEN 0 ELSE 2 END");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Peripheral");

            migrationBuilder.RenameColumn(
                name: "EstadoInt",
                table: "Peripheral",
                newName: "Estado");

            migrationBuilder.AlterColumn<int>(
                name: "Estado",
                table: "Peripheral",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
