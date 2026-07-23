// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace IoT.DriverCore.TwinCATRx.SourceGenerators;

/// <summary>Generates TwinCAT reactive stream binding members.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class TwinCatReactiveStreamGenerator : IIncrementalGenerator
{
    /// <summary>Stores the legacy stream attribute metadata name.</summary>
    private const string TwinCatReactiveStreamAttributeName = "IoT.DriverCore.TwinCATRx.TwinCatReactiveStreamAttribute";

    /// <summary>Stores the Reactive legacy stream attribute metadata name.</summary>
    private const string ReactiveTwinCatReactiveStreamAttributeName =
        "IoT.DriverCore.TwinCATRx.Reactive.TwinCatReactiveStreamAttribute";

    /// <summary>Stores the PLC connection attribute metadata name.</summary>
    private const string TwinCatPlcConnectionAttributeName = "IoT.DriverCore.TwinCATRx.TwinCatPlcConnectionAttribute";

    /// <summary>Stores the Reactive PLC connection attribute metadata name.</summary>
    private const string ReactiveTwinCatPlcConnectionAttributeName =
        "IoT.DriverCore.TwinCATRx.Reactive.TwinCatPlcConnectionAttribute";

    /// <summary>Stores the direct notification attribute metadata name.</summary>
    private const string DirectNotificationAttributeName = "IoT.DriverCore.TwinCATRx.DirectNotificationAttribute";

    /// <summary>Stores the Reactive direct notification attribute metadata name.</summary>
    private const string ReactiveDirectNotificationAttributeName = "IoT.DriverCore.TwinCATRx.Reactive.DirectNotificationAttribute";

    /// <summary>Stores the structured notification attribute metadata name.</summary>
    private const string StructuredNotificationAttributeName = "IoT.DriverCore.TwinCATRx.StructuredNotificationAttribute";

    /// <summary>Stores the Reactive structured notification attribute metadata name.</summary>
    private const string ReactiveStructuredNotificationAttributeName =
        "IoT.DriverCore.TwinCATRx.Reactive.StructuredNotificationAttribute";

    /// <summary>Stores the write-only attribute metadata name.</summary>
    private const string WriteOnlyAttributeName = "IoT.DriverCore.TwinCATRx.WriteOnlyAttribute";

    /// <summary>Stores the Reactive write-only attribute metadata name.</summary>
    private const string ReactiveWriteOnlyAttributeName = "IoT.DriverCore.TwinCATRx.Reactive.WriteOnlyAttribute";

    /// <summary>Stores the lean library namespace.</summary>
    private const string LeanLibraryNamespace = "IoT.DriverCore.TwinCATRx";

    /// <summary>Stores the Reactive library namespace.</summary>
    private const string ReactiveLibraryNamespace = "IoT.DriverCore.TwinCATRx.Reactive";

    /// <summary>Stores the lean core namespace.</summary>
    private const string LeanCoreNamespace = "IoT.DriverCore.TwinCATRx.Core";

    /// <summary>Stores the Reactive core namespace.</summary>
    private const string ReactiveCoreNamespace = "IoT.DriverCore.TwinCATRx.Core.Reactive";

    /// <summary>Stores the lean collections namespace.</summary>
    private const string LeanCollectionsNamespace = "CP.Collections";

    /// <summary>Stores the Reactive collections namespace.</summary>
    private const string ReactiveCollectionsNamespace = "CP.Collections.Reactive";

    /// <summary>Stores the trimming warning for dynamic client connections.</summary>
    private const string ClientReflectionWarning =
        "RxTcAdsClient uses reflection to materialize PLC structure types.";

    /// <summary>Stores the trimming warning for structured notifications.</summary>
    private const string StructuredNotificationWarning =
        "Structured notifications use HashTableRx structure materialization.";

    /// <summary>Stores the generated using-directive prefix.</summary>
    private const string UsingDirectivePrefix = "using ";

    /// <summary>Stores the direct notification tag kind.</summary>
    private const string DirectKind = "Direct";

    /// <summary>Stores the structured notification tag kind.</summary>
    private const string StructuredKind = "Structured";

    /// <summary>Stores the write-only tag kind.</summary>
    private const string WriteOnlyKind = "WriteOnly";

    /// <summary>Stores the observable-name attribute argument.</summary>
    private const string ObservableNameArgument = "ObservableName";

    /// <summary>Stores the suffix used for generated observable members.</summary>
    private const string ObservableSuffix = "Observable";

    /// <summary>Stores the array-size attribute argument.</summary>
    private const string ArraySizeArgument = "ArraySize";

    /// <summary>Stores a generated class-level opening brace.</summary>
    private const string ClassOpenBrace = "    {";

    /// <summary>Stores a generated class-level closing brace.</summary>
    private const string ClassCloseBrace = "    }";

    /// <summary>Stores a generated block-level opening brace.</summary>
    private const string BlockOpenBrace = "        {";

    /// <summary>Stores a generated block-level closing brace.</summary>
    private const string BlockCloseBrace = "        }";

    /// <summary>Stores a generated nested-block opening brace.</summary>
    private const string NestedBlockOpenBrace = "            {";

    /// <summary>Stores a generated nested-block closing brace.</summary>
    private const string NestedBlockCloseBrace = "            }";

    /// <summary>Stores the modern-framework conditional-compilation directive.</summary>
    private const string Net5OrGreaterDirective = "#if NET5_0_OR_GREATER";

    /// <summary>Stores the conditional-compilation terminator.</summary>
    private const string EndIfDirective = "#endif";

    /// <summary>Stores the generated named identifier argument prefix.</summary>
    private const string IdArgumentPrefix = ", id: \"";

    /// <summary>Stores the generated tag comparison prefix.</summary>
    private const string OrdinalTagComparisonPrefix = " || string.Equals(tag, \"";

    /// <summary>Stores the generated ordinal tag comparison suffix.</summary>
    private const string OrdinalTagComparisonSuffix = "\", StringComparison.OrdinalIgnoreCase)";

    /// <summary>Stores an indented generated false return statement.</summary>
    private const string IndentedReturnFalse = "            return false;";

    /// <summary>Stores the generated asynchronous result method prefix.</summary>
    private const string AsyncResultMethodPrefix =
        "    public System.Threading.Tasks.Task<global::IoT.DriverCore.Core.TagOperationResult<";

    /// <summary>Stores the generated trimming-annotation prefix.</summary>
    private const string RequiresUnreferencedCodePrefix = "    [RequiresUnreferencedCode(";

    /// <summary>Stores the generated structured-write dictionary prefix.</summary>
    private const string GeneratedDictionaryPrefix = "System.Collections.Generic.Dictionary<string, ";

    /// <summary>Stores the generated structured-write list prefix.</summary>
    private const string GeneratedStructuredWriteListPrefix =
        "System.Collections.Generic.List<(string MemberAddress, ";

    /// <summary>Stores the default PLC notification cycle time in milliseconds.</summary>
    private const int DefaultCycleTime = 100;

    /// <summary>Defines the attributes consumed by this source generator.</summary>
    private const string AttributeSource = """
// <auto-generated/>
#nullable enable
namespace IoT.DriverCore.TwinCATRx;

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class TwinCatReactiveStreamAttribute : System.Attribute
{
    public TwinCatReactiveStreamAttribute(string variable, System.Type dataType)
    {
        Variable = variable;
        DataType = dataType;
    }

    public string Variable { get; }

    public System.Type DataType { get; }

    public string? Id { get; set; }

    public string? PropertyName { get; set; }

    public string? ObservableName { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class TwinCatPlcConnectionAttribute : System.Attribute
{
    public TwinCatPlcConnectionAttribute(string adsAddress, int port)
    {
        AdsAddress = adsAddress;
        Port = port;
    }

    public string AdsAddress { get; }

    public int Port { get; }

    public string? SettingsId { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class DirectNotificationAttribute : System.Attribute
{
    public DirectNotificationAttribute(string address)
    {
        Address = address;
    }

    public string Address { get; }

    public int CycleTime { get; set; } = 100;

    public int ArraySize { get; set; } = -1;

    public string? Id { get; set; }

    public string? ObservableName { get; set; }

    public bool CanWrite { get; set; } = true;

    public string? WriteAddress { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class StructuredNotificationAttribute : System.Attribute
{
    public StructuredNotificationAttribute(string address)
    {
        Address = address;
    }

    public StructuredNotificationAttribute(string address, string memberAddress)
    {
        Address = address;
        MemberAddress = memberAddress;
    }

    public string Address { get; }

    public string? MemberAddress { get; set; }

    public int CycleTime { get; set; } = 100;

    public int ArraySize { get; set; } = -1;

    public string? Id { get; set; }

    public string? ObservableName { get; set; }

    public bool CanWrite { get; set; } = true;

    public string? WriteAddress { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class WriteOnlyAttribute : System.Attribute
{
    public WriteOnlyAttribute(string address)
    {
        Address = address;
    }

    public string Address { get; }

    public int ArraySize { get; set; } = -1;

    public string? Id { get; set; }
}
""";

    /// <summary>Identifies the generated TwinCATRx API surface.</summary>
    private enum ApiSurface
    {
        /// <summary>The lean ReactiveUI.Primitives surface.</summary>
        Lean,

        /// <summary>The System.Reactive-compatible surface.</summary>
        Reactive,
    }

    /// <summary>Initializes the incremental generator pipeline.</summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("TwinCatReactiveStreamAttribute.Lean.g.cs", SourceText.From(AttributeSource, Encoding.UTF8));
            ctx.AddSource(
                "TwinCatReactiveStreamAttribute.Reactive.g.cs",
                SourceText.From(
                    AttributeSource.Replace(
                        "namespace IoT.DriverCore.TwinCATRx;",
                        "namespace IoT.DriverCore.TwinCATRx.Reactive;"),
                    Encoding.UTF8));
        });

        var legacyCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                TwinCatReactiveStreamAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => GetLegacyStream(ctx))
            .Where(static stream => stream is not null);

        var connectionCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                TwinCatPlcConnectionAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => GetConnection(ctx))
            .Where(static connection => connection is not null);

        var reactiveLegacyCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReactiveTwinCatReactiveStreamAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => GetLegacyStream(ctx))
            .Where(static stream => stream is not null);

        var reactiveConnectionCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReactiveTwinCatPlcConnectionAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => GetConnection(ctx))
            .Where(static connection => connection is not null);

        context.RegisterSourceOutput(legacyCandidates.Collect(), ExecuteLegacy);
        context.RegisterSourceOutput(connectionCandidates.Collect(), ExecuteConnections);
        context.RegisterSourceOutput(reactiveLegacyCandidates.Collect(), ExecuteLegacy);
        context.RegisterSourceOutput(reactiveConnectionCandidates.Collect(), ExecuteConnections);
    }
}
