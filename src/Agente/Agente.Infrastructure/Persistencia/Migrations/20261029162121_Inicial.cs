using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agente.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "consultas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlacao_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conversa_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    periodo_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    esferas = table.Column<string>(type: "jsonb", nullable: false),
                    perfis = table.Column<string>(type: "jsonb", nullable: false),
                    grupos = table.Column<string>(type: "jsonb", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    requer_revisao_humana = table.Column<bool>(type: "boolean", nullable: false),
                    limitacoes = table.Column<string>(type: "jsonb", nullable: false),
                    versao_contrato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entes_monitorados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entes_monitorados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "execucoes_coleta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlacao_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    novidades_encontradas = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execucoes_coleta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "novidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_deduplicacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: false),
                    orgao_emissor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_ato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero_ato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ementa = table.Column<string>(type: "text", nullable: false),
                    trecho = table.Column<string>(type: "text", nullable: true),
                    link_oficial = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    esfera = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    grupo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    impacto = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    perfis = table.Column<string>(type: "jsonb", nullable: false),
                    origem_perfil = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    origem_impacto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    perfil_decidido = table.Column<bool>(type: "boolean", nullable: false),
                    aguardando_classificacao_ia = table.Column<bool>(type: "boolean", nullable: false),
                    situacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    retifica_novidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsavel_interno = table.Column<string>(type: "jsonb", nullable: false),
                    hash_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    coletada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novidades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registros_consumo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlacao_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tentativa = table.Column<int>(type: "integer", nullable: false),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: true),
                    tokens_saida = table.Column<int>(type: "integer", nullable: true),
                    uso_conhecido = table.Column<bool>(type: "boolean", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracao_ms = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    custo_estimado = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    custo_informado = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    moeda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tabela_preco_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_consumo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_preco",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    preco_entrada_por_milhao = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    preco_saida_por_milhao = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    moeda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    url_referencia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_preco", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "versoes_catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    documento_origem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    aprovado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versoes_catalogo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "etapas_execucao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consulta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    detalhe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etapas_execucao", x => x.id);
                    table.ForeignKey(
                        name: "FK_etapas_execucao_consultas_consulta_id",
                        column: x => x.consulta_id,
                        principalTable: "consultas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ocorrencias_novidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    novidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: true),
                    natureza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    execucao_coleta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coletada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ocorrencias_novidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_ocorrencias_novidade_novidades_novidade_id",
                        column: x => x.novidade_id,
                        principalTable: "novidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revisoes_novidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    novidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    link_oficial_informado = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revisoes_novidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_revisoes_novidade_novidades_novidade_id",
                        column: x => x.novidade_id,
                        principalTable: "novidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fontes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_catalogo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    link_oficial = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    o_que_monitorar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    orgao_emissor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    esfera = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    grupo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    natureza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    responsavel_interno = table.Column<string>(type: "jsonb", nullable: false),
                    formatos = table.Column<string>(type: "jsonb", nullable: false),
                    tipo_conector = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    configuracao_conector = table.Column<string>(type: "jsonb", nullable: true),
                    periodicidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    excecao_dominio_aprovada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fontes", x => x.id);
                    table.ForeignKey(
                        name: "FK_fontes_versoes_catalogo_versao_catalogo_id",
                        column: x => x.versao_catalogo_id,
                        principalTable: "versoes_catalogo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entes_monitorados_ente",
                table: "entes_monitorados",
                column: "ente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_etapas_execucao_consulta_id",
                table: "etapas_execucao",
                column: "consulta_id");

            migrationBuilder.CreateIndex(
                name: "IX_execucoes_coleta_fonte_id_iniciada_em",
                table: "execucoes_coleta",
                columns: new[] { "fonte_id", "iniciada_em" });

            migrationBuilder.CreateIndex(
                name: "IX_fontes_versao_catalogo_id_codigo",
                table: "fontes",
                columns: new[] { "versao_catalogo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_novidades_aguardando_classificacao_ia",
                table: "novidades",
                column: "aguardando_classificacao_ia",
                filter: "aguardando_classificacao_ia");

            migrationBuilder.CreateIndex(
                name: "IX_novidades_chave_deduplicacao",
                table: "novidades",
                column: "chave_deduplicacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_novidades_data_publicacao",
                table: "novidades",
                column: "data_publicacao");

            migrationBuilder.CreateIndex(
                name: "ux_ocorrencia_principal",
                table: "ocorrencias_novidade",
                column: "novidade_id",
                unique: true,
                filter: "principal");

            migrationBuilder.CreateIndex(
                name: "IX_registros_consumo_execucao_id",
                table: "registros_consumo",
                column: "execucao_id");

            migrationBuilder.CreateIndex(
                name: "IX_revisoes_novidade_novidade_id",
                table: "revisoes_novidade",
                column: "novidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_preco_modelo_vigente_desde",
                table: "tabelas_preco",
                columns: new[] { "modelo", "vigente_desde" });

            migrationBuilder.CreateIndex(
                name: "IX_versoes_catalogo_versao",
                table: "versoes_catalogo",
                column: "versao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_versoes_catalogo_vigente",
                table: "versoes_catalogo",
                column: "vigente",
                unique: true,
                filter: "vigente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entes_monitorados");

            migrationBuilder.DropTable(
                name: "etapas_execucao");

            migrationBuilder.DropTable(
                name: "execucoes_coleta");

            migrationBuilder.DropTable(
                name: "fontes");

            migrationBuilder.DropTable(
                name: "ocorrencias_novidade");

            migrationBuilder.DropTable(
                name: "registros_consumo");

            migrationBuilder.DropTable(
                name: "revisoes_novidade");

            migrationBuilder.DropTable(
                name: "tabelas_preco");

            migrationBuilder.DropTable(
                name: "consultas");

            migrationBuilder.DropTable(
                name: "versoes_catalogo");

            migrationBuilder.DropTable(
                name: "novidades");
        }
    }
}
