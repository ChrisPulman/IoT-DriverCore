// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace IoT.DriverCore.TwinCATRx.SourceGenerators;

/// <summary>Generates TwinCAT reactive stream binding members.</summary>
public sealed partial class TwinCatReactiveStreamGenerator : IIncrementalGenerator
{
    /// <summary>Gets unique notification registrations.</summary>
    /// <param name="properties">The PLC property specifications.</param>
    /// <returns>The unique notification registrations.</returns>
    private static List<NotificationRegistration> GetNotificationRegistrations(
        IReadOnlyList<PlcPropertySpec> properties)
    {
        var registrations = new List<NotificationRegistration>();
        foreach (var property in properties)
        {
            if (property.Kind == WriteOnlyKind || ContainsVariable(registrations, property.Address, static r => r.Variable))
            {
                continue;
            }

            registrations.Add(new NotificationRegistration(property.Address, property.CycleTime, property.ArraySize));
        }

        return registrations;
    }

    /// <summary>Gets unique write variable registrations.</summary>
    /// <param name="properties">The PLC property specifications.</param>
    /// <returns>The unique write variable registrations.</returns>
    private static List<WriteRegistration> GetWriteRegistrations(IReadOnlyList<PlcPropertySpec> properties)
    {
        var registrations = new List<WriteRegistration>();
        var structuredVariables = GetStructuredVariables(properties);
        foreach (var property in properties)
        {
            if (!property.IsWritable)
            {
                continue;
            }

            var writeAddress = GetWriteAddress(property);
            var registrationAddress = GetWriteRegistrationAddress(property, structuredVariables);
            AddWriteRegistration(registrations, registrationAddress, property.ArraySize);
            if (!string.Equals(writeAddress, registrationAddress, StringComparison.OrdinalIgnoreCase))
            {
                AddWriteRegistration(registrations, writeAddress, property.ArraySize);
            }
        }

        return registrations;
    }

    /// <summary>Adds a write registration when it does not already exist.</summary>
    /// <param name="registrations">The write registrations.</param>
    /// <param name="writeAddress">The write address.</param>
    /// <param name="arraySize">The array size.</param>
    private static void AddWriteRegistration(List<WriteRegistration> registrations, string writeAddress, int arraySize)
    {
        if (ContainsVariable(registrations, writeAddress, static r => r.Variable))
        {
            return;
        }

        registrations.Add(new WriteRegistration(writeAddress, arraySize));
    }

    /// <summary>Gets unique structured notification root variables.</summary>
    /// <param name="properties">The PLC property specifications.</param>
    /// <returns>The unique structured notification root variables.</returns>
    private static List<string> GetStructuredVariables(IReadOnlyList<PlcPropertySpec> properties)
    {
        var variables = new List<string>();
        foreach (var property in properties)
        {
            if (property.Kind != StructuredKind ||
                string.IsNullOrWhiteSpace(property.MemberAddress) ||
                ContainsString(variables, property.Address))
            {
                continue;
            }

            variables.Add(property.Address);
        }

        return variables;
    }

    /// <summary>Gets write-capable property specifications.</summary>
    /// <param name="properties">The PLC property specifications.</param>
    /// <returns>The write-capable properties.</returns>
    private static List<PlcPropertySpec> GetWriteProperties(IReadOnlyList<PlcPropertySpec> properties)
    {
        var writableProperties = new List<PlcPropertySpec>();
        foreach (var property in properties)
        {
            if (property.IsWritable)
            {
                writableProperties.Add(property);
            }
        }

        return writableProperties;
    }

    /// <summary>Gets write-capable structured property specifications.</summary>
    /// <param name="properties">The write-capable PLC property specifications.</param>
    /// <param name="structuredVariables">The structured root variables.</param>
    /// <returns>The structured write properties.</returns>
    private static List<StructuredWritePropertySpec> GetStructuredWriteProperties(
        IReadOnlyList<PlcPropertySpec> properties,
        IReadOnlyList<string> structuredVariables)
    {
        var structuredWriteProperties = new List<StructuredWritePropertySpec>();
        foreach (var property in properties)
        {
            var target = GetStructuredWriteTarget(property, structuredVariables);
            if (target is not null)
            {
                structuredWriteProperties.Add(new StructuredWritePropertySpec(property, target));
            }
        }

        return structuredWriteProperties;
    }

