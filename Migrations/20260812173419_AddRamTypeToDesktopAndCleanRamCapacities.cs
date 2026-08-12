using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddRamTypeToDesktopAndCleanRamCapacities : Migration
    {
        // RamType enum values (Models/Desktop.cs): DDR2 = 0, DDR3 = 1, DDR4 = 2, DDR5 = 3.
        // Corrects the AddRamTypeAndReseedCapacities migration, which put RamType on the Ram
        // lookup row itself (forcing one Ram row per Type+Capacity combination). RamType and
        // capacity are independent selections in the UI, so RamType now lives directly on
        // Desktop instead, and Ram goes back to being capacity-only.
        private const int DefaultRamType = 2; // DDR4 — see note below.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable for now — backfilled below before being made required.
            migrationBuilder.AddColumn<int>(
                name: "RamType",
                table: "Desktop",
                type: "int",
                nullable: true);

            // No per-desktop RAM type is recoverable at this point, so every existing Desktop
            // is backfilled to a single default (DDR4) — matching what the migration this one
            // corrects already reset every desktop's RAM to, so this is not an additional loss.
            migrationBuilder.Sql($"UPDATE [Desktop] SET [RamType] = {DefaultRamType};");

            migrationBuilder.AlterColumn<int>(
                name: "RamType",
                table: "Desktop",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Collapse Ram back down to one row per capacity (keep the lowest Id per
            // Especification as canonical), repointing any Desktop that referenced a
            // duplicate before deleting the duplicates.
            migrationBuilder.Sql(
                "WITH Canonical AS (" +
                "    SELECT [Especification], MIN([Id]) AS CanonicalId" +
                "    FROM [Ram]" +
                "    GROUP BY [Especification]" +
                ") " +
                "UPDATE d SET d.[RamId] = c.CanonicalId " +
                "FROM [Desktop] d " +
                "INNER JOIN [Ram] r ON d.[RamId] = r.[Id] " +
                "INNER JOIN Canonical c ON c.[Especification] = r.[Especification] " +
                "WHERE r.[Id] <> c.CanonicalId;");

            migrationBuilder.Sql(
                "WITH Canonical AS (" +
                "    SELECT [Especification], MIN([Id]) AS CanonicalId" +
                "    FROM [Ram]" +
                "    GROUP BY [Especification]" +
                ") " +
                "DELETE r FROM [Ram] r " +
                "INNER JOIN Canonical c ON c.[Especification] = r.[Especification] " +
                "WHERE r.[Id] <> c.CanonicalId;");

            migrationBuilder.CreateIndex(
                name: "IX_Ram_Especification",
                table: "Ram",
                column: "Especification",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: does not restore the collapsed Ram duplicates or the RamType
            // that used to live on Ram rows.
            migrationBuilder.DropIndex(
                name: "IX_Ram_Especification",
                table: "Ram");

            migrationBuilder.DropColumn(
                name: "RamType",
                table: "Desktop");
        }
    }
}
