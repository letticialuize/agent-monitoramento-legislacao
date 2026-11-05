namespace Agente.Application.Comum;

public sealed class OpcoesConsulta
{
    public int PeriodoPadraoDias { get; set; } = 7;

    public int LimiteItens { get; set; } = 50;
}

public sealed class OpcoesColeta
{
    public bool Habilitada { get; set; } = true;

    public int IntervaloVerificacaoMinutos { get; set; } = 30;

    public int TimeoutPadraoSegundos { get; set; } = 60;

    public int TentativasPorFonte { get; set; } = 2;

    /// <summary>Janela da primeira coleta de uma fonte sem histórico.</summary>
    public int JanelaInicialDias { get; set; } = 7;

    public int LoteClassificacaoIa { get; set; } = 20;

    public int MaxLotesClassificacaoIa { get; set; } = 10;

    public string ArquivoCatalogoInicial { get; set; } = string.Empty;

    public string AprovadorCatalogoInicial { get; set; } = string.Empty;
}

public sealed class OpcoesFerramentas
{
    public int TimeoutSegundos { get; set; } = 30;
}

public sealed class OpcoesMemoria
{
    public string BancoMongo { get; set; } = "agente";

    public int MaxMensagens { get; set; } = 10;

    public int TtlDias { get; set; } = 30;
}

public sealed class OpcoesValidacaoLlm
{
    public int RetentativasValidacao { get; set; } = 2;
}

public sealed class OpcoesRetencao
{
    public int Dias { get; set; } = 30;

    public string Horario { get; set; } = "02:00";
}
