namespace FinAI.Api.Services.Forecasting.Models;

public sealed record CashFlowForecast(
    string Method,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<MonthlyForecastPoint> Forecast,
    ConfidenceInfo Confidence);

public sealed record MonthlyForecastPoint(
    string Month,
    decimal Income,
    decimal Expenses,
    decimal Balance);

public sealed record ConfidenceInfo(string Level, string Note);

/// <summary>
/// Série mensal de histórico usada pelo forecaster.
/// </summary>
public sealed record MonthlySeriesPoint(string Month, decimal Income, decimal Expenses);
