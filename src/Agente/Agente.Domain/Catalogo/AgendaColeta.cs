using Agente.Domain.Comum;

namespace Agente.Domain.Catalogo;

/// <summary>Decide se uma fonte está devida para coleta, conforme a periodicidade do catálogo.</summary>
public sealed class AgendaColeta
{
    public bool EstaDevida(Periodicidade periodicidade, DateOnly? ultimaColetaLocal, DateOnly hojeLocal)
    {
        if (periodicidade == Periodicidade.DiariaDiasUteis &&
            hojeLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        if (ultimaColetaLocal is null)
        {
            return true;
        }

        var dias = hojeLocal.DayNumber - ultimaColetaLocal.Value.DayNumber;
        return periodicidade switch
        {
            Periodicidade.DiariaDiasUteis or Periodicidade.Diaria => dias >= 1,
            Periodicidade.Semanal => dias >= 7,
            Periodicidade.Quinzenal => dias >= 15,
            Periodicidade.Trimestral => dias >= 90,
            _ => dias >= 1,
        };
    }
}
