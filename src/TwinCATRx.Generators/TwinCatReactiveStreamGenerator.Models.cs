// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CP.TwinCatRx.SourceGenerators;

/// <summary>Generates TwinCAT reactive stream binding members.</summary>
public sealed partial class TwinCatReactiveStreamGenerator : IIncrementalGenerator
{
    /// <summary>Describes an attributed legacy stream class and its reactive properties.</summary>
    private sealed class LegacyStreamSpec
    {
        /// <summary>Initializes a new instance of the <see cref="LegacyStreamSpec"/> class.</summary>
        /// <param name="ns">The containing namespace.</param>
        /// <param name="className">The class name.</param>
        /// <param name="accessibility">The class accessibility.</param>
        /// <param name="surface">The generated API surface.</param>
        /// <param name="properties">The reactive properties.</param>
        public LegacyStreamSpec(
            string? ns,
            string className,
            string accessibility,
            ApiSurface surface,
            IReadOnlyList<LegacyReactivePropertySpec> properties)
        {
            Namespace = ns;
            ClassName = className;
            Accessibility = accessibility;
            Surface = surface;
            Properties = properties;
        }

        /// <summary>Gets the containing namespace.</summary>
        public string? Namespace { get; }

        /// <summary>Gets the class name.</summary>
        public string ClassName { get; }

        /// <summary>Gets the class accessibility.</summary>
        public string Accessibility { get; }

        /// <summary>Gets the generated API surface.</summary>
        public ApiSurface Surface { get; }

        /// <summary>Gets the reactive properties.</summary>
        public IReadOnlyList<LegacyReactivePropertySpec> Properties { get; }
    }

    /// <summary>Describes a generated legacy reactive property.</summary>
    private sealed class LegacyReactivePropertySpec
    {
        /// <summary>Initializes a new instance of the <see cref="LegacyReactivePropertySpec"/> class.</summary>
        /// <param name="variable">The PLC variable name.</param>
        /// <param name="typeName">The generated property type name.</param>
        /// <param name="id">The optional stream identifier.</param>
        /// <param name="propertyName">The generated property name.</param>
        /// <param name="observableName">The generated observable name.</param>
        public LegacyReactivePropertySpec(
            string variable,
            string typeName,
            string? id,
            string propertyName,
            string observableName)
        {
            Variable = variable;
            TypeName = typeName;
            Id = id;
            PropertyName = propertyName;
            ObservableName = observableName;
        }

        /// <summary>Gets the PLC variable name.</summary>
        public string Variable { get; }

        /// <summary>Gets the generated property type name.</summary>
        public string TypeName { get; }

        /// <summary>Gets the optional stream identifier.</summary>
        public string? Id { get; }

        /// <summary>Gets the generated property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the generated observable name.</summary>
        public string ObservableName { get; }
    }

    /// <summary>Describes a generated PLC connection class.</summary>
    private sealed class ConnectionSpec
    {
        /// <summary>Initializes a new instance of the <see cref="ConnectionSpec"/> class.</summary>
        /// <param name="ns">The containing namespace.</param>
        /// <param name="className">The class name.</param>
        /// <param name="accessibility">The class accessibility.</param>
        /// <param name="adsAddress">The ADS address.</param>
        /// <param name="port">The ADS port.</param>
        /// <param name="settingsId">The settings identifier.</param>
        /// <param name="properties">The PLC properties.</param>
        public ConnectionSpec(
            string? ns,
            string className,
            string accessibility,
            string adsAddress,
            int port,
            string settingsId,
            IReadOnlyList<PlcPropertySpec> properties)
        {
            Namespace = ns;
            ClassName = className;
            Accessibility = accessibility;
            AdsAddress = adsAddress;
            Port = port;
            SettingsId = settingsId;
            Properties = properties;
        }

        /// <summary>Gets the containing namespace.</summary>
        public string? Namespace { get; }

        /// <summary>Gets the class name.</summary>
        public string ClassName { get; }

        /// <summary>Gets the class accessibility.</summary>
        public string Accessibility { get; }

        /// <summary>Gets the ADS address.</summary>
        public string AdsAddress { get; }

        /// <summary>Gets the ADS port.</summary>
        public int Port { get; }

