using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class SeedPeripheralTypeCatalog : Migration
    {
        // "Diadema" is intentionally excluded here — it already exists in every environment seeded
        // manually before this migration (headset entry created via the old Create-only UI).
        private static readonly string[] SeedNames = new[]
        {
            "Teclado",
            "Mouse",
            "Monitor",
            "Parlantes",
            "Micrófono",
            "Cámara web",
            "Base refrigerante para portátil",
            "Dock/Adaptador USB-C",
            "Hub USB",
            "Mousepad",
            "Impresora",
            "Escáner",
            "UPS",
            "Disco duro externo",
            "Lector de tarjetas",
            "Cable HDMI",
            "Cable VGA",
            "Cable de red",
            "Control remoto",
            "Teclado numérico externo",
            "Filtro de privacidad",
            "Soporte para monitor",
            "Regulador de voltaje"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Environments seeded before this migration may already have some of these names
            // (created via the old Create-only UI) — insert is conditional to avoid violating
            // the unique index on PeripheralType.Name.
            foreach (var name in SeedNames)
            {
                var escaped = name.Replace("'", "''");
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [PeripheralType] WHERE [Name] = N'{escaped}') " +
                    $"INSERT INTO [PeripheralType] ([Name]) VALUES (N'{escaped}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: removes rows by name, which also removes any of these that predated
            // this migration (e.g. a pre-existing "Teclado" row) rather than only the ones it added.
            migrationBuilder.DeleteData(
                table: "PeripheralType",
                keyColumn: "Name",
                keyValues: SeedNames.Select(name => (object)name).ToArray());
        }
    }
}
