namespace FPDF.App;

public record OperationOption(int Id, string Name, string Category);

public static class OperationCatalog
{
    // Stable identifiers keep behavior independent of the menu's visual order.
    public static IReadOnlyList<OperationOption> All { get; } = new OperationOption[]
    {
        new(0, "Juntar PDFs", "Organizar páginas"),
        new(11, "Visualizar e organizar páginas", "Organizar páginas"),
        new(2, "Extrair páginas", "Organizar páginas"),
        new(3, "Dividir por intervalos", "Organizar páginas"),
        new(4, "Girar páginas", "Organizar páginas"),
        new(5, "Otimizar sem perda", "Reduzir tamanho"),
        new(6, "Comprimir PDF", "Reduzir tamanho"),
        new(12, "Comprimir até um limite de tamanho", "Reduzir tamanho"),
        new(1, "Dividir por tamanho máximo", "Preparar para envio"),
        new(7, "Comprimir e dividir por tamanho", "Preparar para envio"),
        new(8, "Imagens para PDF", "Criar e identificar"),
        new(13, "Reconhecer texto (OCR)", "Criar e identificar"),
        new(9, "Adicionar marca-d’água", "Criar e identificar"),
        new(10, "Numerar páginas", "Criar e identificar")
    };
}
