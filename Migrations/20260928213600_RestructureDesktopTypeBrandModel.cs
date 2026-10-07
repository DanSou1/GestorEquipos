using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class RestructureDesktopTypeBrandModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add nullable Brand/Model columns to DesktopType — the old Name column stays
            // around until every row below has a real Brand/Model to replace it with.
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "DesktopType",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "DesktopType",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 2. "Claim" each existing DesktopType row using the Brand/Model of one Desktop
            // that already references it (covers the placeholder type seeded by the previous
            // migration, e.g. "Escritorio").
            migrationBuilder.Sql(@"
                UPDATE dt
                SET dt.Brand = d.Brand, dt.Model = d.Model
                FROM [DesktopType] dt
                CROSS APPLY (SELECT TOP 1 Brand, Model FROM [Desktop] WHERE DesktopTypeId = dt.Id) d;
            ");

            // 3. Insert a DesktopType row for every distinct (Brand, Model) pair not already
            // covered above — needed if a placeholder type was shared by desktops of different
            // brands/models.
            migrationBuilder.Sql(@"
                INSERT INTO [DesktopType] (Brand, Model)
                SELECT DISTINCT d.Brand, d.Model
                FROM [Desktop] d
                WHERE NOT EXISTS (
                    SELECT 1 FROM [DesktopType] dt WHERE dt.Brand = d.Brand AND dt.Model = d.Model
                );
            ");

            // 4. Re-point every Desktop to the DesktopType row matching its own Brand/Model.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.DesktopTypeId = dt.Id
                FROM [Desktop] d
                INNER JOIN [DesktopType] dt ON dt.Brand = d.Brand AND dt.Model = d.Model;
            ");

            // 5. Drop catalog rows that never got populated (unused placeholder types with no
            // desktop referencing them).
            migrationBuilder.Sql(@"
                DELETE FROM [DesktopType]
                WHERE Brand IS NULL
                  AND Id NOT IN (SELECT DISTINCT DesktopTypeId FROM [Desktop]);
            ");

            // 6. Desktop.Brand/Model are now redundant — DesktopTypeId is authoritative.
            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Desktop");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Desktop");

            // 7. Retire the old single-column catalog shape.
            migrationBuilder.DropIndex(
                name: "IX_DesktopType_Name",
                table: "DesktopType");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "DesktopType");

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "DesktopType",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "DesktopType",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesktopType_Brand_Model",
                table: "DesktopType",
                columns: new[] { "Brand", "Model" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DesktopType_Brand_Model",
                table: "DesktopType");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "DesktopType",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [DesktopType] SET [Name] = [Brand] + ', ' + [Model];");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Desktop",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Desktop",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Best-effort only: the original independent Brand/Model text on Desktop cannot be
            // reconstructed exactly, so both get the catalog's combined name.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.Brand = dt.Name, d.Model = dt.Name
                FROM [Desktop] d
                INNER JOIN [DesktopType] dt ON dt.Id = d.DesktopTypeId;
            ");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "DesktopType");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "DesktopType");

            migrationBuilder.CreateIndex(
                name: "IX_DesktopType_Name",
                table: "DesktopType",
                column: "Name",
                unique: true);
        }
    }
}
