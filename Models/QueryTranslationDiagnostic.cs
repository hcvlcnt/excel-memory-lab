namespace OutOfMemoryWorkbook.Models;

public sealed record QueryTranslationDiagnostic(
    bool Translatable,
    string Expression,
    string Message,
    string RecommendedSolution);
