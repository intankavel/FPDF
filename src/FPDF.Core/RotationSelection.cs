namespace FPDF.Core;

public record RotationRule(PageRange Pages, int Degrees);

public static class RotationSelection
{
    public static IReadOnlyList<RotationRule> Parse(string expression, int total)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("Informe os giros. Exemplo: 1:E90, 2:180, 3-fim:D90.");
        if (expression.Length > 12_000) throw new ArgumentException("A lista de giros é muito longa.");
        var rules = new List<RotationRule>();
        foreach (var token in expression.Split(','))
        {
            var fields = token.Trim().Replace(";", ":").Split(':');
            if (fields.Length is < 2 or > 3) throw new ArgumentException($"Giro inválido: '{token}'. Use 1:E90, 2:180, 3:D90.");
            var pages = PageSelection.Parse(fields[0], total);
            string angle = string.Concat(fields.Skip(1)).Replace(" ", "").Replace("°", "").ToUpperInvariant();
            int degrees = angle switch { "E90" or "-90" => -90, "D90" or "90" or "+90" => 90, "180" or "E180" or "D180" => 180, _ => throw new ArgumentException($"Giro inválido: '{angle}'. Use E90 (esquerda), D90 (direita) ou 180.") };
            foreach (var page in pages)
            {
                if (rules.Any(r => r.Pages.First <= page.Last && page.First <= r.Pages.Last))
                    throw new ArgumentException("Uma página recebeu mais de um giro. Remova a sobreposição entre as regras.");
                rules.Add(new(page, degrees));
                if (rules.Count > 500) throw new ArgumentException("Use no máximo 500 regras de giro.");
            }
        }
        return rules;
    }
}
