using Agente.Domain.Catalogo;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agente.Infrastructure.Persistencia.Configuracoes;

internal static class ExtensoesConfiguracao
{
    public static PropertyBuilder<IReadOnlyList<T>> ComoListaJson<T>(this PropertyBuilder<IReadOnlyList<T>> p) =>
        p.HasConversion(Conversores.ListaJson<T>(), Conversores.ComparadorLista<T>()).HasColumnType("jsonb");

    public static void Auditoria<T>(this EntityTypeBuilder<T> b) where T : RaizAgregado
    {
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
    }
}

internal sealed class VersaoCatalogoConfig : IEntityTypeConfiguration<VersaoCatalogo>
{
    public void Configure(EntityTypeBuilder<VersaoCatalogo> b)
    {
        b.ToTable("versoes_catalogo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Versao).HasColumnName("versao").HasMaxLength(50);
        b.Property(x => x.DocumentoOrigem).HasColumnName("documento_origem").HasMaxLength(500);
        b.Property(x => x.AprovadoPor).HasColumnName("aprovado_por").HasMaxLength(200);
        b.Property(x => x.Vigente).HasColumnName("vigente");
        b.Auditoria();
        b.HasIndex(x => x.Versao).IsUnique();
        // Só uma versão vigente.
        b.HasIndex(x => x.Vigente).IsUnique().HasFilter("vigente").HasDatabaseName("ux_versoes_catalogo_vigente");
        b.HasMany(x => x.Fontes).WithOne().HasForeignKey(f => f.VersaoCatalogoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Fontes).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class FonteConfig : IEntityTypeConfiguration<Fonte>
{
    public void Configure(EntityTypeBuilder<Fonte> b)
    {
        b.ToTable("fontes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.VersaoCatalogoId).HasColumnName("versao_catalogo_id");
        b.Property(x => x.Codigo).HasColumnName("codigo").HasConversion(Conversores.CodigoFonte).HasMaxLength(64);
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300);
        b.Property(x => x.LinkOficial).HasColumnName("link_oficial").HasConversion(Conversores.LinkOficial).HasMaxLength(1000);
        b.Property(x => x.OQueMonitorar).HasColumnName("o_que_monitorar").HasMaxLength(1000);
        b.Property(x => x.OrgaoEmissor).HasColumnName("orgao_emissor").HasMaxLength(300);
        b.Property(x => x.Esfera).HasColumnName("esfera").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Ente).HasColumnName("ente").HasConversion(Conversores.Ente).HasMaxLength(120);
        b.Property(x => x.Grupo).HasColumnName("grupo").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Natureza).HasColumnName("natureza").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ResponsavelInterno).HasColumnName("responsavel_interno").ComoListaJson();
        b.Property(x => x.Formatos).HasColumnName("formatos").ComoListaJson();
        b.Property(x => x.TipoConector).HasColumnName("tipo_conector").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ConfiguracaoConector).HasColumnName("configuracao_conector").HasColumnType("jsonb");
        b.Property(x => x.Periodicidade).HasColumnName("periodicidade").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Ativa).HasColumnName("ativa");
        b.Property(x => x.ExcecaoDominioAprovadaPor).HasColumnName("excecao_dominio_aprovada_por").HasMaxLength(200);
        b.HasIndex(x => new { x.VersaoCatalogoId, x.Codigo }).IsUnique();
    }
}

internal sealed class EnteMonitoradoConfig : IEntityTypeConfiguration<EnteMonitorado>
{
    public void Configure(EntityTypeBuilder<EnteMonitorado> b)
    {
        b.ToTable("entes_monitorados");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Ente).HasColumnName("ente").HasConversion(Conversores.EnteObrigatorio).HasMaxLength(120);
        b.Property(x => x.Ativo).HasColumnName("ativo");
        b.Auditoria();
        b.HasIndex(x => x.Ente).IsUnique();
    }
}

