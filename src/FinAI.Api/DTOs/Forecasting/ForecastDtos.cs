using FinAI.Api.Services.Forecasting.Models;

namespace FinAI.Api.DTOs.Forecasting;

public sealed record CashFlowForecastResponse(
    string Method,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<MonthlyForecastPointDto> Forecast,
    ConfidenceInfoDto Confidence);

public sealed record MonthlyForecastPointDto(string Month, decimal Income, decimal Expenses, decimal Balance);

public sealed record ConfidenceInfoDto(string Level, string Note);

public static class ForecastMappings
{
    public static CashFlowForecastResponse ToResponse(this CashFlowForecast f)
        => new(
            f.Method,
            f.GeneratedAt,
            f.Forecast.Select(p => new MonthlyForecastPointDto(p.Month, p.Income, p.Expenses, p.Balance)).ToList(),
            new ConfidenceInfoDto(f.Confidence.Level, f.Confidence.Note));
}
