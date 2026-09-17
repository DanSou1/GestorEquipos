using Microsoft.EntityFrameworkCore.Migrations;

namespace GestorEquipos.Migrations
{
    public partial class AddUserSystemSecurityStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "UserSystem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SecurityStamp", table: "UserSystem");
        }
    }
}
