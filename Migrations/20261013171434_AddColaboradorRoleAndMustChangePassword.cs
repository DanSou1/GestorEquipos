using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddColaboradorRoleAndMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "UserSystem",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Las cuentas que ya existían antes de este cambio (Admin/Técnico/Auditor ya en uso)
            // ya conocen su contraseña — solo las cuentas Colaborador creadas de aquí en adelante
            // (o por el backfill de AuthBootstrapper) deben nacer en true.
            migrationBuilder.Sql("UPDATE [UserSystem] SET [MustChangePassword] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "UserSystem");
        }
    }
}
