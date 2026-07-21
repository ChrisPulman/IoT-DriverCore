// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModbusRx.Generators;

/// <summary>Generates Modbus-backed reactive properties and observable streams.</summary>
public sealed partial class ModbusReactiveStreamGenerator
{
    /// <summary>Describes a source property decorated as a Modbus reactive point.</summary>
    /// <param name="propertySymbol">The property symbol.</param>
    /// <param name="propertySyntax">The property syntax.</param>
    /// <param name="classSymbol">The containing class symbol.</param>
    /// <param name="kind">The Modbus point kind.</param>
    /// <param name="address">The Modbus address.</param>
    /// <param name="deviceOptions">The device options.</param>
    /// <param name="pointOptions">The point options.</param>
    private sealed class ModbusPoint(
        IPropertySymbol propertySymbol,
        PropertyDeclarationSyntax propertySyntax,
        INamedTypeSymbol classSymbol,
        ModbusPointKind kind,
        ushort address,
        DeviceOptions deviceOptions,
        PointOptions pointOptions)
    {
        /// <summary>Gets the property symbol.</summary>
        public IPropertySymbol PropertySymbol { get; } = propertySymbol;

        /// <summary>Gets the property declaration syntax.</summary>
        public PropertyDeclarationSyntax PropertySyntax { get; } = propertySyntax;

        /// <summary>Gets the containing class symbol.</summary>
        public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

        /// <summary>Gets the Modbus point kind.</summary>
        public ModbusPointKind Kind { get; } = kind;

        /// <summary>Gets the Modbus address.</summary>
        public ushort Address { get; } = address;

        /// <summary>Gets the device options.</summary>
        public DeviceOptions DeviceOptions { get; } = deviceOptions;

        /// <summary>Gets the point options.</summary>
        public PointOptions PointOptions { get; } = pointOptions;
    }

    /// <summary>Combines a source point with its generated type information.</summary>
    /// <param name="point">The source point.</param>
    /// <param name="type">The generated type information.</param>
    private sealed class GeneratedPoint(ModbusPoint point, ModbusTypeInfo type)
    {
        /// <summary>Gets the source point.</summary>
        public ModbusPoint Point { get; } = point;

        /// <summary>Gets the generated type information.</summary>
        public ModbusTypeInfo Type { get; } = type;
    }

    /// <summary>Contains generator options read from a device attribute.</summary>
    /// <param name="connectionMember">The connection member name.</param>
    /// <param name="tagClientMember">The optional logical-tag client member name.</param>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="defaultInterval">The default polling interval.</param>
    /// <param name="masterKind">The master kind.</param>
    /// <param name="apiRoot">The generated API root namespace.</param>
    private sealed class DeviceOptions(
        string connectionMember,
        string? tagClientMember,
        byte slaveAddress,
        double defaultInterval,
        string masterKind,
        string apiRoot)
    {
        /// <summary>Gets the connection member name.</summary>
        public string ConnectionMember { get; } = connectionMember;

        /// <summary>Gets the optional logical-tag client member name.</summary>
        public string? TagClientMember { get; } = tagClientMember;

        /// <summary>Gets the slave address.</summary>
        public byte SlaveAddress { get; } = slaveAddress;

        /// <summary>Gets the default polling interval.</summary>
        public double DefaultInterval { get; } = defaultInterval;

        /// <summary>Gets the master kind.</summary>
        public string MasterKind { get; } = masterKind;

        /// <summary>Gets the generated API root namespace.</summary>
        public string ApiRoot { get; } = apiRoot;

        /// <summary>Creates device options from a generated device attribute.</summary>
        /// <param name="attribute">The device attribute data.</param>
        /// <returns>The device options.</returns>
        public static DeviceOptions From(AttributeData attribute)
        {
            var connectionMember = "MasterStream";
            string? tagClientMember = null;
            var slaveAddress = (byte)1;
            var defaultInterval = 1000.0;
            var masterKind = "Auto";

            foreach (var argument in attribute.NamedArguments)
            {
                switch (argument.Key)
                {
                    case "ConnectionMember" when argument.Value.Value is string value:
                        {
                            connectionMember = value;
                            break;
                        }

                    case "SlaveAddress" when argument.Value.Value is byte value:
                        {
                            slaveAddress = value;
                            break;
                        }

                    case "DefaultInterval" when argument.Value.Value is double value:
                        {
                            defaultInterval = value;
                            break;
                        }

                    case "MasterKind" when argument.Value.Value is int value:
                        {
                            masterKind = value switch
                            {
                                SerialMasterKindValue => "Serial",
                                1 => "Ip",
                                _ => "Auto",
                            };

                            break;
                        }

                    case "TagClientMember" when argument.Value.Value is string value:
                        {
                            tagClientMember = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        }
                }
            }

            return new DeviceOptions(
                connectionMember,
                tagClientMember,
                slaveAddress,
                defaultInterval,
                masterKind,
                "global::ModbusRx");
        }

        /// <summary>Creates a copy with a resolved API root namespace.</summary>
        /// <param name="apiRoot">The API root namespace.</param>
        /// <returns>The updated device options.</returns>
        public DeviceOptions WithApiRoot(string apiRoot) =>
            new(ConnectionMember, TagClientMember, SlaveAddress, DefaultInterval, MasterKind, apiRoot);
    }

