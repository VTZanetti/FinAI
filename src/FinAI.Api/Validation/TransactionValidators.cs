using FinAI.Api.Services.Transactions;
using FluentValidation;

namespace FinAI.Api.Validation;

public class CreateTransactionValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(255);

        RuleFor(x => x.Amount)
            .NotEqual(0m).WithMessage("Amount must not be zero")
            .InclusiveBetween(-999_999_999_999.99m, 999_999_999_999.99m)
            .WithMessage("Amount out of range");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(new DateTime(2000, 1, 1)))
            .WithMessage("Date must be on or after 2000-01-01")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(1)))
            .WithMessage("Date cannot be more than 1 year in the future");

        RuleFor(x => x.ExternalId)
            .MaximumLength(120)
            .When(x => x.ExternalId is not null);
    }
}

public class UpdateTransactionValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(255);

        RuleFor(x => x.Amount)
            .NotEqual(0m).WithMessage("Amount must not be zero")
            .InclusiveBetween(-999_999_999_999.99m, 999_999_999_999.99m)
            .WithMessage("Amount out of range");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(new DateTime(2000, 1, 1)))
            .WithMessage("Date must be on or after 2000-01-01")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(1)))
            .WithMessage("Date cannot be more than 1 year in the future");
    }
}
