// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Globalization;

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Tests for generated stream value conversion.</summary>
public sealed class SerialPortReactiveValueConverterTests
{
    /// <summary>The reusable ready text sample.</summary>
    private const string ReadyText = "ready";

    /// <summary>The reusable numeric analogue-to-digital converter pattern.</summary>
    private const string AdcPattern = "^ADC=(\\d+)$";

    /// <summary>Verifies null values fail conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WhenValueIsNullAndTargetIsInt_ReturnsFalseAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            null,
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Verifies simple string conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForString_ReturnsInputTextAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<string>(
            ReadyText,
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(ReadyText);
    }

    /// <summary>Verifies regular expression named groups are converted.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithNamedRegexGroup_ConvertsGroupValueAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<double>(
            "TEMP:-12.5",
            "^TEMP:(?<value>-?\\d+(\\.\\d+)?)$",
            "value",
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(NegativeTwelvePointFive);
    }

    /// <summary>Verifies regular expression numeric groups are converted.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithNumberedRegexGroup_ConvertsGroupValueAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            "ADC=1024",
            AdcPattern,
            null,
            1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(OneThousandTwentyFour);
    }

    /// <summary>Verifies regular expression matching honors ignore-case.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithIgnoreCase_MatchesCaseInsensitivePatternAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<bool>(
            "status:ON",
            "^STATUS:(?<value>ON)$",
            "value",
            -1,
            ignoreCase: true,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsFalse();
    }

    /// <summary>Verifies unmatched regular expressions fail conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WhenPatternDoesNotMatch_ReturnsFalseAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            "ADC=x",
            AdcPattern,
            null,
            1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Verifies char conversion requires a single character.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForChar_RequiresSingleCharacterAsync()
    {
        var oneCharacter = SerialPortReactiveValueConverter.TryConvertMatch<char>(
            "A",
            null,
            null,
            -1,
            false,
            out var character);
        var twoCharacters = SerialPortReactiveValueConverter.TryConvertMatch<char>("AB", null, null, -1, false, out _);

        await Assert.That(oneCharacter).IsTrue();
        await Assert.That(character).IsEqualTo('A');
        await Assert.That(twoCharacters).IsFalse();
    }

    /// <summary>Verifies Boolean conversion accepts standard and numeric values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForBoolean_ConvertsKnownValuesAsync()
    {
        var textTrue = SerialPortReactiveValueConverter.TryConvertMatch<bool>(
            "true",
            null,
            null,
            -1,
            false,
            out var trueResult);
        var numericFalse = SerialPortReactiveValueConverter.TryConvertMatch<bool>(
            "0",
            null,
            null,
            -1,
            false,
            out var falseResult);
        var invalid = SerialPortReactiveValueConverter.TryConvertMatch<bool>("maybe", null, null, -1, false, out _);

        await Assert.That(textTrue).IsTrue();
        await Assert.That(trueResult).IsTrue();
        await Assert.That(numericFalse).IsTrue();
        await Assert.That(falseResult).IsFalse();
        await Assert.That(invalid).IsFalse();
    }

    /// <summary>Verifies enum conversion ignores case.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForEnum_ConvertsIgnoringCaseAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<DayOfWeek>(
            "friday",
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(DayOfWeek.Friday);
    }

    /// <summary>Verifies Guid and date conversions use invariant parsing.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForGuidAndDates_ConvertsValuesAsync()
    {
        var guid = Guid.NewGuid();
        var dateTime = new DateTime(2026, 6, 26, 10, 30, 0, DateTimeKind.Utc);
        var dateTimeOffset = new DateTimeOffset(2026, 6, 26, 10, 30, 0, TimeSpan.Zero);

        var guidConverted = SerialPortReactiveValueConverter.TryConvertMatch<Guid>(
            guid.ToString(),
            null,
            null,
            -1,
            false,
            out var guidResult);
        var dateTimeConverted = SerialPortReactiveValueConverter.TryConvertMatch<DateTime>(
            dateTime.ToString("O"),
            null,
            null,
            -1,
            false,
            out var dateTimeResult);
        var dateTimeOffsetConverted = SerialPortReactiveValueConverter.TryConvertMatch<DateTimeOffset>(
            dateTimeOffset.ToString("O"),
            null,
            null,
            -1,
            false,
            out var dateTimeOffsetResult);

        await Assert.That(guidConverted).IsTrue();
        await Assert.That(guidResult).IsEqualTo(guid);
        await Assert.That(dateTimeConverted).IsTrue();
        await Assert.That(dateTimeResult).IsEqualTo(dateTime);
        await Assert.That(dateTimeOffsetConverted).IsTrue();
        await Assert.That(dateTimeOffsetResult).IsEqualTo(dateTimeOffset);
    }

    /// <summary>Verifies unmatched named and numeric groups fall back safely and numeric true is accepted.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_GroupFallbacksAndNumericTrue_AreHandledAsync()
    {
        var missingNamedGroup = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            "ADC=7",
            AdcPattern,
            "missing",
            1,
            false,
            out var namedResult);
        var invalidNumberedGroup = SerialPortReactiveValueConverter.TryConvertMatch<string>(
            ReadyText,
            "^(ready)$",
            null,
            Answer,
            false,
            out var numberedResult);
        var numericTrue = SerialPortReactiveValueConverter.TryConvertMatch<bool>(
            "1",
            null,
            null,
            -1,
            false,
            out var booleanResult);

        await Assert.That(missingNamedGroup).IsTrue();
        await Assert.That(namedResult).IsEqualTo(Seven);
        await Assert.That(invalidNumberedGroup).IsTrue();
        await Assert.That(numberedResult).IsEqualTo(ReadyText);
        await Assert.That(numericTrue).IsTrue();
        await Assert.That(booleanResult).IsTrue();
    }

    /// <summary>Verifies a type converter that returns no value is treated as an unsuccessful conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WhenTypeConverterReturnsNull_ReturnsFalseAsync()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<NullConvertedValue>(
            "none",
            null,
            null,
            -1,
            false,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsNull();
    }

    /// <summary>Represents a type whose deterministic converter declines every string value.</summary>
    [TypeConverter(typeof(NullValueConverter))]
    public sealed class NullConvertedValue
    {
        /// <summary>Gets the deterministic empty payload.</summary>
        public string Payload { get; } = string.Empty;
    }

    /// <summary>Converts supported strings into no value to exercise the converter-null guard.</summary>
    public sealed class NullValueConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => null;
    }
}
