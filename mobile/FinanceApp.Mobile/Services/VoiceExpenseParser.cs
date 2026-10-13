using System.Globalization;
using System.Text.RegularExpressions;
using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// O que o parser consegue extrair de uma frase em pt-BR como "gastei 50 reais no mercado ontem no cartão".
/// Campos que não dão para inferir ficam nulos: a tela de confirmação dá ao usuário a chance de completar.
/// </summary>
public sealed record ParsedVoiceExpense(
    decimal? Amount,
    string Description,
    string? CategoryHint,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    TransactionType Type);

/// <summary>
/// Converte uma fala curta em um rascunho de lançamento. Totalmente offline (regras + regex): nenhuma
/// chamada externa é feita, e o reconhecimento de voz continua acontecendo no dispositivo.
/// Projetado para pt-BR; o inglês cai para o fluxo manual.
/// </summary>
public static class VoiceExpenseParser
{
    private static readonly string[] IncomeHints =
    {
        "recebi", "ganhei", "entrou", "caiu", "salário", "salario", "pagamento", "pix recebido", "crédito em conta"
    };

    private static readonly string[] FillerPhrases =
    {
        "gastei", "paguei", "comprei", "foi", "era", "reais", "real", "no", "na", "em", "de", "do", "da",
        "um", "uma", "pelo", "pela", "ontem", "hoje", "anteontem", "r\\$"
    };

    private static readonly (string Keyword, string Category)[] CategoryMap =
    {
        ("mercado", "Alimentação"), ("supermercado", "Alimentação"), ("padaria", "Alimentação"),
        ("açougue", "Alimentação"), ("feira", "Alimentação"), ("pão de açucar", "Alimentação"),
        ("pao de acucar", "Alimentação"), ("carrefour", "Alimentação"), ("extra", "Alimentação"),

        ("uber", "Transporte"), ("99", "Transporte"), ("táxi", "Transporte"), ("taxi", "Transporte"),
        ("gasolina", "Transporte"), ("combustível", "Transporte"), ("combustivel", "Transporte"),
        ("posto", "Transporte"), ("passagem", "Transporte"), ("ônibus", "Transporte"), ("onibus", "Transporte"),
        ("metrô", "Transporte"), ("metro", "Transporte"), ("estacionamento", "Transporte"), ("pedágio", "Transporte"),
        ("pedagio", "Transporte"),

        ("restaurante", "Restaurantes"), ("almoço", "Restaurantes"), ("almoco", "Restaurantes"),
        ("janta", "Restaurantes"), ("jantar", "Restaurantes"), ("lanche", "Restaurantes"),
        ("ifood", "Restaurantes"), ("rappi", "Restaurantes"), ("delivery", "Restaurantes"),
        ("mcdonalds", "Restaurantes"), ("burger", "Restaurantes"), ("pizza", "Restaurantes"),

        ("farmácia", "Saúde"), ("farmacia", "Saúde"), ("remédio", "Saúde"), ("remedio", "Saúde"),
        ("médico", "Saúde"), ("medico", "Saúde"), ("consulta", "Saúde"), ("hospital", "Saúde"),
        ("dentista", "Saúde"), ("exame", "Saúde"),

        ("aluguel", "Moradia"), ("condomínio", "Moradia"), ("condominio", "Moradia"),
        ("iptu", "Moradia"), ("gás", "Moradia"), ("gas", "Moradia"), ("água", "Contas"),
        ("agua", "Contas"), ("luz", "Contas"), ("energia", "Contas"), ("internet", "Contas"),
        ("telefone", "Contas"), ("celular", "Contas"),

        ("netflix", "Assinaturas"), ("spotify", "Assinaturas"), ("disney", "Assinaturas"),
        ("amazon", "Assinaturas"), ("prime", "Assinaturas"), ("youtube", "Assinaturas"),

        ("escola", "Educação"), ("faculdade", "Educação"), ("curso", "Educação"), ("livro", "Educação"),

        ("shopping", "Compras"), ("roupa", "Compras"), ("sapato", "Compras"), ("presente", "Compras"),

        ("cinema", "Lazer"), ("show", "Lazer"), ("bar", "Lazer"), ("cerveja", "Lazer"),

        ("salário", "Salário"), ("salario", "Salário"),
    };