        /// <summary>Gets the settings identifier.</summary>
        public string SettingsId { get; }

        /// <summary>Gets or sets the generated API surface.</summary>
        public ApiSurface Surface { get; set; }

        /// <summary>Gets the PLC properties.</summary>
        public IReadOnlyList<PlcPropertySpec> Properties { get; }
    }

    /// <summary>Groups the generated property identity.</summary>
    private sealed class PlcPropertyIdentity
    {
        /// <summary>Initializes a new instance of the <see cref="PlcPropertyIdentity"/> class.</summary>
        /// <param name="propertyName">The generated property name.</param>
        /// <param name="typeName">The fully qualified property type name.</param>
        /// <param name="observableName">The generated observable name.</param>
        public PlcPropertyIdentity(string propertyName, string typeName, string observableName)
        {
            PropertyName = propertyName;
            TypeName = typeName;
            ObservableName = observableName;
        }

        /// <summary>Gets the generated property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the fully qualified property type name.</summary>
        public string TypeName { get; }

        /// <summary>Gets the generated observable name.</summary>
        public string ObservableName { get; }
    }

    /// <summary>Groups the PLC address metadata for a generated property.</summary>
    private sealed class PlcAddressSpec
    {
        /// <summary>Initializes a new instance of the <see cref="PlcAddressSpec"/> class.</summary>
        /// <param name="kind">The PLC tag kind.</param>
        /// <param name="address">The PLC address.</param>
        /// <param name="memberAddress">The optional structured member address.</param>
        /// <param name="writeAddress">The optional write address.</param>
        /// <param name="id">The optional identifier.</param>
        public PlcAddressSpec(string kind, string address, string? memberAddress, string? writeAddress, string? id)
        {
            Kind = kind;
            Address = address;
            MemberAddress = memberAddress;
            WriteAddress = writeAddress;
            Id = id;
        }

        /// <summary>Gets the PLC tag kind.</summary>
        public string Kind { get; }

        /// <summary>Gets the PLC address.</summary>
        public string Address { get; }

        /// <summary>Gets the optional structured member address.</summary>
        public string? MemberAddress { get; }

        /// <summary>Gets the optional write address.</summary>
        public string? WriteAddress { get; }

        /// <summary>Gets the optional identifier.</summary>
        public string? Id { get; }
    }

    /// <summary>Groups notification timing and array metadata.</summary>
    private sealed class PlcNotificationSpec
    {
        /// <summary>Initializes a new instance of the <see cref="PlcNotificationSpec"/> class.</summary>
        /// <param name="cycleTime">The notification cycle time.</param>
        /// <param name="arraySize">The optional array size.</param>
        public PlcNotificationSpec(int cycleTime, int arraySize)
        {
            CycleTime = cycleTime;
            ArraySize = arraySize;
        }

        /// <summary>Gets the notification cycle time.</summary>
        public int CycleTime { get; }

        /// <summary>Gets the optional array size.</summary>
        public int ArraySize { get; }
    }

    /// <summary>Describes an attributed PLC property.</summary>
    private sealed class PlcPropertySpec
    {
        /// <summary>Initializes a new instance of the <see cref="PlcPropertySpec"/> class.</summary>
        /// <param name="identity">The generated property identity.</param>
        /// <param name="address">The PLC address metadata.</param>
        /// <param name="notification">The PLC notification metadata.</param>
        /// <param name="canWrite">A value indicating whether writes should be generated.</param>
        public PlcPropertySpec(
            PlcPropertyIdentity identity,
            PlcAddressSpec address,
            PlcNotificationSpec notification,
            bool canWrite)
        {
            PropertyName = identity.PropertyName;
            TypeName = identity.TypeName;
            Kind = address.Kind;
            Address = address.Address;
            MemberAddress = address.MemberAddress;
            WriteAddress = address.WriteAddress;
            Id = address.Id;
            ObservableName = identity.ObservableName;
            CycleTime = notification.CycleTime;
            ArraySize = notification.ArraySize;
            IsWritable = address.Kind == WriteOnlyKind || canWrite;
            SubjectField = $"_{ToCamel(identity.PropertyName)}Subject";
            SetterName = $"Set{identity.PropertyName}";
            ReadMethodName = $"Read{identity.PropertyName}";
            WriteMethodName = $"Write{identity.PropertyName}";
        }

