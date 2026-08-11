using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services.AnomalyDetection;
using FinAI.Api.Services.AnomalyDetection.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.AnomalyDetection;

[Trait("Category", "Unit")]
public class AnomalyDetectionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CategoryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();

    private AnomalyDetectionService CreateService()
        => new(_transactions, Options.Create(new AnomalyDetectionOptions
        {
            MinSamplesForZScore = 8,
            AnomalyZScoreThreshold = 3.0,
            MinSamplesForIqr = 4
        }), NullLogger<AnomalyDetectionService>.Instance);

    private static Transaction Tx(decimal amount, Guid? categoryId = null, string? categoryName = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Teste",
            Amount = amount,
            Date = new DateOnly(2026, 8, 10),
            Type = TransactionType.Expense,
            CategoryId = categoryId,
            Category = categoryName is null ? null : new Category { Id = categoryId ?? Guid.NewGuid(), Name = categoryName }
        };

    private void SetupHistory(int count = 12)
    {
        var history = Enumerable.Range(1, count)
            .Select(i => Tx(-(150m + i * 3m), CategoryId, "Food"))
            .ToList();
        // Histórico: query sem From específico (qualquer que não seja o período alvo)
        _transactions.QueryAsync(UserId,
                Arg.Is<TransactionFilter>(f => f.Type == TransactionType.Expense && f.From != new DateOnly(2026, 8, 1)),
                Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult(history, history.Count, 1, int.MaxValue, 1));
    }

    [Fact]
    public async Task DetectAsync_ExtremeTransaction_IsFlaggedAsAnomaly()
    {
        // Arrange: histórico normal + uma transação extrema no período
        SetupHistory(12);
        var extreme = Tx(-5000m, CategoryId, "Food");
        // O período alvo (agosto) retorna apenas a transação extrema
        _transactions.QueryAsync(UserId,
                Arg.Is<TransactionFilter>(f => f.From == new DateOnly(2026, 8, 1) && f.To == new DateOnly(2026, 8, 31)),
                Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([extreme], 1, 1, int.MaxValue, 1));

        var service = CreateService();

        // Act
        var result = await service.DetectAsync(UserId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().Contain(i => i.TransactionId == extreme.Id && i.Anomaly);
    }

    [Fact]
    public async Task DetectAsync_FromAfterTo_ReturnsValidationError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.DetectAsync(UserId, new DateOnly(2026, 8, 31), new DateOnly(2026, 8, 1));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task DetectAsync_MethodIqr_ReturnsIqrMethod()
    {
        // Arrange
        SetupHistory(12);
        _transactions.QueryAsync(UserId,
                Arg.Is<TransactionFilter>(f => f.From == new DateOnly(2026, 8, 1) && f.To == new DateOnly(2026, 8, 31)),
                Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([], 0, 1, int.MaxValue, 1));
        var service = CreateService();

        // Act
        var result = await service.DetectAsync(UserId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), method: "iqr");

        // Assert
        result.Value!.Method.Should().Be("iqr");
    }

    [Fact]
    public async Task CheckAsync_ExtremeValue_ReturnsAnomalyTrue()
    {
        // Arrange: histórico normal na categoria
        SetupHistory(12);
        var service = CreateService();

        // Act
        var result = await service.CheckAsync(UserId, "Compra estranha", -8000m, CategoryId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Anomaly.Should().BeTrue();
        result.Value.SuggestedAction.Should().Be("review");
    }

    [Fact]
    public async Task CheckAsync_NormalValue_ReturnsAnomalyFalse()
    {
        // Arrange
        SetupHistory(12);
        var service = CreateService();

        // Act
        var result = await service.CheckAsync(UserId, "Compra normal", -160m, CategoryId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Anomaly.Should().BeFalse();
        result.Value.SuggestedAction.Should().Be("ok");
    }

    [Fact]
    public async Task CheckAsync_FewSamples_FallsBackToIqr()
    {
        // Arrange: apenas 2 amostras → Z-score insuficiente → IQR
        SetupHistory(2);
        var service = CreateService();

        // Act
        var result = await service.CheckAsync(UserId, "Compra", -5000m, CategoryId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Method.Should().Be("iqr");
    }
}
