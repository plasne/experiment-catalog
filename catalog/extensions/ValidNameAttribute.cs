using System;
using System.ComponentModel.DataAnnotations;

namespace Catalog;

/// <summary>
/// Validates that a string is a valid name containing only letters, digits, hyphens, underscores, periods, and colons.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class ValidNameAttribute : ValidationAttribute
{
    public ValidNameAttribute()
        : base("The {0} field must contain only letters, digits, hyphens, underscores, periods, or colons.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is not string name)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Length < 3 || name.Length > 50)
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != ':')
            {
                return false;
            }
        }

        return true;
    }
}