    /// <summary>Determines whether a list contains a registration whose variable matches.</summary>
    /// <typeparam name="T">The registration type.</typeparam>
    /// <param name="registrations">The existing registrations.</param>
    /// <param name="variable">The variable to find.</param>
    /// <param name="getVariable">Selector that returns the variable name for a registration.</param>
    /// <returns><c>true</c> when a matching registration exists.</returns>
    private static bool ContainsVariable<T>(List<T> registrations, string variable, Func<T, string> getVariable)
    {
        for (var i = 0; i < registrations.Count; i++)
        {
            if (string.Equals(getVariable(registrations[i]), variable, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a string already exists.</summary>
    /// <param name="values">The existing values.</param>
    /// <param name="value">The value to find.</param>
    /// <returns><c>true</c> when the value exists.</returns>
    private static bool ContainsString(List<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the PLC write address for a property.</summary>
    /// <param name="property">The PLC property specification.</param>
    /// <returns>The PLC write address.</returns>
    private static string GetWriteAddress(PlcPropertySpec property) =>
        property switch
        {
            { WriteAddress: { } writeAddress } when !string.IsNullOrWhiteSpace(writeAddress) => writeAddress,
            { Kind: StructuredKind, MemberAddress: { } memberAddress }
                when !string.IsNullOrWhiteSpace(memberAddress) =>
                CombineAddress(property.Address, memberAddress),
            _ => property.Address,
        };

    /// <summary>Gets the settings write registration address for a property.</summary>
    /// <param name="property">The PLC property specification.</param>
    /// <param name="structuredVariables">The structured root variables.</param>
    /// <returns>The write registration address.</returns>
    private static string GetWriteRegistrationAddress(
        PlcPropertySpec property,
        IReadOnlyList<string> structuredVariables)
    {
        var structuredTarget = GetStructuredWriteTarget(property, structuredVariables);
        return structuredTarget?.RootAddress ?? GetWriteAddress(property);
    }

    /// <summary>Gets the structured write target for a property.</summary>
    /// <param name="property">The PLC property specification.</param>
    /// <param name="structuredVariables">The structured root variables.</param>
    /// <returns>The structured write target, or <c>null</c> when the property is not structure-backed.</returns>
    private static StructuredWriteTarget? GetStructuredWriteTarget(
        PlcPropertySpec property,
        IReadOnlyList<string> structuredVariables)
    {
        if (property.Kind == StructuredKind && !string.IsNullOrWhiteSpace(property.MemberAddress))
        {
            return new StructuredWriteTarget(property.Address, property.MemberAddress!);
        }

        if (property.Kind != WriteOnlyKind)
        {
            return null;
        }

        for (var i = 0; i < structuredVariables.Count; i++)
        {
            var root = structuredVariables[i];
            var prefix = root.EndsWith(".", StringComparison.Ordinal) ? root : $"{root}.";
            if (property.Address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new StructuredWriteTarget(root, property.Address.Substring(prefix.Length));
            }
        }

        return null;
    }

    /// <summary>Combines a structured root and member address.</summary>
    /// <param name="root">The structured root address.</param>
    /// <param name="member">The member address.</param>
    /// <returns>The combined address.</returns>
    private static string CombineAddress(string root, string member) =>
        root.EndsWith(".", StringComparison.Ordinal) || member.StartsWith(".", StringComparison.Ordinal)
            ? root + member
            : $"{root}.{member}";

    /// <summary>Gets whether properties include any notification tags.</summary>
    /// <param name="properties">The PLC property specifications.</param>
    /// <returns><c>true</c> when at least one notification property exists.</returns>
    private static bool HasNotificationProperties(IReadOnlyList<PlcPropertySpec> properties)
    {
        foreach (var property in properties)
        {
            if (property.Kind != WriteOnlyKind)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends a generated global using alias.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="alias">The using alias.</param>
    /// <param name="targetNamespace">The target namespace.</param>
    /// <param name="typeName">The aliased type name.</param>
    private static void AppendUsingAlias(
        StringBuilder sb,
        string alias,
        string targetNamespace,
        string typeName)
    {
        _ = sb.Append(UsingDirectivePrefix).Append(alias).Append(" = global::")
            .Append(targetNamespace).Append('.').Append(typeName).AppendLine(";");
    }

    /// <summary>Appends a file-scoped namespace when present.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="ns">The namespace.</param>
    private static void AppendNamespace(StringBuilder sb, string? ns)
    {
        if (ns is null)
        {
            return;
        }

        _ = sb.Append("namespace ").Append(ns).AppendLine(";")
            .AppendLine();
    }

    /// <summary>Gets a source hint name.</summary>
    /// <param name="ns">The namespace.</param>
    /// <param name="className">The class name.</param>
    /// <param name="suffix">The generated file suffix.</param>
    /// <returns>The source hint name.</returns>
    private static string GetHintName(string? ns, string className, string suffix) =>
        string.IsNullOrWhiteSpace(ns)
            ? $"{className}.{suffix}.g.cs"
            : $"{ns}.{className}.{suffix}.g.cs";

    /// <summary>Gets the namespace for a named type.</summary>
    /// <param name="symbol">The named type symbol.</param>
    /// <returns>The namespace, or <c>null</c> for the global namespace.</returns>
    private static string? GetNamespace(INamedTypeSymbol symbol) =>
        symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();

    /// <summary>Gets the API surface represented by an attribute.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <returns>The selected API surface.</returns>
    private static ApiSurface GetApiSurface(AttributeData attribute) =>
        attribute.AttributeClass?.ToDisplayString()
            .StartsWith($"{ReactiveLibraryNamespace}.", StringComparison.Ordinal) == true
            ? ApiSurface.Reactive
            : ApiSurface.Lean;

    /// <summary>Gets the library namespace for an API surface.</summary>
    /// <param name="surface">The API surface.</param>
    /// <returns>The library namespace.</returns>
    private static string GetLibraryNamespace(ApiSurface surface) =>
        surface == ApiSurface.Reactive ? ReactiveLibraryNamespace : LeanLibraryNamespace;

    /// <summary>Gets the core namespace for an API surface.</summary>
    /// <param name="surface">The API surface.</param>
    /// <returns>The core namespace.</returns>
    private static string GetCoreNamespace(ApiSurface surface) =>
        surface == ApiSurface.Reactive ? ReactiveCoreNamespace : LeanCoreNamespace;

    /// <summary>Gets the collections namespace for an API surface.</summary>
    /// <param name="surface">The API surface.</param>
    /// <returns>The collections namespace.</returns>
    private static string GetCollectionsNamespace(ApiSurface surface) =>
        surface == ApiSurface.Reactive ? ReactiveCollectionsNamespace : LeanCollectionsNamespace;

    /// <summary>Gets a constructor string argument.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="index">The constructor argument index.</param>
    /// <returns>The constructor string value, or <c>null</c> when unavailable.</returns>
    private static string? GetConstructorString(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as string : null;

    /// <summary>Gets a named string argument value from an attribute.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="name">The named argument name.</param>
    /// <returns>The named string value, or <c>null</c> when it is not present.</returns>
    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    /// <summary>Gets a named integer argument value from an attribute.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="name">The named argument name.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The named integer value, or the default value.</returns>
    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is int value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    /// <summary>Gets a named Boolean argument value from an attribute.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="name">The named argument name.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The named Boolean value, or the default value.</returns>
    private static bool GetNamedBool(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is bool value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    /// <summary>Converts Roslyn accessibility to generated C# accessibility text.</summary>
    /// <param name="symbol">The target class symbol.</param>
    /// <returns>The generated accessibility text.</returns>
    private static string GetAccessibility(INamedTypeSymbol symbol) =>
        symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal",
        };

    /// <summary>Creates a legal C# identifier from a PLC variable name.</summary>
    /// <param name="variable">The PLC variable name.</param>
    /// <returns>The sanitized identifier.</returns>
    private static string SanitizeIdentifier(string variable)
    {
        var builder = new StringBuilder(variable.Length);
        foreach (var character in variable)
        {
            if (char.IsLetterOrDigit(character))
            {
                _ = builder.Append(character);
            }
        }

        var text = builder.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Value";
        }

        return char.IsDigit(text[0]) ? $"Value{text}" : text;
    }

    /// <summary>Converts a PascalCase identifier to camelCase.</summary>
    /// <param name="value">The identifier to convert.</param>
    /// <returns>The camelCase identifier.</returns>
    private static string ToCamel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "value";
        }

        var characters = value.ToCharArray();
        characters[0] = char.ToLowerInvariant(characters[0]);
        return new string(characters);
    }

    /// <summary>Gets a local variable name for a structured notification root.</summary>
    /// <param name="index">The structured notification index.</param>
    /// <returns>The local variable name.</returns>
    private static string GetStructureLocalName(int index) =>
        $"structure{index.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Escapes a string for inclusion in generated C# source.</summary>
    /// <param name="value">The string value to escape.</param>
    /// <returns>The escaped string.</returns>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
