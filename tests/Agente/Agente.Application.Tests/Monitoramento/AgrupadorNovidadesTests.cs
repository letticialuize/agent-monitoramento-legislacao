using Agente.Application.Comum.Envelope;
using Agente.Domain.Comum;

namespace Agente.Application.Tests.Monitoramento;

/// <summary>FR-011: novidades agrupadas por perfil, esfera e situação.</summary>
public class AgrupadorNovidadesTests
{
    private static NovidadeDto Novidade(Esfera esfera, SituacaoNovidade situacao, params Perfil[] perfis) =>
        Montagem.Novidade(situacao) with { Esfera = esfera, Perfis = perfis };

    [Fact]
    public void Agrupa_na_ordem_perfil_esfera_situacao()
    {
        var pendente = Novidade(Esfera.Federal, SituacaoNovidade.PendenteConfirmacao, Perfil.ConsultorTributario);
        var confirmada = Novidade(Esfera.Federal, SituacaoNovidade.Confirmada, Perfil.ConsultorTributario);
        var municipal = Novidade(Esfera.Municipal, SituacaoNovidade.Confirmada, Perfil.ConsultorTributario);
        var estadual = Novidade(Esfera.Estadual, SituacaoNovidade.Confirmada, Perfil.Desenvolvedor);

        var grupos = AgrupadorNovidades.Agrupar([pendente, municipal, estadual, confirmada], []);

        Assert.Equal([Perfil.ConsultorTributario, Perfil.Desenvolvedor], grupos.Select(g => g.Perfil));
        var consultor = grupos[0];
        Assert.Equal(3, consultor.Total);
        Assert.Equal([Esfera.Federal, Esfera.Municipal], consultor.Esferas.Select(e => e.Esfera));
        Assert.Equal(2, consultor.Esferas[0].Total);
        Assert.Equal([SituacaoNovidade.Confirmada, SituacaoNovidade.PendenteConfirmacao], consultor.Esferas[0].Situacoes.Select(s => s.Situacao));
        Assert.Equal(confirmada.Id, Assert.Single(consultor.Esferas[0].Situacoes[0].Novidades).Id);
        Assert.Equal(estadual.Id, Assert.Single(Assert.Single(grupos[1].Esferas).Situacoes[0].Novidades).Id);
    }

    [Fact]
    public void Novidade_com_dois_perfis_aparece_nos_dois_grupos()
    {
        var ambos = Novidade(Esfera.Federal, SituacaoNovidade.Confirmada, Perfil.ConsultorTributario, Perfil.Desenvolvedor);

        var grupos = AgrupadorNovidades.Agrupar([ambos], []);

        Assert.Equal(2, grupos.Count);
        Assert.All(grupos, g => Assert.Equal(1, g.Total));
    }

    [Fact]
    public void Filtro_de_perfil_mostra_so_o_grupo_pedido()
    {
        var ambos = Novidade(Esfera.Federal, SituacaoNovidade.Confirmada, Perfil.ConsultorTributario, Perfil.Desenvolvedor);

        var grupo = Assert.Single(AgrupadorNovidades.Agrupar([ambos], [Perfil.Desenvolvedor]));

        Assert.Equal(Perfil.Desenvolvedor, grupo.Perfil);
    }

    [Fact]
    public void Sem_novidades_nao_ha_grupos() => Assert.Empty(AgrupadorNovidades.Agrupar([], []));

    [Fact]
    public void Preserva_a_ordem_recebida_dentro_do_grupo()
    {
        var a = Novidade(Esfera.Federal, SituacaoNovidade.Confirmada, Perfil.Desenvolvedor);
        var b = Novidade(Esfera.Federal, SituacaoNovidade.Confirmada, Perfil.Desenvolvedor);

        var novidades = AgrupadorNovidades.Agrupar([b, a], [])[0].Esferas[0].Situacoes[0].Novidades;

        Assert.Equal([b.Id, a.Id], novidades.Select(n => n.Id));
    }
}
