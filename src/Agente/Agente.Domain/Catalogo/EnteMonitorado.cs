using Agente.Domain.Catalogo.ObjetosDeValor;
using Agente.Domain.Comum;

namespace Agente.Domain.Catalogo;

public enum TipoEnte { Uf, Municipio }

/// <summary>Agregado: UF ou município monitorado (FR-035).</summary>
public sealed class EnteMonitorado : RaizAgregado
{
    public TipoEnte Tipo { get; private set; }

    public Ente Ente { get; private set; } = null!;

    public bool Ativo { get; private set; }

    private EnteMonitorado() { }

    public static Resultado<EnteMonitorado> Criar(TipoEnte tipo, Ente ente, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(ente);
        if (tipo == TipoEnte.Municipio && ente.Municipio is null)
        {
            return Resultado<EnteMonitorado>.Falha("ente.municipio_obrigatorio", "Município é obrigatório para tipo município.");
        }

        if (tipo == TipoEnte.Uf && ente.Municipio is not null)
        {
            return Resultado<EnteMonitorado>.Falha("ente.municipio_indevido", "Ente do tipo UF não tem município.");
        }

        return Resultado<EnteMonitorado>.Ok(new EnteMonitorado
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Ente = ente,
            Ativo = false,
            CriadoEm = agora,
            AtualizadoEm = agora,
        });
    }

    /// <summary>Só ativa com ao menos uma fonte ativa do ente na versão vigente do catálogo.</summary>
    public Resultado Ativar(int quantidadeFontesAtivas, DateTimeOffset agora)
    {
        if (quantidadeFontesAtivas <= 0)
        {
            return Resultado.Falha("ente.sem_fontes", $"O ente {Ente} não tem fontes ativas no catálogo vigente.");
        }

        Ativo = true;
        Tocar(agora);
        return Resultado.Ok();
    }

    public void Desativar(DateTimeOffset agora)
    {
        Ativo = false;
        Tocar(agora);
    }
}
