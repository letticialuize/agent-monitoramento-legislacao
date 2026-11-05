using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agente.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RelacaoTributaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "relacionada_tributos",
                table: "novidades",
                type: "boolean",
                nullable: true);

            // Reavalia pela IA o que já foi coletado: sem isso, as novidades existentes nunca seriam ocultadas.
            migrationBuilder.Sql("UPDATE novidades SET aguardando_classificacao_ia = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "relacionada_tributos",
                table: "novidades");
        }
    }
}
