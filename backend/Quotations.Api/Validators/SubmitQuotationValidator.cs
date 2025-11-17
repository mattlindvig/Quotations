using FluentValidation;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using System;

namespace Quotations.Api.Validators;

/// <summary>
/// Validator for quotation submission requests
/// </summary>
public class SubmitQuotationValidator : AbstractValidator<SubmitQuotationRequest>
{
    public SubmitQuotationValidator()
    {
        // Quotation text validation
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Quotation text is required")
            .Length(1, 5000).WithMessage("Quotation text must be between 1 and 5000 characters")
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("Quotation text cannot be only whitespace");

        // Author name validation
        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required")
            .Length(1, 200).WithMessage("Author name must be between 1 and 200 characters");

        // Author lifespan validation (optional)
        RuleFor(x => x.AuthorLifespan)
            .Matches(@"^\d{4}-(\d{4}|present)$")
            .WithMessage("Author lifespan must be in format 'YYYY-YYYY' or 'YYYY-present'")
            .When(x => !string.IsNullOrEmpty(x.AuthorLifespan));

        // Author occupation validation (optional)
        RuleFor(x => x.AuthorOccupation)
            .MaximumLength(200).WithMessage("Author occupation must be 200 characters or less")
            .When(x => !string.IsNullOrEmpty(x.AuthorOccupation));

        // Source title validation
        RuleFor(x => x.SourceTitle)
            .NotEmpty().WithMessage("Source title is required")
            .Length(1, 300).WithMessage("Source title must be between 1 and 300 characters");

        // Source type validation
        RuleFor(x => x.SourceType)
            .NotEmpty().WithMessage("Source type is required")
            .Must(BeValidSourceType).WithMessage("Source type must be one of: book, movie, speech, interview, other");

        // Source year validation (optional)
        RuleFor(x => x.SourceYear)
            .InclusiveBetween(1000, 2100).WithMessage("Source year must be between 1000 and 2100")
            .When(x => x.SourceYear.HasValue);

        // Source additional info validation (optional)
        RuleFor(x => x.SourceAdditionalInfo)
            .MaximumLength(500).WithMessage("Source additional info must be 500 characters or less")
            .When(x => !string.IsNullOrEmpty(x.SourceAdditionalInfo));

        // Tags validation
        RuleFor(x => x.Tags)
            .Must(tags => tags.Count <= 20).WithMessage("Maximum 20 tags allowed")
            .Must(tags => tags.TrueForAll(tag => tag.Length >= 1 && tag.Length <= 50))
            .WithMessage("Each tag must be between 1 and 50 characters");
    }

    private bool BeValidSourceType(string sourceType)
    {
        return Enum.TryParse<SourceType>(sourceType, true, out _);
    }
}