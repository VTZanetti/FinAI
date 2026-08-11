using FinAI.Api.Services.Categories;
using FluentValidation;

namespace FinAI.Api.Validation;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(80);

        RuleFor(x => x.Subcategory)
            .MaximumLength(80)
            .When(x => x.Subcategory is not null);
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(80);

        RuleFor(x => x.Subcategory)
            .MaximumLength(80)
            .When(x => x.Subcategory is not null);
    }
}
