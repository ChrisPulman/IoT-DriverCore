// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CP.TwinCatRx.SourceGenerators;

/// <summary>Generates TwinCAT reactive stream binding members.</summary>
public sealed partial class TwinCatReactiveStreamGenerator : IIncrementalGenerator
{
    /// <summary>Creates a stream specification from an attributed class.</summary>
    /// <param name="context">The attributed generator context.</param>
    /// <returns>The stream specification, or <c>null</c> when the attribute is invalid.</returns>
    private static LegacyStreamSpec? GetLegacyStream(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol || context.Attributes.Length == 0)
        {
            return null;
        }

        var surface = GetApiSurface(context.Attributes[0]);
        var specs = new List<LegacyReactivePropertySpec>();
        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length != 2)
            {
                continue;
            }

            var variable = attribute.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(variable) ||
                attribute.ConstructorArguments[1].Value is not INamedTypeSymbol dataType)
            {
                continue;
            }

            var propertyName = GetNamedString(attribute, "PropertyName") ?? SanitizeIdentifier(variable!);
            var observableName = GetNamedString(attribute, ObservableNameArgument) ?? (propertyName + ObservableSuffix);
            specs.Add(new LegacyReactivePropertySpec(
                variable!,
                dataType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GetNamedString(attribute, "Id"),
                propertyName,
                observableName));
        }

        return specs.Count == 0
            ? null
            : new LegacyStreamSpec(
                GetNamespace(classSymbol),
                classSymbol.Name,
                GetAccessibility(classSymbol),
                surface,
                specs);
    }

    /// <summary>Creates a PLC connection specification from an attributed class.</summary>
    /// <param name="context">The attributed generator context.</param>
    /// <returns>The connection specification, or <c>null</c> when the attribute is invalid.</returns>
    private static ConnectionSpec? GetConnection(GeneratorAttributeSyntaxContext context)
    {
        if (!TryGetConnectionValues(context, out var classSymbol, out var adsAddress, out var port, out var settingsId))
        {
            return null;
        }

        var surface = GetApiSurface(context.Attributes[0]);
        var properties = new List<PlcPropertySpec>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property || property.IsStatic)
            {
                continue;
            }

            var propertySpec = GetPlcProperty(property, surface);
            if (propertySpec is not null)
            {
                properties.Add(propertySpec);
            }
        }

        return new ConnectionSpec(
                GetNamespace(classSymbol),
                classSymbol.Name,
                GetAccessibility(classSymbol),
                adsAddress,
                port,
                settingsId,
                properties)
        {
            Surface = surface,
        };
    }

    /// <summary>Tries to read class-level PLC connection values.</summary>
    /// <param name="context">The attributed generator context.</param>
    /// <param name="classSymbol">The class symbol.</param>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    /// <param name="settingsId">The settings identifier.</param>
    /// <returns><c>true</c> when connection values were read.</returns>
    private static bool TryGetConnectionValues(
        GeneratorAttributeSyntaxContext context,
        out INamedTypeSymbol classSymbol,
        out string adsAddress,
        out int port,
        out string settingsId)
    {
        classSymbol = null!;
        adsAddress = string.Empty;
        port = 0;
        settingsId = string.Empty;
        if (context.TargetSymbol is not INamedTypeSymbol targetClass || context.Attributes.Length == 0)
        {
            return false;
        }

        var connectionAttribute = context.Attributes[0];
        if (connectionAttribute.ConstructorArguments.Length != 2 ||
            connectionAttribute.ConstructorArguments[0].Value is not string targetAddress ||
            connectionAttribute.ConstructorArguments[1].Value is not int targetPort)
        {
            return false;
        }

        classSymbol = targetClass;
        adsAddress = targetAddress;
        port = targetPort;
        settingsId = GetNamedString(connectionAttribute, "SettingsId") ?? targetClass.Name;
        return true;
    }

    /// <summary>Creates a PLC property specification from an attributed property.</summary>
    /// <param name="property">The property symbol.</param>
    /// <param name="surface">The API surface selected by the connection attribute.</param>
    /// <returns>The property specification, or <c>null</c> when no supported attribute is present.</returns>
    private static PlcPropertySpec? GetPlcProperty(IPropertySymbol property, ApiSurface surface)
    {
        var directAttributeName = surface == ApiSurface.Reactive
            ? ReactiveDirectNotificationAttributeName
            : DirectNotificationAttributeName;
        var structuredAttributeName = surface == ApiSurface.Reactive
            ? ReactiveStructuredNotificationAttributeName
            : StructuredNotificationAttributeName;
        var writeOnlyAttributeName = surface == ApiSurface.Reactive
            ? ReactiveWriteOnlyAttributeName
            : WriteOnlyAttributeName;
        foreach (var attribute in property.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            if (attributeName == directAttributeName)
            {
                return GetDirectProperty(property, attribute);
            }

            if (attributeName == structuredAttributeName)
            {
                return GetStructuredProperty(property, attribute);
            }

            if (attributeName == writeOnlyAttributeName)
            {
                return GetWriteOnlyProperty(property, attribute);
            }
        }

        return null;
    }

    /// <summary>Creates a direct notification property specification.</summary>
    /// <param name="property">The property symbol.</param>
    /// <param name="attribute">The attribute data.</param>
    /// <returns>The property specification, or <c>null</c> when invalid.</returns>
    private static PlcPropertySpec? GetDirectProperty(IPropertySymbol property, AttributeData attribute)
    {
        var address = GetConstructorString(attribute, 0);
        return string.IsNullOrWhiteSpace(address)
            ? null
            : new PlcPropertySpec(
                new PlcPropertyIdentity(
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    GetNamedString(attribute, ObservableNameArgument) ?? (property.Name + ObservableSuffix)),
                new PlcAddressSpec(
                    DirectKind,
                    address!,
                    null,
                    GetNamedString(attribute, "WriteAddress"),
                    GetNamedString(attribute, "Id")),
                new PlcNotificationSpec(
                    GetNamedInt(attribute, "CycleTime", DefaultCycleTime),
                    GetNamedInt(attribute, ArraySizeArgument, -1)),
                GetNamedBool(attribute, "CanWrite", true));
    }

    /// <summary>Creates a structured notification property specification.</summary>
    /// <param name="property">The property symbol.</param>
    /// <param name="attribute">The attribute data.</param>
    /// <returns>The property specification, or <c>null</c> when invalid.</returns>
    private static PlcPropertySpec? GetStructuredProperty(IPropertySymbol property, AttributeData attribute)
    {
        var address = GetConstructorString(attribute, 0);
        var memberAddress = GetConstructorString(attribute, 1) ?? GetNamedString(attribute, "MemberAddress");
        return string.IsNullOrWhiteSpace(address)
            ? null
            : new PlcPropertySpec(
                new PlcPropertyIdentity(
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    GetNamedString(attribute, ObservableNameArgument) ?? (property.Name + ObservableSuffix)),
                new PlcAddressSpec(
                    StructuredKind,
                    address!,
                    memberAddress,
                    GetNamedString(attribute, "WriteAddress"),
                    GetNamedString(attribute, "Id")),
                new PlcNotificationSpec(
                    GetNamedInt(attribute, "CycleTime", DefaultCycleTime),
                    GetNamedInt(attribute, ArraySizeArgument, -1)),
                GetNamedBool(attribute, "CanWrite", true));
    }

    /// <summary>Creates a write-only property specification.</summary>
    /// <param name="property">The property symbol.</param>
    /// <param name="attribute">The attribute data.</param>
    /// <returns>The property specification, or <c>null</c> when invalid.</returns>
    private static PlcPropertySpec? GetWriteOnlyProperty(IPropertySymbol property, AttributeData attribute)
    {
        var address = GetConstructorString(attribute, 0);
        return string.IsNullOrWhiteSpace(address)
            ? null
            : new PlcPropertySpec(
                new PlcPropertyIdentity(
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    property.Name + ObservableSuffix),
                new PlcAddressSpec(
                    WriteOnlyKind,
                    address!,
                    null,
                    null,
                    GetNamedString(attribute, "Id")),
                new PlcNotificationSpec(
                    DefaultCycleTime,
                    GetNamedInt(attribute, ArraySizeArgument, -1)),
                true);
    }

    /// <summary>Emits generated source for all collected legacy stream specifications.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="streams">The collected stream specifications.</param>
    private static void ExecuteLegacy(SourceProductionContext context, ImmutableArray<LegacyStreamSpec?> streams)
    {
        var groups = new Dictionary<string, List<LegacyStreamSpec>>();
        foreach (var stream in streams)
        {
            if (stream is null)
            {
                continue;
            }

            var key = $"{stream.Surface}:{stream.Namespace}.{stream.ClassName}";
            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups[key] = group;
            }

            group.Add(stream);
        }

        foreach (var group in groups.Values)
        {
            var spec = group[0];
            var properties = new List<LegacyReactivePropertySpec>();
            foreach (var stream in group)
            {
                properties.AddRange(stream.Properties);
            }

            context.AddSource(
                GetHintName(spec.Namespace, spec.ClassName, $"{spec.Surface}.TwinCatReactiveStream"),
                SourceText.From(GenerateLegacy(spec, properties), Encoding.UTF8));
        }
    }

    /// <summary>Emits generated source for all collected PLC connection specifications.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="connections">The collected connection specifications.</param>
    private static void ExecuteConnections(SourceProductionContext context, ImmutableArray<ConnectionSpec?> connections)
    {
        foreach (var connection in connections)
        {
            if (connection is null)
            {
                continue;
            }

            context.AddSource(
                GetHintName(
                    connection.Namespace,
                    connection.ClassName,
                    $"{connection.Surface}.TwinCatPlcConnection"),
                SourceText.From(GenerateConnection(connection), Encoding.UTF8));
        }
    }
}
