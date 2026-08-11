using FinAI.Api.Models.Enums;
using FinAI.Api.Services.Accounts;
using FluentValidation;

namespace FinAI.Api.Validation;

public class CreateAccountValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(120);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid account type");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO-4217 code")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO-4217 code");

        RuleFor(x => x.InitialBalance)
            .InclusiveBetween(-999_999_999_999.99m, 999_999_999_999.99m)
            .WithMessage("InitialBalance out of range");
    }
}

public class UpdateAccountValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(120);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid account type");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO-4217 code")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO-4217 code");
    }
}
