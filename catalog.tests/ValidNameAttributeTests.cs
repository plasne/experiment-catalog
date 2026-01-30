using System;
using System.Collections.Generic;
using Catalog;
using Xunit;

namespace Catalog.Tests;

public class ValidNameAttributeTests
{
    private readonly ValidNameAttribute _attribute = new();

    #region String Tests

    [Theory]
    [InlineData("valid-name", true)]
    [InlineData("validname", true)]
    [InlineData("12345", true)]
    [InlineData("valid-name-test", true)]
    [InlineData("valid_name_test", true)]
    [InlineData("valid.name.test", true)]
    [InlineData("valid:name:test", true)]
    [InlineData("a1-b_c.d:e", true)]
    [InlineData("abc", true)] // minimum length
    [InlineData("ValidName", true)] // mixed case
    [InlineData("名前テスト", true)] // Unicode letters
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("ab", false)] // too short
    [InlineData("invalid name", false)] // space
    [InlineData("invalid@name", false)]
    [InlineData("invalid#name", false)]
    [InlineData("invalid$name", false)]
    [InlineData("invalid!name", false)]
    [InlineData("invalid/name", false)]
    [InlineData("invalid\\name", false)]
    [InlineData(null, false)]
    public void IsValid_String_ReturnsExpected(string? value, bool expected)
    {
        Assert.Equal(expected, _attribute.IsValid(value));
    }

    [Theory]
    [MemberData(nameof(BoundaryLengthTestData))]
    public void IsValid_StringBoundaryLength_ReturnsExpected(string value, bool expected)
    {
        Assert.Equal(expected, _attribute.IsValid(value));
    }

    public static TheoryData<string, bool> BoundaryLengthTestData => new()
    {
        { new string('a', 50), true }, // exactly maximum
        { "a1-b_c.d:e" + new string('x', 40), true }, // exactly 50 chars
        { new string('a', 51), false } // exceeds maximum
    };

    #endregion

    #region Dictionary Tests

    [Theory]
    [MemberData(nameof(ValidDictionaryTestData))]
    public void IsValid_Dictionary_ReturnsTrue(Dictionary<string, object>? dict)
    {
        Assert.True(_attribute.IsValid(dict));
    }

    [Theory]
    [MemberData(nameof(InvalidDictionaryTestData))]
    public void IsValid_Dictionary_ReturnsFalse(Dictionary<string, object> dict)
    {
        Assert.False(_attribute.IsValid(dict));
    }

    public static TheoryData<Dictionary<string, object>?> ValidDictionaryTestData => new()
    {
        new Dictionary<string, object> { { "valid-key", "value1" }, { "another_key", 123 } },
        new Dictionary<string, object> { { "validkey", "value" } },
        new Dictionary<string, object>
        {
            { "letters", 1 },
            { "123456", 2 },
            { "with-hyphens", 3 },
            { "with_underscores", 4 },
            { "with.periods", 5 },
            { "with:colons", 6 }
        },
        null // null collections are valid
    };

    public static TheoryData<Dictionary<string, object>> InvalidDictionaryTestData => new()
    {
        new Dictionary<string, object>(), // empty
        new Dictionary<string, object> { { "valid-key", "value1" }, { "invalid key", "value2" } }, // space in key
        new Dictionary<string, object> { { "ab", "value" } }, // too short
        new Dictionary<string, object> { { new string('a', 51), "value" } }, // too long
        new Dictionary<string, object> { { "invalid@key", "value" } } // special char
    };

    #endregion

    #region IEnumerable<string> Tests

    [Theory]
    [MemberData(nameof(ValidEnumerableTestData))]
    public void IsValid_Enumerable_ReturnsTrue(IEnumerable<string>? values)
    {
        Assert.True(_attribute.IsValid(values));
    }

    [Theory]
    [MemberData(nameof(InvalidEnumerableTestData))]
    public void IsValid_Enumerable_ReturnsFalse(IEnumerable<string?> values)
    {
        Assert.False(_attribute.IsValid(values));
    }

    public static TheoryData<IEnumerable<string>?> ValidEnumerableTestData => new()
    {
        new List<string> { "valid-name", "another_name", "name.test" },
        new List<string> { "validname" },
        new[] { "valid-name", "another_name" },
        new HashSet<string> { "valid-name", "another_name" },
        null // null collections are valid
    };

    public static TheoryData<IEnumerable<string?>> InvalidEnumerableTestData => new()
    {
        new List<string>(), // empty list
        new List<string> { "valid-name", "invalid name" }, // space
        new List<string> { "valid-name", "ab" }, // too short
        new List<string> { "valid-name", new string('a', 51) }, // too long
        new List<string?> { "valid-name", null }, // null element
        new List<string> { "valid-name", "" }, // empty element
        new List<string> { "valid-name", "   " }, // whitespace element
        new[] { "valid-name", "invalid name" }, // array with invalid
        Array.Empty<string>(), // empty array
        new HashSet<string> { "valid-name", "invalid name" } // hashset with invalid
    };

    #endregion

    #region Unsupported Type Tests

    [Theory]
    [MemberData(nameof(UnsupportedTypeTestData))]
    public void IsValid_UnsupportedType_ReturnsFalse(object? value)
    {
        Assert.False(_attribute.IsValid(value));
    }

    public static TheoryData<object?> UnsupportedTypeTestData => new()
    {
        123,
        new object(),
        new List<int> { 1, 2, 3 },
        new Dictionary<int, object> { { 1, "value" } }
    };

    #endregion
}
