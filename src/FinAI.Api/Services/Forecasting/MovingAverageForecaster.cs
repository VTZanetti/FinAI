namespace FinAI.Api.Services.Forecasting;

public interface IMovingAverageForecaster
{
    /// <summary>
    /// Prevê o próximo valor com média móvel ponderada por recência (pesos crescentes).
    /// </summary>
    decimal ForecastNext(IReadOnlyList<decimal> series);
}

/// <summary>
/// Média móvel ponderada: pesos w_i = i (1..k) normalizados — meses mais recentes pesam mais.
/// </summary>
public class MovingAverageForecaster : IMovingAverageForecaster
{
    public decimal ForecastNext(IReadOnlyList<decimal> series)
    {
        if (series.Count == 0)
            return 0m;

        if (series.Count == 1)
            return series[0];

        var totalWeight = 0d;
        var weightedSum = 0m;

        for (var i = 0; i < series.Count; i++)
        {
            var weight = i + 1; // 1..k
            totalWeight += weight;
            weightedSum += series[i] * weight;
        }

        return Math.Round(weightedSum / (decimal)totalWeight, 2);
    }
}
