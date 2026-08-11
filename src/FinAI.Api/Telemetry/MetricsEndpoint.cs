using System.Globalization;
using System.Text;

namespace FinAI.Api.Telemetry;

/// <summary>
/// Renderiza as métricas do Meter FinAI.Api em formato Prometheus (text/plain).
/// </summary>
public static class MetricsEndpoint
{
    public static string Render()
    {
        var sb = new StringBuilder();

        // Classificações de IA por fonte
        sb.AppendLine("# TYPE finai_ai_classification_total counter");
        foreach (var (source, value) in FinAiMetrics.ClassificationSnapshots)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"finai_ai_classification_total{{source=\"{source}\"}} {value}");

        // Latência do LLM (agregado)
        var (calls, totalSeconds) = FinAiMetrics.LlmLatencySnapshot;
        sb.AppendLine("# TYPE finai_ai_llm_latency_seconds_sum gauge");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"finai_ai_llm_latency_seconds_sum {totalSeconds}");
        sb.AppendLine("# TYPE finai_ai_llm_latency_seconds_count gauge");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"finai_ai_llm_latency_seconds_count {calls}");

        // Anomalias e forecasts
        sb.AppendLine("# TYPE finai_anomaly_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"finai_anomaly_total {FinAiMetrics.AnomalyCount}");
        sb.AppendLine("# TYPE finai_forecast_generated_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"finai_forecast_generated_total {FinAiMetrics.ForecastCount}");

        // Requisições HTTP por método/rota/status
        sb.AppendLine("# TYPE finai_http_request_duration_seconds_count counter");
        foreach (var ((method, route, status), value) in FinAiMetrics.HttpRequestSnapshots)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"finai_http_request_duration_seconds_count{{method=\"{method}\",endpoint=\"{route}\",status=\"{status}\"}} {value}");

        return sb.ToString();
    }
}
