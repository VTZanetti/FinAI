using FinAI.Api.Services.Budgets;
using FluentValidation;

namespace FinAI.Api.Validation;

public class CreateBudgetValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("CategoryId is required");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year out of range");

        RuleFor(x => x.LimitAmount)
            .GreaterThan(0m).WithMessage("LimitAmount must be greater than zero")
            .InclusiveBetween(0.01m, 999_999_999_999.99m)
            .WithMessage("LimitAmount out of range");
    }
}

public class UpdateBudgetValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetValidator()
    {
        RuleFor(x => x.LimitAmount)
            .GreaterThan(0m).WithMessage("LimitAmount must be greater than zero")
            .InclusiveBetween(0.01m, 999_999_999_999.99m)
            .WithMessage("LimitAmount out of range");
    }
}
