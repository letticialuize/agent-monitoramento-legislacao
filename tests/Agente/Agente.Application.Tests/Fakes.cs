using Agente.Application.Comum;
using Agente.Application.Comum.Envelope;
using Agente.Application.Monitoramento;
using Agente.Application.Operacao;
using Agente.Application.Portas;
using Agente.Domain.Comum;
using Agente.Domain.Monitoramento;
using Agente.Domain.Operacao;
using Microsoft.Extensions.Options;

namespace Agente.Application.Tests;

internal sealed class RelogioFixo : IRelogio
{
    public DateTimeOffset AgoraUtc { get; set; } = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    public DateTimeOffset ParaLocal(DateTimeOffset instante) => instante.ToOffset(TimeSpan.FromHours(-3));

    public DateOnly HojeLocal => DateOnly.FromDateTime(ParaLocal(AgoraUtc).DateTime);
}

internal sealed class UnidadeFake : IUnidadeDeTrabalho
{
    public int Salvamentos { get; private set; }

    public Task SalvarAsync(CancellationToken ct)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }
}

internal sealed class ConsultasFake : IRepositorioConsultas
{
    public List<Consulta> Itens { get; } = [];

    public void Adicionar(Consulta consulta) => Itens.Add(consulta);
}

internal sealed class ConsumoFake : IConsultaConsumo
{
    public Task<ResumoConsumo> ResumirAsync(Guid execucaoId, CancellationToken ct) => Task.FromResult(ResumoConsumo.Nenhum);
}

internal sealed class LeituraFake : IConsultaNovidades
{
    public List<NovidadeDto> Itens { get; } = [];

    public FiltroNovidades? UltimoFiltro { get; private set; }

    public Task<PaginaNovidades> BuscarAsync(FiltroNovidades filtro, CancellationToken ct)
    {
        UltimoFiltro = filtro;
        return Task.FromResult(new PaginaNovidades(Itens.Take(filtro.Limite).ToList(), Itens.Count));
    }

    public Task<NovidadeDto?> ObterAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Itens.FirstOrDefault(n => n.Id == id && n.Situacao != SituacaoNovidade.Descartada));
}

internal sealed class SituacaoFontesFake : IConsultaSituacaoFontes
{
    public SituacaoFontes Situacao { get; set; } = new(["FED-SENADO"], [], []);

    public Task<SituacaoFontes> ObterAsync(DateOnly inicio, DateOnly fim, IReadOnlyList<Esfera> esferas, IReadOnlyList<Grupo> grupos, CancellationToken ct) =>
        Task.FromResult(Situacao);
}

internal sealed class RegrasFake : IProvedorRegras
{
    public RegrasPerfil Perfis { get; } = new("t", [], ["leiaute"], [Grupo.Tributos], []);

    public RegrasEsferaPorOrgao EsferaPorOrgao { get; } = new("t", Esfera.Federal, []);

    public DicionarioSinonimos Sinonimos { get; } = new("t", [["CBS", "Contribuição sobre Bens e Serviços"]]);
}

internal static class Montagem
{
    public static IOptions<T> Opcoes<T>(T valor) where T : class => Options.Create(valor);

    public static NovidadeDto Novidade(
        SituacaoNovidade situacao = SituacaoNovidade.Confirmada,
        Origem origemPerfil = Origem.RegraDeterministica,
        string url = "https://www.in.gov.br/ato-1") =>
        new(Guid.NewGuid(), situacao, Esfera.Federal, null, [Perfil.ConsultorTributario], origemPerfil,
            new DateOnly(2026, 10, 28), "Receita Federal", "Instrução Normativa", "2250", "Altera regras da CBS",
            null, null, null, null, null, [new EvidenciaDto("TRIB-RFB", url, true)],
            situacao != SituacaoNovidade.Confirmada || origemPerfil == Origem.IndicioIa);

    public static (ConsultarNovidades Caso, LeituraFake Leitura, SituacaoFontesFake Fontes, ConsultasFake Consultas) Consulta(int limite = 50)
    {
        var leitura = new LeituraFake();
        var fontes = new SituacaoFontesFake();
        var consultas = new ConsultasFake();
        var relogio = new RelogioFixo();
        var caso = new ConsultarNovidades(
            leitura, fontes, new ConsumoFake(), consultas, new UnidadeFake(), new RegrasFake(),
            new RegistradorEtapas(relogio), relogio, Opcoes(new OpcoesConsulta { LimiteItens = limite, PeriodoPadraoDias = 7 }));
        return (caso, leitura, fontes, consultas);
    }

    public static readonly ContextoUsuario Consultor = new("u1", [Papeis.Consultor]);
}