    /// <summary>Contains generator options read from a point attribute.</summary>
    /// <param name="count">The configured register or coil count.</param>
    /// <param name="swapWords">Whether register words are swapped for multi-register values.</param>
    /// <param name="tagName">The optional logical tag name.</param>
    private sealed class PointOptions(ushort count, bool swapWords, string? tagName)
    {
        /// <summary>Gets the configured register or coil count.</summary>
        public ushort Count { get; } = count;

        /// <summary>Gets a value indicating whether register words are swapped for multi-register values.</summary>
        public bool SwapWords { get; } = swapWords;

        /// <summary>Gets the optional logical tag name.</summary>
        public string? TagName { get; } = tagName;

        /// <summary>Creates point options from a generated point attribute.</summary>
        /// <param name="attribute">The point attribute data.</param>
        /// <returns>The point options.</returns>
        public static PointOptions From(AttributeData attribute)
        {
            var count = (ushort)0;
            var swapWords = true;
            string? tagName = null;

            foreach (var argument in attribute.NamedArguments)
            {
                switch (argument.Key)
                {
                    case "Count" when argument.Value.Value is ushort value:
                        {
                            count = value;
                            break;
                        }

                    case "SwapWords" when argument.Value.Value is bool value:
                        {
                            swapWords = value;
                            break;
                        }

                    case "TagName" when argument.Value.Value is string value:
                        {
                            tagName = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        }
                }
            }

            return new PointOptions(count, swapWords, tagName);
        }
    }

    /// <summary>Contains generated conversion metadata for a Modbus property type.</summary>
    /// <param name="typeName">The generated type name.</param>
    /// <param name="registerCount">The number of registers required to read the value.</param>
    /// <param name="conversionFactory">The generated conversion expression factory.</param>
    private sealed class ModbusTypeInfo(
        string typeName,
        ushort registerCount,
        Func<string, bool, string> conversionFactory)
    {
        /// <summary>Gets the generated type name.</summary>
        public string TypeName { get; } = typeName;

        /// <summary>Gets the number of registers required to read the value.</summary>
        public ushort RegisterCount { get; } = registerCount;

        /// <summary>Creates conversion metadata for a property type and Modbus point kind.</summary>
        /// <param name="typeSymbol">The property type symbol.</param>
        /// <param name="pointKind">The point kind.</param>
        /// <param name="apiRoot">The generated API root namespace.</param>
        /// <returns>The type metadata when the property type is supported.</returns>
        public static ModbusTypeInfo? Create(ITypeSymbol typeSymbol, ModbusPointKind pointKind, string apiRoot)
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var underlying = Nullable.GetUnderlyingTypeName(typeSymbol) ?? typeName;
            var nullableCast = underlying == typeName ? string.Empty : $"({typeName})";

            if (pointKind is ModbusPointKind.Coil or ModbusPointKind.DiscreteInput)
            {
                return underlying == "bool"
                    ? new ModbusTypeInfo(typeName, 1, (data, _) => $"{nullableCast}{data}[0]")
                    : null;
            }

            return underlying switch
            {
                "ushort" => new ModbusTypeInfo(typeName, 1, (data, _) => $"{nullableCast}{data}[0]"),
                "short" => new ModbusTypeInfo(
                    typeName,
                    1,
                    (data, _) => $"{nullableCast}unchecked((short){data}[0])"),
                "uint" => new ModbusTypeInfo(
                    typeName,
                    RegistersPer32BitValue,
                    (data, swap) => nullableCast + ToUInt32Expression(data, swap)),
                "int" => new ModbusTypeInfo(
                    typeName,
                    RegistersPer32BitValue,
                    (data, swap) => $"{nullableCast}unchecked((int){ToUInt32Expression(data, swap)})"),
                "float" => new ModbusTypeInfo(
                    typeName,
                    RegistersPer32BitValue,
                    (data, swap) => string.Concat(
                        nullableCast,
                        apiRoot,
                        $".Utility.ModbusUtility.ReadSingle(new global::System.ReadOnlySpan<ushort>({data}, 0, ",
                        $"{RegistersPer32BitValue}), {ToBoolLiteral(swap)})")),
                "double" => new ModbusTypeInfo(
                    typeName,
                    RegistersPer64BitValue,
                    (data, swap) => string.Concat(
                        nullableCast,
                        apiRoot,
                        $".Utility.ModbusUtility.ReadDouble(new global::System.ReadOnlySpan<ushort>({data}, 0, ",
                        $"{RegistersPer64BitValue}), {ToBoolLiteral(swap)})")),
                _ => null,
            };
        }

        /// <summary>Gets the generated conversion expression for a source data variable.</summary>
        /// <param name="dataName">The source data variable name.</param>
        /// <param name="swapWords">Whether multi-register words should be swapped.</param>
        /// <returns>The generated conversion expression.</returns>
        public string GetConversionExpression(string dataName, bool swapWords) =>
            conversionFactory(dataName, swapWords);

        /// <summary>Creates a generated expression that converts two registers to a 32-bit unsigned integer.</summary>
        /// <param name="data">The source data variable name.</param>
        /// <param name="swapWords">Whether register words should be swapped.</param>
        /// <returns>The generated conversion expression.</returns>
        private static string ToUInt32Expression(string data, bool swapWords)
        {
            var high = swapWords ? $"{data}[1]" : $"{data}[0]";
            var low = swapWords ? $"{data}[0]" : $"{data}[1]";
            return $"(((uint){high} << 16) | {low})";
        }

        /// <summary>Converts a boolean value to a generated C# literal.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The generated C# literal.</returns>
        private static string ToBoolLiteral(bool value) => value ? "true" : "false";
    }
}