internal sealed class NovidadeConfig : IEntityTypeConfiguration<Novidade>
{
    public void Configure(EntityTypeBuilder<Novidade> b)
    {
        b.ToTable("novidades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.ChaveDeduplicacao).HasColumnName("chave_deduplicacao").HasConversion(Conversores.Chave).HasMaxLength(300);
        b.Property(x => x.DataPublicacao).HasColumnName("data_publicacao");
        b.Property(x => x.OrgaoEmissor).HasColumnName("orgao_emissor").HasMaxLength(300);
        b.Property(x => x.TipoAto).HasColumnName("tipo_ato").HasMaxLength(200);
        b.Property(x => x.NumeroAto).HasColumnName("numero_ato").HasMaxLength(100);
        b.Property(x => x.Ementa).HasColumnName("ementa");
        b.Property(x => x.Trecho).HasColumnName("trecho");
        b.Property(x => x.LinkOficial).HasColumnName("link_oficial").HasMaxLength(2000);
        b.Property(x => x.Esfera).HasColumnName("esfera").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Ente).HasColumnName("ente").HasConversion(Conversores.Ente).HasMaxLength(120);
        b.Property(x => x.Grupo).HasColumnName("grupo").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Tema).HasColumnName("tema").HasMaxLength(200);
        b.Property(x => x.Impacto).HasColumnName("impacto").HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Perfis).HasColumnName("perfis").ComoListaJson();
        b.Property(x => x.OrigemPerfil).HasColumnName("origem_perfil").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.OrigemImpacto).HasColumnName("origem_impacto").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PerfilDecidido).HasColumnName("perfil_decidido");
        b.Property(x => x.RelacionadaTributos).HasColumnName("relacionada_tributos");
        b.Property(x => x.AguardandoClassificacaoIa).HasColumnName("aguardando_classificacao_ia");
        b.Property(x => x.Situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.RetificaNovidadeId).HasColumnName("retifica_novidade_id");
        b.Property(x => x.ResponsavelInterno).HasColumnName("responsavel_interno").ComoListaJson();
        b.Property(x => x.HashConteudo).HasColumnName("hash_conteudo").HasMaxLength(64);
        b.Property(x => x.ColetadaEm).HasColumnName("coletada_em");
        b.Auditoria();
        b.Ignore(x => x.RequerRevisaoHumana);
        b.Ignore(x => x.EstaPendenteDeRevisao);

        b.HasIndex(x => x.ChaveDeduplicacao).IsUnique();
        b.HasIndex(x => x.DataPublicacao);
        b.HasIndex(x => x.AguardandoClassificacaoIa).HasFilter("aguardando_classificacao_ia");

        b.HasMany(x => x.Ocorrencias).WithOne().HasForeignKey(o => o.NovidadeId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Ocorrencias).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.Revisoes).WithOne().HasForeignKey(r => r.NovidadeId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Revisoes).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class OcorrenciaConfig : IEntityTypeConfiguration<OcorrenciaNovidade>
{
    public void Configure(EntityTypeBuilder<OcorrenciaNovidade> b)
    {
        b.ToTable("ocorrencias_novidade");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.NovidadeId).HasColumnName("novidade_id");
        b.Property(x => x.FonteId).HasColumnName("fonte_id");
        b.Property(x => x.Natureza).HasColumnName("natureza").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000);
        b.Property(x => x.ExecucaoColetaId).HasColumnName("execucao_coleta_id");
        b.Property(x => x.ColetadaEm).HasColumnName("coletada_em");
        b.Property(x => x.Principal).HasColumnName("principal");
        b.HasIndex(x => x.NovidadeId).IsUnique().HasFilter("principal").HasDatabaseName("ux_ocorrencia_principal");
    }
}

internal sealed class RevisaoConfig : IEntityTypeConfiguration<RevisaoNovidade>
{
    public void Configure(EntityTypeBuilder<RevisaoNovidade> b)
    {
        b.ToTable("revisoes_novidade");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.NovidadeId).HasColumnName("novidade_id");
        b.Property(x => x.RevisorId).HasColumnName("revisor_id").HasMaxLength(200);
        b.Property(x => x.Decisao).HasColumnName("decisao").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(2000);
        b.Property(x => x.LinkOficialInformado).HasColumnName("link_oficial_informado").HasMaxLength(2000);
        b.Property(x => x.DecididaEm).HasColumnName("decidida_em");
    }
}

internal sealed class ExecucaoColetaConfig : IEntityTypeConfiguration<ExecucaoColeta>
{
    public void Configure(EntityTypeBuilder<ExecucaoColeta> b)
    {
        b.ToTable("execucoes_coleta");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CorrelacaoId).HasColumnName("correlacao_id").HasMaxLength(100);
        b.Property(x => x.FonteId).HasColumnName("fonte_id");
        b.Property(x => x.IniciadaEm).HasColumnName("iniciada_em");
        b.Property(x => x.FinalizadaEm).HasColumnName("finalizada_em");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Erro).HasColumnName("erro").HasMaxLength(2000);
        b.Property(x => x.NovidadesEncontradas).HasColumnName("novidades_encontradas");
        b.Auditoria();
        b.HasIndex(x => new { x.FonteId, x.IniciadaEm });
    }
}

