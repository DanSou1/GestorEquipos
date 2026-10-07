using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddCasoTIModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TipoCaso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoCaso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CasoTI",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    TecnicoUserSystemId = table.Column<int>(type: "int", nullable: false),
                    PersonaUserId = table.Column<int>(type: "int", nullable: false),
                    AreaSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoCasoId = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RatingToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RatingSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RatingValue = table.Column<int>(type: "int", nullable: true),
                    RatingComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasoTI", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasoTI_TipoCaso_TipoCasoId",
                        column: x => x.TipoCasoId,
                        principalTable: "TipoCaso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CasoTI_UserSystem_TecnicoUserSystemId",
                        column: x => x.TecnicoUserSystemId,
                        principalTable: "UserSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CasoTI_Users_PersonaUserId",
                        column: x => x.PersonaUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CasoTI_PersonaUserId",
                table: "CasoTI",
                column: "PersonaUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CasoTI_RatingToken",
                table: "CasoTI",
                column: "RatingToken",
                unique: true,
                filter: "[RatingToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CasoTI_TecnicoUserSystemId",
                table: "CasoTI",
                column: "TecnicoUserSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_CasoTI_TipoCasoId",
                table: "CasoTI",
                column: "TipoCasoId");

            migrationBuilder.CreateIndex(
                name: "IX_TipoCaso_Nombre",
                table: "TipoCaso",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CasoTI");

            migrationBuilder.DropTable(
                name: "TipoCaso");
        }
    }
}