        /// <summary>Gets the property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the property type name.</summary>
        public string TypeName { get; }

        /// <summary>Gets the PLC tag kind.</summary>
        public string Kind { get; }

        /// <summary>Gets the PLC address.</summary>
        public string Address { get; }

        /// <summary>Gets the structured member address.</summary>
        public string? MemberAddress { get; }

        /// <summary>Gets the optional write address.</summary>
        public string? WriteAddress { get; }

        /// <summary>Gets the optional identifier.</summary>
        public string? Id { get; }

        /// <summary>Gets the observable property name.</summary>
        public string ObservableName { get; }

        /// <summary>Gets the notification cycle time.</summary>
        public int CycleTime { get; }

        /// <summary>Gets the array size.</summary>
        public int ArraySize { get; }

        /// <summary>Gets a value indicating whether writes should be generated.</summary>
        public bool IsWritable { get; }

        /// <summary>Gets the generated signal field name.</summary>
        public string SubjectField { get; }

        /// <summary>Gets the generated setter method name.</summary>
        public string SetterName { get; }

        /// <summary>Gets the generated read method name.</summary>
        public string ReadMethodName { get; }

        /// <summary>Gets the generated write method name.</summary>
        public string WriteMethodName { get; }
    }

    /// <summary>Describes a notification registration.</summary>
    private sealed class NotificationRegistration
    {
        /// <summary>Initializes a new instance of the <see cref="NotificationRegistration"/> class.</summary>
        /// <param name="variable">The notification variable.</param>
        /// <param name="cycleTime">The notification cycle time.</param>
        /// <param name="arraySize">The array size.</param>
        public NotificationRegistration(string variable, int cycleTime, int arraySize)
        {
            Variable = variable;
            CycleTime = cycleTime;
            ArraySize = arraySize;
        }

        /// <summary>Gets the notification variable.</summary>
        public string Variable { get; }

        /// <summary>Gets the notification cycle time.</summary>
        public int CycleTime { get; }

        /// <summary>Gets the array size.</summary>
        public int ArraySize { get; }
    }

    /// <summary>Describes a write registration.</summary>
    private sealed class WriteRegistration
    {
        /// <summary>Initializes a new instance of the <see cref="WriteRegistration"/> class.</summary>
        /// <param name="variable">The write variable.</param>
        /// <param name="arraySize">The array size.</param>
        public WriteRegistration(string variable, int arraySize)
        {
            Variable = variable;
            ArraySize = arraySize;
        }

        /// <summary>Gets the write variable.</summary>
        public string Variable { get; }

        /// <summary>Gets the array size.</summary>
        public int ArraySize { get; }
    }

    /// <summary>Describes a write-capable property and its structured target.</summary>
    private sealed class StructuredWritePropertySpec
    {
        /// <summary>Initializes a new instance of the <see cref="StructuredWritePropertySpec"/> class.</summary>
        /// <param name="property">The write-capable property.</param>
        /// <param name="target">The structured write target.</param>
        public StructuredWritePropertySpec(PlcPropertySpec property, StructuredWriteTarget target)
        {
            Property = property;
            Target = target;
        }

        /// <summary>Gets the write-capable property.</summary>
        public PlcPropertySpec Property { get; }

        /// <summary>Gets the structured write target.</summary>
        public StructuredWriteTarget Target { get; }
    }

    /// <summary>Describes a structured root/member write target.</summary>
    private sealed class StructuredWriteTarget
    {
        /// <summary>Initializes a new instance of the <see cref="StructuredWriteTarget"/> class.</summary>
        /// <param name="rootAddress">The structured root address.</param>
        /// <param name="memberAddress">The structured member address.</param>
        public StructuredWriteTarget(string rootAddress, string memberAddress)
        {
            RootAddress = rootAddress;
            MemberAddress = memberAddress;
        }

        /// <summary>Gets the structured root address.</summary>
        public string RootAddress { get; }

        /// <summary>Gets the structured member address.</summary>
        public string MemberAddress { get; }
    }
}
