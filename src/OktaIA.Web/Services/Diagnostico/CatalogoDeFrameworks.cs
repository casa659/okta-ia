namespace OktaIA.Web.Services.Diagnostico;

/// <summary>
/// Os frameworks que o questionário toca, e quais perguntas tocam cada um.
///
/// ⚠️ DERIVADO DO CATÁLOGO, NUNCA UMA SEGUNDA LISTA. Cada pergunta já declara os controles que
/// toca em <see cref="PerguntaDoDiagnostico.Frameworks"/> ("CIS 4.4", "ISO A.8.20", "NIST PR.AC-5",
/// "LGPD art. 46"). Escrever aqui uma lista paralela de frameworks criaria duas versões da mesma
/// verdade: no dia em que alguém etiquetasse uma pergunta com um framework novo, ele não apareceria
/// no menu — e ninguém descobriria, porque a tela continuaria funcionando.
///
/// Por isso um prefixo desconhecido **entra na lista assim mesmo**, com o próprio prefixo por nome.
/// Aparecer feio é muito melhor que sumir.
///
/// ⚠️ ISTO NÃO É CONFORMIDADE, e a palavra não aparece em lugar nenhum do que sai daqui. O que a
/// planilha leva são as perguntas do NOSSO catálogo que tocam controles do framework — uma fatia
/// estreita do que um auditor avalia. Dizer "questionário ISO 27001" é a alegação que o auditor do
/// cliente derruba na primeira pergunta, e junto com ela cai a confiança no documento inteiro.
/// </summary>
public static class CatalogoDeFrameworks
{
    /// <param name="Prefixo">A primeira palavra da etiqueta: <c>CIS</c>, <c>ISO</c>, <c>NIST</c>…</param>
    /// <param name="Perguntas">Quantas perguntas do catálogo tocam este framework.</param>
    /// <param name="Controles">Quantos controles distintos dele são tocados.</param>
    public record Framework(string Prefixo, string Nome, string Resumo, int Perguntas, int Controles);

    /// <summary>Uma linha do questionário de um framework: a pergunta, o domínio e os controles tocados.</summary>
    public record ItemDoQuestionario(
        DominioDeSeguranca Dominio, PerguntaDoDiagnostico Pergunta, string[] Controles);

    /// <summary>
    /// Nome de exibição por prefixo. É só rótulo — quem decide a EXISTÊNCIA do framework no menu é
    /// o catálogo de perguntas, não este dicionário.
    /// </summary>
    private static readonly Dictionary<string, (string Nome, string Resumo)> Rotulos = new()
    {
        ["CIS"] = ("CIS Controls v8",
            "Os controles do Center for Internet Security, na ordem em que reduzem risco. "
            + "É o mais operacional dos quatro: fala de configuração, não de política."),
        ["ISO"] = ("ISO/IEC 27001:2022 · Anexo A",
            "Os controles do Anexo A da norma. É o vocabulário que auditoria e certificação usam, "
            + "e o que um cliente com contrato corporativo costuma ter de demonstrar."),
        ["NIST"] = ("NIST Cybersecurity Framework",
            "As subcategorias do framework do NIST, organizadas por função (Identificar, Proteger, "
            + "Detectar, Responder, Recuperar). É a linguagem de quem conversa com a diretoria."),
        ["LGPD"] = ("LGPD · Lei nº 13.709/2018",
            "Os artigos da lei que pedem medida técnica de segurança. Não cobre a lei inteira: "
            + "cobre a parte que um levantamento técnico consegue observar."),
    };

    public static IReadOnlyList<Framework> Todos { get; }

    static CatalogoDeFrameworks()
    {
        Todos = CatalogoDeDominios.Todos
            .SelectMany(d => d.Perguntas)
            .SelectMany(p => p.Frameworks.Select(f => new { Pergunta = p, Etiqueta = f }))
            .Select(x => new { x.Pergunta, x.Etiqueta, Prefixo = PrefixoDe(x.Etiqueta) })
            .Where(x => x.Prefixo.Length > 0)
            .GroupBy(x => x.Prefixo)
            .Select(g => new Framework(
                g.Key,
                Rotulos.TryGetValue(g.Key, out var r) ? r.Nome : g.Key,
                Rotulos.TryGetValue(g.Key, out var r2) ? r2.Resumo : "",
                g.Select(x => x.Pergunta.Codigo).Distinct().Count(),
                g.Select(x => x.Etiqueta).Distinct().Count()))
            // Mais perguntas primeiro: é o framework sobre o qual temos mais a dizer.
            .OrderByDescending(f => f.Perguntas).ThenBy(f => f.Nome)
            .ToList();
    }

    /// <summary>"NIST PR.AC-5" → "NIST". Etiqueta sem espaço vira o prefixo inteiro.</summary>
    private static string PrefixoDe(string etiqueta)
    {
        var t = (etiqueta ?? "").Trim();
        var i = t.IndexOf(' ');
        return i < 0 ? t : t[..i];
    }

    public static Framework? Buscar(string? prefixo) =>
        prefixo is null ? null
        : Todos.FirstOrDefault(f => string.Equals(f.Prefixo, prefixo, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// As perguntas que tocam este framework, na ordem do levantamento — a mesma ordem da tela, de
    /// propósito: quem preenche a planilha e depois abre o diagnóstico encontra as coisas no
    /// mesmo lugar.
    ///
    /// ⚠️ Só as perguntas ETIQUETADAS. Uma planilha que trouxesse o catálogo inteiro sob o nome de
    /// um framework afirmaria que todas aquelas perguntas vêm dele — e ninguém teria como conferir.
    /// </summary>
    public static IReadOnlyList<ItemDoQuestionario> QuestionarioDe(string prefixo)
    {
        var alvo = Buscar(prefixo);
        if (alvo is null) { return []; }

        return CatalogoDeDominios.Todos
            .OrderBy(d => d.Ordem)
            .SelectMany(d => d.Perguntas.Select(p => new { Dominio = d, Pergunta = p }))
            .Select(x => new
            {
                x.Dominio,
                x.Pergunta,
                Controles = x.Pergunta.Frameworks
                    .Where(f => string.Equals(PrefixoDe(f), alvo.Prefixo, StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
            })
            .Where(x => x.Controles.Length > 0)
            .Select(x => new ItemDoQuestionario(x.Dominio, x.Pergunta, x.Controles))
            .ToList();
    }
}
