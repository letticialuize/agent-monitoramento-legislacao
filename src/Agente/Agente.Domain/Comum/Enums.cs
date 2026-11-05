namespace Agente.Domain.Comum;

// Os nomes técnicos nos contratos JSON são a forma snake_case destes membros
// (ex.: ConsultorTributario → "consultor_tributario").

public enum Esfera { Federal, Estadual, Municipal }

public enum EsferaFonte { Federal, Estadual, Municipal, Nacional }

public enum Grupo
{
    LegislacaoFederal,
    LegislacaoEstadual,
    LegislacaoMunicipal,
    Tributos,
    TribunaisJudiciais,
    AdministrativosContas,
}

public enum NaturezaFonte { Oficial, AgregadorApoio }

public enum PapelResponsavel { AJ, AF, CC, TI }

public enum Formato { Html, Pdf, Xml, Json, Xsd }

public enum TipoConector
{
    Inlabs,
    SenadoDadosAbertos,
    CamaraDadosAbertos,
    LexmlOaiPmh,
    Djen,
    QueridoDiario,
    PaginaOficial,
}

public enum Periodicidade { DiariaDiasUteis, Diaria, Semanal, Quinzenal, Trimestral }

public enum Perfil { ConsultorTributario, Desenvolvedor }

public enum Origem { RegraDeterministica, IndicioIa }

public enum SituacaoNovidade
{
    Coletada,
    Confirmada,
    PendenteConfirmacao,
    FonteNaoCatalogada,
    EmAnalise,
    Descartada,
}

public enum Impacto { Alto, Medio, Baixo }

/// <summary>Cinco categorias obrigatórias de achados (Princípio IV).</summary>
public enum CategoriaAchado
{
    ErroEstrutural,
    InconsistenciaNegocio,
    IndicioIa,
    AusenciaEvidencia,
    FalhaTecnica,
}
