using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace FinAI.Api.Telemetry;

/// <summary>
/// Métricas customizadas (OpenTelemetry-style) — expostas via /metrics (Prometheus) quando habilitado.
/// </summary>
public static class FinAiMetrics
{
    public const string MeterName = "FinAI.Api";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Classificações de IA por fonte (rules|cached|llm|fallback|external).</summary>
    public static readonly Counter<long> AiClassificationTotal = Meter.CreateCounter<long>(
        "finai_ai_classification_total",
        description: "Total de classificações de IA por fonte.");

    /// <summary>Latência das chamadas ao LLM (segundos).</summary>
    public static readonly Histogram<double> AiLlmLatencySeconds = Meter.CreateHistogram<double>(
        "finai_ai_llm_latency_seconds",
        unit: "s",
        description: "Latência das chamadas ao LLM.");

    /// <summary>Anomalias detectadas.</summary>
    public static readonly Counter<long> AnomalyTotal = Meter.CreateCounter<long>(
        "finai_anomaly_total",
        description: "Total de anomalias detectadas.");

    /// <summary>Forecasts gerados.</summary>
    public static readonly Counter<long> ForecastGeneratedTotal = Meter.CreateCounter<long>(
        "finai_forecast_generated_total",
        description: "Total de forecasts gerados.");

    /// <summary>Duração das requisições HTTP (segundos) — status e endpoint.</summary>
    public static readonly Histogram<double> HttpRequestDurationSeconds = Meter.CreateHistogram<double>(
        "finai_http_request_duration_seconds",
        unit: "s",
        description: "Duração das requisições HTTP.");

    // ── Snapshot para o endpoint /metrics (sem depender de exporter) ─────────

    private static readonly ConcurrentDictionary<string, long> ClassificationBySource = new();
    private static long _anomalyCount;
    private static long _forecastCount;
    private static long _llmCalls;
    private static double _llmLatencySumSeconds;
    private static readonly object LlmLock = new();
    private static readonly ConcurrentDictionary<(string Method, string Route, int Status), long> HttpRequests = new();

    public static IReadOnlyDictionary<string, long> ClassificationSnapshots => ClassificationBySource;

    public static long AnomalyCount => Interlocked.Read(ref _anomalyCount);

    public static long ForecastCount => Interlocked.Read(ref _forecastCount);

    public static (long Calls, double TotalSeconds) LlmLatencySnapshot
    {
        get
        {
            lock (LlmLock)
            {
                return (_llmCalls, _llmLatencySumSeconds);
            }
        }
    }

    public static IReadOnlyDictionary<(string Method, string Route, int Status), long> HttpRequestSnapshots => HttpRequests;

    public static void RecordClassification(string source)
    {
        AiClassificationTotal.Add(1, new KeyValuePair<string, object?>("source", source));
        ClassificationBySource.AddOrUpdate(source, 1, (_, v) => v + 1);
    }

    public static void RecordLlmLatency(double seconds)
    {
        AiLlmLatencySeconds.Record(seconds);
        lock (LlmLock)
        {
            _llmCalls++;
            _llmLatencySumSeconds += seconds;
        }
    }

    public static void RecordAnomaly()
    {
        AnomalyTotal.Add(1);
        Interlocked.Increment(ref _anomalyCount);
    }

    public static void RecordForecast()
    {
        ForecastGeneratedTotal.Add(1);
        Interlocked.Increment(ref _forecastCount);
    }

    public static void RecordHttpRequest(string method, string route, int status, double seconds)
    {
        HttpRequestDurationSeconds.Record(seconds,
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("endpoint", route),
            new KeyValuePair<string, object?>("status", status));
        HttpRequests.AddOrUpdate((method, route, status), 1, (_, v) => v + 1);
    }
}
