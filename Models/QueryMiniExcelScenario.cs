namespace OutOfMemoryWorkbook.Models;

public sealed record QueryMiniExcelScenario(
    string Name,
    string Query,
    string EnumConversion,
    string Materialization,
    string Objective);