    private static readonly (string Keyword, PaymentMethod Method)[] PaymentMap =
    {
        ("crédito", PaymentMethod.Credit), ("credito", PaymentMethod.Credit),
        ("cartão de crédito", PaymentMethod.Credit), ("cartao de credito", PaymentMethod.Credit),
        ("débito", PaymentMethod.Debit), ("debito", PaymentMethod.Debit),
        ("cartão de débito", PaymentMethod.Debit), ("cartao de debito", PaymentMethod.Debit),
        // "cartão" sozinho fica como Credit (uso mais comum para parcelado); vem depois dos específicos.
        ("cartão", PaymentMethod.Credit), ("cartao", PaymentMethod.Credit),
        ("pix", PaymentMethod.Pix),
        ("dinheiro", PaymentMethod.Cash), ("espécie", PaymentMethod.Cash), ("especie", PaymentMethod.Cash),
        ("boleto", PaymentMethod.Boleto),
        ("transferência", PaymentMethod.BankTransfer), ("transferencia", PaymentMethod.BankTransfer),
        ("ted", PaymentMethod.BankTransfer), ("doc", PaymentMethod.BankTransfer),
    };

    // Mapa reduzido de palavras-número ("vinte e cinco") → valor, suficiente para quantias de uso diário.
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["um"] = 1, ["uma"] = 1, ["dois"] = 2, ["duas"] = 2, ["três"] = 3, ["tres"] = 3,
        ["quatro"] = 4, ["cinco"] = 5, ["seis"] = 6, ["sete"] = 7, ["oito"] = 8, ["nove"] = 9,
        ["dez"] = 10, ["onze"] = 11, ["doze"] = 12, ["treze"] = 13, ["quatorze"] = 14, ["catorze"] = 14,
        ["quinze"] = 15, ["dezesseis"] = 16, ["dezessete"] = 17, ["dezoito"] = 18, ["dezenove"] = 19,
        ["vinte"] = 20, ["trinta"] = 30, ["quarenta"] = 40, ["cinquenta"] = 50, ["cinqüenta"] = 50,
        ["sessenta"] = 60, ["setenta"] = 70, ["oitenta"] = 80, ["noventa"] = 90,
        ["cem"] = 100, ["cento"] = 100, ["duzentos"] = 200, ["trezentos"] = 300, ["quatrocentos"] = 400,
        ["quinhentos"] = 500, ["seiscentos"] = 600, ["setecentos"] = 700, ["oitocentos"] = 800, ["novecentos"] = 900,
        ["mil"] = 1000,
    };

    public static ParsedVoiceExpense Parse(string spoken, DateOnly today)
    {
        var text = (spoken ?? string.Empty).Trim();
        if (text.Length == 0)
            return new ParsedVoiceExpense(null, string.Empty, null, null, today, TransactionType.Expense);

        var lower = text.ToLowerInvariant();

        var amount = TryParseAmount(lower);
        var type = IncomeHints.Any(h => lower.Contains(h, StringComparison.OrdinalIgnoreCase))
            ? TransactionType.Income
            : TransactionType.Expense;
        var method = TryMatchPayment(lower);
        var category = TryMatchCategory(lower);
        var date = ParseDate(lower, today);
        var description = CleanDescription(text, lower);

        // Se nenhuma descrição sobrou, usa a categoria (ou um rótulo neutro) para o rascunho.
        if (string.IsNullOrWhiteSpace(description))
            description = category ?? (type == TransactionType.Income ? "Entrada" : "Gasto");

        return new ParsedVoiceExpense(amount, description, category, method, date, type);
    }

    private static decimal? TryParseAmount(string text)
    {
        // 1) Dígitos: captura o maior bloco contíguo de números/separadores em volta de uma marca de dinheiro,
        //    de modo que "aluguel 2000 reais" vire 2000 e "1.250,75" vire 1250,75. Anos ("em 2026") só são
        //    descartados quando NÃO há marca de dinheiro na frase.
        var hasMoneyWord = Regex.IsMatch(text, @"r\$|reais|real", RegexOptions.IgnoreCase);
        foreach (Match m in Regex.Matches(text, @"\d[\d.,]*"))
        {
            var raw = m.Value.TrimEnd('.', ',');
            if (raw.Length == 0) continue;
            if (!hasMoneyWord && raw.Length == 4 && !raw.Contains(',') && !raw.Contains('.')) continue;
            if (TryParseDecimalPtBr(raw, out var value)) return value;
        }

        // 2) Palavras: "cinquenta reais", "mil e quinhentos reais".
        return TryParseWordAmount(text);
    }

    private static bool TryParseDecimalPtBr(string raw, out decimal value)
    {
        // Regras pt-BR x US:
        //   "1.250,75" (BR com milhar+decimal), "1250,75" (BR decimal), "1.250" (BR só milhar),
        //   "1250.75" (US decimal) e "1250" (inteiro).
        string normalized;
        if (raw.Contains(','))
        {
            normalized = raw.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (raw.Contains('.'))
        {
            // Um único ponto com 1–2 dígitos no fim = decimal; o resto são milhares.
            var dot = raw.LastIndexOf('.');
            normalized = raw.Count(c => c == '.') == 1 && raw.Length - dot - 1 is 1 or 2
                ? raw
                : raw.Replace(".", string.Empty);
        }
        else
        {
            normalized = raw;
        }
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static decimal? TryParseWordAmount(string text)
    {
        // Varredura esquerda→direita, como pt-BR fala "mil e quinhentos" = 1500.
        var tokens = Regex.Matches(text, @"[a-zãáéíóúâêôçü]+", RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant())
            .ToArray();

        for (int start = 0; start < tokens.Length; start++)
        {
            if (!NumberWords.ContainsKey(tokens[start])) continue;

            int acc = 0, group = 0;
            int end = start;
            bool sawReais = false;
            while (end < tokens.Length)
            {
                var w = tokens[end];
                if (w is "e") { end++; continue; }
                if (w is "reais" or "real") { sawReais = true; end++; break; }

                if (!NumberWords.TryGetValue(w, out var n)) break;
                if (n >= 1000) { acc += (group == 0 ? 1 : group) * n; group = 0; }
                else { group += n; }   // dezenas/unidades/centenas se somam no grupo atual
                end++;
            }
            if (sawReais) return acc + group;
        }
        return null;
    }

    private static PaymentMethod? TryMatchPayment(string lower)
    {
        foreach (var (keyword, method) in PaymentMap)
            if (lower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return method;
        return null;
    }

    private static string? TryMatchCategory(string lower)
    {
        foreach (var (keyword, category) in CategoryMap)
            if (lower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return category;
        return null;
    }

    private static DateOnly ParseDate(string lower, DateOnly today)
    {
        if (Regex.IsMatch(lower, @"\banteontem\b")) return today.AddDays(-2);
        if (Regex.IsMatch(lower, @"\bontem\b")) return today.AddDays(-1);
        if (Regex.IsMatch(lower, @"\bhoje\b|\bagora\b")) return today;

        // "sexta passada", "última segunda" → ocorrência anterior do dia da semana.
        var dayMap = new Dictionary<string, DayOfWeek>
        {
            ["domingo"] = DayOfWeek.Sunday, ["segunda"] = DayOfWeek.Monday,
            ["terça"] = DayOfWeek.Tuesday, ["terca"] = DayOfWeek.Tuesday,
            ["quarta"] = DayOfWeek.Wednesday, ["quinta"] = DayOfWeek.Thursday,
            ["sexta"] = DayOfWeek.Friday, ["sábado"] = DayOfWeek.Saturday, ["sabado"] = DayOfWeek.Saturday,
        };
        foreach (var (word, dow) in dayMap)
        {
            if (!Regex.IsMatch(lower, $@"\b{word}\b")) continue;
            if (!Regex.IsMatch(lower, $@"\b(?:última|ultima|passad[ao])\b")) continue;

            int diff = (int)today.DayOfWeek - (int)dow;
            if (diff <= 0) diff += 7;
            return today.AddDays(-diff);
        }

        return today;
    }

    private static string CleanDescription(string original, string lower)
    {
        // Remove números, R$ e preenchedores; mantém o resto como descrição.
        var withoutNumbers = Regex.Replace(original, @"(r\$\s*)?\d[\d.,]*", " ", RegexOptions.IgnoreCase);
        var tokens = Regex.Matches(withoutNumbers, @"[\p{L}]+").Select(m => m.Value);
        var filtered = new List<string>();
        foreach (var token in tokens)
        {
            var t = token.ToLowerInvariant();
            if (FillerPhrases.Contains(t)) continue;
            if (NumberWords.ContainsKey(t)) continue;
            // Palavras de forma de pagamento, data e verbo já estão representadas em campos próprios.
            if (PaymentMap.Any(p => p.Keyword == t)) continue;
            if (t is "ontem" or "hoje" or "anteontem" or "passada" or "passado" or "última" or "ultima" or "e") continue;

            filtered.Add(char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant());
        }
        return string.Join(' ', filtered).Trim();
    }
}
