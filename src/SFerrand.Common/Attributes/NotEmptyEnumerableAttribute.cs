using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace SFerrand.Common.Attributes;

/// <summary>
/// Validates that a collection property is neither <see langword="null"/> nor empty.
/// </summary>
/// <remarks>
/// Unlike <see cref="RequiredAttribute"/> and <see cref="MinLengthAttribute"/>, it works on any
/// <see cref="IEnumerable"/>, including lazy sequences, without materializing more than one element.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyEnumerableAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IEnumerable enumerable && HasAnyElement(enumerable))
            return ValidationResult.Success;

        return new ValidationResult(
            ErrorMessage ?? $"{validationContext.DisplayName} can not be null or empty",
            validationContext.MemberName is { } member ? new[] { member } : null);
    }

    private static bool HasAnyElement(IEnumerable enumerable)
    {
        var enumerator = enumerable.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}
