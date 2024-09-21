using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FacturacionAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateBoletasTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boletas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroBoleta = table.Column<long>(type: "bigint", nullable: false),
                    Cirujano = table.Column<string>(type: "text", nullable: true),
                    Gravado = table.Column<bool>(type: "boolean", nullable: false),
                    Hospital = table.Column<string>(type: "text", nullable: true),
                    Afiliado = table.Column<string>(type: "text", nullable: true),
                    Paciente = table.Column<string>(type: "text", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Periodo = table.Column<string>(type: "text", nullable: false),
                    IdProfesional = table.Column<int>(type: "integer", nullable: false),
                    Facturado = table.Column<decimal>(type: "numeric", nullable: false),
                    Cobrado = table.Column<decimal>(type: "numeric", nullable: false),
                    Debitado = table.Column<decimal>(type: "numeric", nullable: false),
                    Saldo = table.Column<decimal>(type: "numeric", nullable: false),
                    IdEntidad = table.Column<int>(type: "integer", nullable: false),
                    Edad = table.Column<int>(type: "integer", nullable: false),
                    MontoARecibir = table.Column<decimal>(type: "numeric", nullable: true),
                    BoletaRefacturada = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boletas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profesionales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroSocio = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesionales", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Boletas");

            migrationBuilder.DropTable(
                name: "Profesionales");
        }
    }
}
