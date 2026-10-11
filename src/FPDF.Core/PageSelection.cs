using System.Globalization;
using System.Text.RegularExpressions;

namespace FPDF.Core;

public record PageRange(int First, int Last)
{
    public int Count => Last - First + 1;
    public string Argument => First == Last ? First.ToString(CultureInfo.InvariantCulture) : $"{First}-{Last}";
}

public static class PageSelection
{
    public static IReadOnlyList<PageRange> Parse(string expression, int total, bool emptyMeansAll = false)
    {
        if (total < 1) throw new ArgumentOutOfRangeException(nameof(total));
        if (string.IsNullOrWhiteSpace(expression))
        {
            if (emptyMeansAll) return [new(1, total)];
            throw new ArgumentException("Informe as páginas. Exemplo: 1-3, 5, 8-10.");
        }
        if (expression.Length > 12_000) throw new ArgumentException("A seleção de páginas é muito longa.");
        var result = new List<PageRange>();
        foreach (var token in expression.Split(','))
        {
            var match = Regex.Match(token.Trim(), @"^(\d+|fim|end)(?:\s*-\s*(\d+|fim|end))?$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new ArgumentException($"Intervalo inválido: '{token}'. Use números separados por vírgula, como 1-3, 5, 8-fim.");
            int Resolve(string text)
            {
                if (text.Equals("fim", StringComparison.OrdinalIgnoreCase) || text.Equals("end", StringComparison.OrdinalIgnoreCase)) return total;
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int page) || page < 1 || page > total)
                    throw new ArgumentException($"A página '{text}' não existe. O documento tem {total} página(s).");
                return page;
            }
            int first = Resolve(match.Groups[1].Value);
            int last = match.Groups[2].Success ? Resolve(match.Groups[2].Value) : first;
            if (last < first) throw new ArgumentException("Dentro de um intervalo, a página final deve ser igual ou maior que a inicial.");
            result.Add(new(first, last));
            if (result.Count > 500) throw new ArgumentException("Selecione no máximo 500 intervalos por operação.");
        }
        return result;
    }

    public static IReadOnlyList<PageRange> Union(IEnumerable<PageRange> ranges)
    {
        var result = new List<PageRange>();
        foreach (var range in ranges.OrderBy(r => r.First))
        {
            if (result.Count == 0 || (long)range.First > (long)result[^1].Last + 1) result.Add(range);
            else result[^1] = result[^1] with { Last = Math.Max(result[^1].Last, range.Last) };
        }
        return result;
    }
}