internal sealed class ConsultaConfig : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> b)
    {
        b.ToTable("consultas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CorrelacaoId).HasColumnName("correlacao_id").HasMaxLength(100);
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id").HasMaxLength(200);
        b.Property(x => x.ConversaId).HasColumnName("conversa_id").HasMaxLength(100);
        b.Property(x => x.Assunto).HasColumnName("assunto").HasMaxLength(200);
        b.Property(x => x.PeriodoInicio).HasColumnName("periodo_inicio");
        b.Property(x => x.PeriodoFim).HasColumnName("periodo_fim");
        b.Property(x => x.Esferas).HasColumnName("esferas").ComoListaJson();
        b.Property(x => x.Perfis).HasColumnName("perfis").ComoListaJson();
        b.Property(x => x.Grupos).HasColumnName("grupos").ComoListaJson();
        b.Property(x => x.ClienteId).HasColumnName("cliente_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.RequerRevisaoHumana).HasColumnName("requer_revisao_humana");
        b.Property(x => x.Limitacoes).HasColumnName("limitacoes").ComoListaJson();
        b.Property(x => x.VersaoContrato).HasColumnName("versao_contrato").HasMaxLength(10);
        b.Property(x => x.FinalizadaEm).HasColumnName("finalizada_em");
        b.Auditoria();
        b.HasMany(x => x.Etapas).WithOne().HasForeignKey(e => e.ConsultaId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Etapas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EtapaConfig : IEntityTypeConfiguration<EtapaExecucao>
{
    public void Configure(EntityTypeBuilder<EtapaExecucao> b)
    {
        b.ToTable("etapas_execucao");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.ConsultaId).HasColumnName("consulta_id");
        b.Property(x => x.Etapa).HasColumnName("etapa").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.IniciadaEm).HasColumnName("iniciada_em");
        b.Property(x => x.FinalizadaEm).HasColumnName("finalizada_em");
        b.Property(x => x.Detalhe).HasColumnName("detalhe").HasMaxLength(2000);
    }
}

internal sealed class RegistroConsumoConfig : IEntityTypeConfiguration<RegistroConsumo>
{
    public void Configure(EntityTypeBuilder<RegistroConsumo> b)
    {
        b.ToTable("registros_consumo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.ExecucaoId).HasColumnName("execucao_id");
        b.Property(x => x.CorrelacaoId).HasColumnName("correlacao_id").HasMaxLength(100);
        b.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(50);
        b.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(100);
        b.Property(x => x.Finalidade).HasColumnName("finalidade").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Tentativa).HasColumnName("tentativa");
        b.Property(x => x.TokensEntrada).HasColumnName("tokens_entrada");
        b.Property(x => x.TokensSaida).HasColumnName("tokens_saida");
        b.Property(x => x.UsoConhecido).HasColumnName("uso_conhecido");
        b.Property(x => x.Inicio).HasColumnName("inicio");
        b.Property(x => x.DuracaoMs).HasColumnName("duracao_ms");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        b.Property(x => x.CustoEstimado).HasColumnName("custo_estimado").HasPrecision(18, 6);
        b.Property(x => x.CustoInformado).HasColumnName("custo_informado").HasPrecision(18, 6);
        b.Property(x => x.Moeda).HasColumnName("moeda").HasMaxLength(10);
        b.Property(x => x.TabelaPrecoId).HasColumnName("tabela_preco_id");
        b.Auditoria();
        b.HasIndex(x => x.ExecucaoId);
    }
}

internal sealed class TabelaPrecoConfig : IEntityTypeConfiguration<TabelaPreco>
{
    public void Configure(EntityTypeBuilder<TabelaPreco> b)
    {
        b.ToTable("tabelas_preco");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(100);
        b.Property(x => x.PrecoEntradaPorMilhao).HasColumnName("preco_entrada_por_milhao").HasPrecision(18, 6);
        b.Property(x => x.PrecoSaidaPorMilhao).HasColumnName("preco_saida_por_milhao").HasPrecision(18, 6);
        b.Property(x => x.Moeda).HasColumnName("moeda").HasMaxLength(10);
        b.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
        b.Property(x => x.UrlReferencia).HasColumnName("url_referencia").HasMaxLength(1000);
        b.Auditoria();
        b.HasIndex(x => new { x.Modelo, x.VigenteDesde });
    }
}
