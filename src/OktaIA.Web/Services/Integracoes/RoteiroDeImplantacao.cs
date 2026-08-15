namespace OktaIA.Web.Services.Integracoes;

/// <summary>Como a plataforma alcança o produto — muda tudo no roteiro.</summary>
public enum ModoDeAcesso
{
    /// <summary>Produto instalado na casa do cliente. Exige endereço + liberação de rede.</summary>
    InstaladoNoCliente,

    /// <summary>Produto em nuvem do fabricante. Não há firewall a liberar; há consentimento a conceder.</summary>
    NuvemDoFabricante,
}

/// <summary>
/// Roteiro de implantação de um fabricante, em dois públicos: o que o CLIENTE provisiona e o que o
/// TÉCNICO executa. Vira PDF nos dois formatos.
///
/// Fica como dado, e não como texto solto numa tela, por dois motivos: o documento do cliente é
/// enviado por e-mail antes da reunião técnica, e o roteiro precisa acompanhar o fabricante — quando
/// um adaptador novo for implementado, o roteiro dele entra junto e a documentação não envelhece.
///
/// <see cref="Implementado"/> separa o que a plataforma já integra do que é preparação comercial.
/// Documento de fabricante não implementado sai carimbado — sem isso, alguém enviaria a um cliente um
/// passo a passo de algo que ainda não conectamos.
/// </summary>
public record RoteiroDeImplantacao(
    string Slug,
    string Fabricante,
    string Categoria,
    bool Implementado,
    ModoDeAcesso Modo,

    /// <summary>Onde o dado que nos interessa realmente mora. É o erro mais comum de levantamento.</summary>
    string OndeEstaOAlerta,

    /// <summary>Passos que o CLIENTE executa no console dele.</summary>
    IReadOnlyList<string> PassosCliente,

    /// <summary>O que o cliente devolve ao técnico ao final.</summary>
    IReadOnlyList<string> InformacoesParaEnviar,

    /// <summary>Passos que o TÉCNICO executa, na plataforma e na conferência.</summary>
    IReadOnlyList<string> PassosTecnico,

    /// <summary>Armadilhas conhecidas e ressalvas honestas.</summary>
    IReadOnlyList<string> Observacoes);
