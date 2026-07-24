// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.MitsubishiRx.Tests;

/// <summary>Provides branch-complete coverage for generated Mitsubishi tag mappings.</summary>
internal sealed partial class MitsubishiGeneratedClientTests
{
    /// <summary>Stores the expected all-type generator fragments.</summary>
    private static readonly string[] AllTypeGeneratedFragments =
    [
        "ReadGeneratedBitTagAsync(\"BitTag\"",
        "WriteGeneratedBitTagAsync(\"BitTag\"",
        "ReadStringByTagAsync(\"StringTag\"",
        "WriteStringByTagAsync(\"StringTag\"",
        "ReadFloatByTagAsync(\"FloatTag\"",
        "WriteFloatByTagAsync(\"FloatTag\"",
        "ReadDWordByTagAsync(\"DWordTag\"",
        "WriteDWordByTagAsync(\"UInt32Tag\"",
        "ReadInt32ByTagAsync(\"Int32Tag\"",
        "WriteInt32ByTagAsync(\"Int32Tag\"",
        "ReadInt16ByTagAsync(\"Int16Tag\"",
        "WriteInt16ByTagAsync(\"Int16Tag\"",
        "ReadUInt16ByTagAsync(\"UInt16Tag\"",
        "WriteUInt16ByTagAsync(\"WordTag\"",
        "ReadUInt16ByTagAsync(\"DefaultWordTag\"",
        "public sealed partial record AllTypesSnapshot(",
    ];

    /// <summary>Verifies every supported schema type selects its matching generated read and write surface.</summary>
    /// <returns>A task that completes after the generated surface has been verified.</returns>
    [Test]
    internal async Task IncrementalGeneratorMapsEverySupportedSchemaTypeAsync()
    {
        const string schema = """
        {
          "tags": [
            { "name": "BitTag", "address": "M0", "dataType": "Bit" },
            { "name": "StringTag", "address": "D0", "dataType": "String" },
            { "name": "FloatTag", "address": "D10", "dataType": "Float" },
            { "name": "DWordTag", "address": "D20", "dataType": "DWord" },
            { "name": "UInt32Tag", "address": "D30", "dataType": "UInt32" },
            { "name": "Int32Tag", "address": "D40", "dataType": "Int32" },
            { "name": "Int16Tag", "address": "D50", "dataType": "Int16" },
            { "name": "UInt16Tag", "address": "D60", "dataType": "UInt16" },
            { "name": "WordTag", "address": "D70", "dataType": "Word" },
            { "name": "DefaultWordTag", "address": "D80" }
          ],
          "groups": [
            {
              "name": "AllTypes",
              "tagNames": [
                "BitTag",
                "StringTag",
                "FloatTag",
                "DWordTag",
                "UInt32Tag",
                "Int32Tag",
                "Int16Tag",
                "UInt16Tag",
                "WordTag",
                "DefaultWordTag"
              ]
            }
          ]
        }
        """;

        string generated = RunGenerator(CreateSchemaMarkerSource(schema));

        foreach (string expectedFragment in AllTypeGeneratedFragments)
        {
            await Assert.That(generated.Contains(expectedFragment, StringComparison.Ordinal)).IsTrue();
        }
    }

    /// <summary>Verifies malformed and sparse schemas are handled through diagnostics without generator crashes.</summary>
    /// <returns>A task that completes after every sparse-schema shape has been processed.</returns>
    [Test]
    internal async Task IncrementalGeneratorHandlesSparseAndMalformedSchemaShapesAsync()
    {
        string[] schemas =
        [
            "{}",
            """{ "tags": {}, "groups": {} }""",
            """{ "tags": [{}], "groups": [{ "tagNames": [null] }] }""",
            """{ "tags": null, "groups": [{ "name": null, "tagNames": {} }] }""",
            "{",
        ];

        foreach (string schema in schemas)
        {
            var result = RunGeneratorCompilation(CreateSchemaMarkerSource(schema));
            await Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Id == "AD0001")).IsFalse();
        }
    }

    /// <summary>Verifies global-namespace and empty property bindings take their documented generator paths.</summary>
    /// <returns>A task that completes after property-binding generation has been inspected.</returns>
    [Test]
    internal async Task IncrementalGeneratorHandlesDefaultAndEmptyPropertyBindingsAsync()
    {
        const string source = """
        using IoT.DriverCore.MitsubishiRx;

        internal sealed partial class GlobalDashboard
        {
            public MitsubishiLogicalTagClient LogicalTags { get; init; } = null!;

            [MitsubishiTag("Line1.Speed")]
            public float Speed { get; set; }

            [MitsubishiTag("")]
            public ushort Ignored { get; set; }
        }
        """;

        var result = RunGeneratorCompilation(source);

        await Assert.That(result.Generated.Contains("partial class GlobalDashboard", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("ReadSpeedAsync", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("ReadIgnoredAsync", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Verifies defensive generator mappings retain the documented word fallback.</summary>
    /// <returns>A task that completes after each fallback mapping has been verified.</returns>
    [Test]
    internal async Task GeneratorMappingFallbacksResolveToWordOperationsAsync()
    {
        var emitter = typeof(MitsubishiTagClientGenerator).Assembly.GetType(
            "IoT.DriverCore.MitsubishiRx.MitsubishiTagClientEmitter",
            throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        string[] methodNames = ["ResolveReadType", "ResolveReadMethod", "ResolveWriteMethod"];
        string[] expected = ["ushort", "ReadUInt16ByTagAsync", "WriteUInt16ByTagAsync"];
        string[] supportedDataTypes =
        [
            "Bit",
            "String",
            "Float",
            "DWord",
            "UInt32",
            "Int32",
            "Int16",
            "UInt16",
            "Word",
        ];

        for (var index = 0; index < methodNames.Length; index++)
        {
            var method = emitter.GetMethod(methodNames[index], flags)
                ?? throw new MissingMethodException(emitter.FullName, methodNames[index]);
            var actual = (string?)method.Invoke(null, ["Unsupported"]);
            await Assert.That(actual).IsEqualTo(expected[index]);
            var nullActual = (string?)method.Invoke(null, [null]);
            await Assert.That(nullActual).IsEqualTo(expected[index]);

            foreach (string dataType in supportedDataTypes)
            {
                for (var characterIndex = 0; characterIndex < dataType.Length; characterIndex++)
                {
                    char[] characters = dataType.ToCharArray();
                    characters[characterIndex] = characters[characterIndex] == '?' ? '!' : '?';
                    string nearMiss = new(characters);
                    var nearMissActual = (string?)method.Invoke(null, [nearMiss]);
                    await Assert.That(nearMissActual).IsEqualTo(expected[index]);
                }
            }
        }
    }
}
