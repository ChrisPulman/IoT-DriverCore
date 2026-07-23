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
    /// <summary>Appends the settings factory method.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="spec">The PLC connection specification.</param>
    private static void AppendSettingsFactory(StringBuilder sb, ConnectionSpec spec)
    {
        var notifications = GetNotificationRegistrations(spec.Properties);
        var writeVariables = GetWriteRegistrations(spec.Properties);

        _ = sb.AppendLine("    public TwinCatRxSettings CreateTwinCatRxSettings()")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        var settings = new TwinCatRxSettings")
            .AppendLine(BlockOpenBrace)
            .Append("            AdsAddress = \"").Append(Escape(spec.AdsAddress)).AppendLine("\",")
            .Append("            Port = ").Append(spec.Port.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("            SettingsId = \"").Append(Escape(spec.SettingsId)).AppendLine("\"")
            .AppendLine("        };");

        foreach (var notification in notifications)
        {
            _ = sb.Append("        TwinCatRxCoreExtensions.AddNotification(settings, \"")
                .Append(Escape(notification.Variable))
                .Append("\", cycleTime: ")
                .Append(notification.CycleTime.ToString(CultureInfo.InvariantCulture))
                .Append(", arraySize: ")
                .Append(notification.ArraySize.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        }

        foreach (var writeVariable in writeVariables)
        {
            _ = sb.Append("        TwinCatRxCoreExtensions.AddWriteVariable(settings, \"")
                .Append(Escape(writeVariable.Variable))
                .Append("\", arraySize: ")
                .Append(writeVariable.ArraySize.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        }

        _ = sb.AppendLine("        return settings;")
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends the owned client connection method.</summary>
    /// <param name="sb">The string builder.</param>
    private static void AppendConnectMethod(StringBuilder sb)
    {
        _ = sb.AppendLine(Net5OrGreaterDirective)
            .AppendLine("    [RequiresDynamicCode(\"RxTcAdsClient generates PLC structure types at runtime.\")]")
            .Append(RequiresUnreferencedCodePrefix).Append('"').Append(ClientReflectionWarning).AppendLine("\")]")
            .AppendLine(EndIfDirective)
            .AppendLine("    public IDisposable ConnectTwinCatRx()")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        var client = new TwinCatRxClient();")
            .AppendLine("        var cleanup = new MultipleDisposable();")
            .AppendLine("        cleanup.Add(client);")
            .AppendLine("        cleanup.Add(BindTwinCatRx(client));")
            .AppendLine("        client.Connect(CreateTwinCatRxSettings());")
            .AppendLine("        return cleanup;")
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends the client binding method.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The PLC property specifications.</param>
    private static void AppendBindingMethod(StringBuilder sb, IReadOnlyList<PlcPropertySpec> properties)
    {
        _ = sb.AppendLine(Net5OrGreaterDirective)
            .Append(RequiresUnreferencedCodePrefix).Append('"')
            .Append(StructuredNotificationWarning).AppendLine("\")]")
            .AppendLine(EndIfDirective)
            .AppendLine("    public IDisposable BindTwinCatRx(TwinCatRxClientContract client)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        if (client == null)")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            throw new ArgumentNullException(nameof(client));")
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        _twinCatRxClient = client;")
            .AppendLine("        var logicalTags = new TwinCatRxLogicalTags(client);")
            .AppendLine("        _twinCatRxLogicalTags = logicalTags;")
            .AppendLine("        _twinCatRxStructures.Clear();")
            .AppendLine("        var subscriptions = new MultipleDisposable();")
            .AppendLine("        subscriptions.Add(logicalTags);")
            .AppendLine("        subscriptions.Add(Scope.Create(() =>")
            .AppendLine("        {")
            .AppendLine("            _twinCatRxStructures.Clear();")
            .AppendLine("            if (ReferenceEquals(_twinCatRxLogicalTags, logicalTags))")
            .AppendLine("            {")
            .AppendLine("                _twinCatRxLogicalTags = null;")
            .AppendLine("            }")
            .AppendLine("            if (ReferenceEquals(_twinCatRxClient, client))")
            .AppendLine("            {")
            .AppendLine("                _twinCatRxClient = null;")
            .AppendLine("            }")
            .AppendLine("        }));");

        AppendLogicalTagRegistrations(sb, properties);

        AppendStructuredBindings(sb, properties);
        AppendDirectBindings(sb, properties);

        _ = sb.AppendLine("        return subscriptions;")
            .AppendLine(ClassCloseBrace)
            .AppendLine();

        foreach (var property in properties)
        {
            _ = sb.Append("    private void ").Append(property.SetterName).Append('(')
                .Append(property.TypeName).AppendLine(" value)")
                .AppendLine(ClassOpenBrace)
                .Append("        ").Append(property.PropertyName).AppendLine(" = value;")
                .Append("        ").Append(property.SubjectField).AppendLine(".OnNext(value);")
                .AppendLine(ClassCloseBrace)
                .AppendLine();
        }
    }

    /// <summary>Appends structured notification subscriptions.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The PLC property specifications.</param>
    private static void AppendStructuredBindings(StringBuilder sb, IReadOnlyList<PlcPropertySpec> properties)
    {
        var structureVariables = GetStructuredVariables(properties);
        for (var i = 0; i < structureVariables.Count; i++)
        {
            var variable = structureVariables[i];
            var structureName = GetStructureLocalName(i);
            _ = sb.Append("        var ").Append(structureName)
                .Append(" = TwinCatRxApiExtensions.CreateStruct(client, \"")
                .Append(Escape(variable)).AppendLine("\")")
                .Append("            ?? throw new InvalidOperationException(\"The PLC structure '")
                .Append(Escape(variable)).AppendLine("' could not be created.\");")
                .Append("        _twinCatRxStructures[\"").Append(Escape(variable)).Append("\"] = ")
                .Append(structureName).AppendLine(";")
                .Append("        subscriptions.Add(").Append(structureName).AppendLine(");");

            foreach (var property in properties)
            {
                if (property.Kind != StructuredKind ||
                    !string.Equals(property.Address, variable, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(property.MemberAddress))
                {
                    continue;
                }

                _ = sb.Append("        subscriptions.Add(TwinCatRxObservableBridge.SubscribeTo(")
                    .Append("TwinCatRxHashTableExtensions.Observe<").Append(property.TypeName).Append(">(")
                    .Append(structureName).Append(", \"").Append(Escape(property.MemberAddress!))
                    .Append("\"), ").Append(property.SetterName).AppendLine("));");
            }
        }
    }

    /// <summary>Appends direct notification subscriptions.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The PLC property specifications.</param>
    private static void AppendDirectBindings(StringBuilder sb, IReadOnlyList<PlcPropertySpec> properties)
    {
        foreach (var property in properties)
        {
            if (property.Kind == WriteOnlyKind ||
                (property.Kind == StructuredKind && !string.IsNullOrWhiteSpace(property.MemberAddress)))
            {
                continue;
            }

            _ = sb.Append("        subscriptions.Add(TwinCatRxObservableBridge.SubscribeTo(")
                .Append("TwinCatRxApiExtensions.Observe<").Append(property.TypeName).Append(">(client, \"")
                .Append(Escape(property.Address)).Append('"');
            if (property.Id is not null)
            {
                _ = sb.Append(", \"").Append(Escape(property.Id)).Append('"');
            }

            _ = sb.Append(", static value => value is ").Append(property.TypeName)
                .Append(" typedValue ? typedValue : throw new InvalidCastException(\"")
                .Append(Escape(property.Address)).Append(" produced an incompatible value.\")), ")
                .Append(property.SetterName).AppendLine("));");
        }
    }

    /// <summary>Appends read methods for notification properties.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The PLC property specifications.</param>
    private static void AppendNotificationMethods(StringBuilder sb, IReadOnlyList<PlcPropertySpec> properties)
    {
        foreach (var property in properties)
        {
            if (property.Kind == WriteOnlyKind || property.Kind == StructuredKind)
            {
                continue;
            }

            _ = sb.Append("    public void ").Append(property.ReadMethodName).AppendLine("()")
                .AppendLine(ClassOpenBrace)
                .AppendLine("        var client = RequireTwinCatRxClient();")
                .Append("        client.Read(\"").Append(Escape(property.Address)).Append('"');

            if (property.ArraySize > 0)
            {
                _ = sb.Append(", arrayLength: ").Append(property.ArraySize.ToString(CultureInfo.InvariantCulture));
            }

            if (property.Id is not null)
            {
                _ = sb.Append(IdArgumentPrefix).Append(Escape(property.Id)).Append('"');
            }

            _ = sb.AppendLine(");")
                .AppendLine(ClassCloseBrace)
                .AppendLine();
        }
    }

    /// <summary>Appends write methods for write-capable properties.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The PLC property specifications.</param>
    private static void AppendWriteMethods(StringBuilder sb, IReadOnlyList<PlcPropertySpec> properties)
    {
        var writeProperties = GetWriteProperties(properties);
        var structuredVariables = GetStructuredVariables(properties);
        var structuredWriteProperties = GetStructuredWriteProperties(writeProperties, structuredVariables);
        AppendIndividualWriteMethods(sb, writeProperties);

        AppendRequiresUnreferencedCodeAttribute(sb);
        _ = sb.AppendLine("    public void WriteTwinCatRx(params (string Tag, object? Value)[] values)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        if (values == null)")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            throw new ArgumentNullException(nameof(values));")
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        var client = RequireTwinCatRxClient();")
            .Append("        var structuredWrites = new ")
            .Append(GeneratedDictionaryPrefix)
            .Append(GeneratedStructuredWriteListPrefix)
            .AppendLine("string WriteAddress, object Value, string? Id)>>(StringComparer.OrdinalIgnoreCase);")
            .AppendLine("        foreach (var value in values)")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            if (TryAddTwinCatRxStructuredWrite(value.Tag, value.Value, structuredWrites))")
            .AppendLine(NestedBlockOpenBrace)
            .AppendLine("                continue;")
            .AppendLine(NestedBlockCloseBrace)
            .AppendLine()
            .AppendLine("            WriteTwinCatRxValue(client, value.Tag, value.Value);")
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        WriteTwinCatRxStructures(client, structuredWrites);")
            .AppendLine(ClassCloseBrace)
            .AppendLine()
            .AppendLine("    private TwinCatRxClientContract RequireTwinCatRxClient() =>")
            .AppendLine("        _twinCatRxClient ?? throw new InvalidOperationException(" +
                "\"The generated TwinCATRx class is not bound to a PLC client.\");")
            .AppendLine()
            .AppendLine("    private TwinCatRxLogicalTags RequireTwinCatRxLogicalTags() =>")
            .AppendLine("        _twinCatRxLogicalTags ?? throw new InvalidOperationException(" +
                "\"The generated TwinCATRx class is not bound to a PLC client.\");")
            .AppendLine();

        AppendStructuredWriteCollector(sb, structuredWriteProperties);
        AppendStructuredWriteFlusher(sb);

        AppendDirectWriteDispatcher(sb, writeProperties, structuredVariables);

        AppendStructuredWriteHelper(sb);
    }

    /// <summary>Appends individual write methods that delegate to the generated batch writer.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="writeProperties">The write-capable properties.</param>
    private static void AppendIndividualWriteMethods(
        StringBuilder sb,
        IReadOnlyList<PlcPropertySpec> writeProperties)
    {
        foreach (var property in writeProperties)
        {
            AppendRequiresUnreferencedCodeAttribute(sb);
            _ = sb.Append("    public void ").Append(property.WriteMethodName).Append('(')
                .Append(property.TypeName).AppendLine(" value)")
                .AppendLine(ClassOpenBrace)
                .Append("        WriteTwinCatRx((nameof(").Append(property.PropertyName)
                .AppendLine("), value));")
                .AppendLine(ClassCloseBrace)
                .AppendLine();
        }
    }

    /// <summary>Appends the direct write dispatch helper.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="writeProperties">The write-capable properties.</param>
    /// <param name="structuredVariables">The known structure roots.</param>
    private static void AppendDirectWriteDispatcher(
        StringBuilder sb,
        IReadOnlyList<PlcPropertySpec> writeProperties,
        IReadOnlyList<string> structuredVariables)
    {
        AppendRequiresUnreferencedCodeAttribute(sb);
        _ = sb.AppendLine(
                "    private void WriteTwinCatRxValue(TwinCatRxClientContract client, string tag, object? value)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        var checkedValue = value ?? throw new ArgumentNullException(nameof(value), " +
                "\"TwinCATRx write values cannot be null.\");");

        foreach (var property in writeProperties)
        {
            if (GetStructuredWriteTarget(property, structuredVariables) is null)
            {
                AppendWriteBranch(sb, property);
            }
        }

        _ = sb.AppendLine(
                "        throw new ArgumentOutOfRangeException(nameof(tag), tag, " +
                "\"Unknown TwinCATRx generated write tag.\");")
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends one write dispatch branch.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="property">The write-capable property specification.</param>
    private static void AppendWriteBranch(StringBuilder sb, PlcPropertySpec property)
    {
        var writeAddress = GetWriteAddress(property);
        _ = sb.Append("        if (string.Equals(tag, nameof(").Append(property.PropertyName)
            .Append("), StringComparison.OrdinalIgnoreCase)")
            .Append(OrdinalTagComparisonPrefix).Append(Escape(writeAddress)).Append(OrdinalTagComparisonSuffix);

        if (property.Kind == StructuredKind && !string.IsNullOrWhiteSpace(property.MemberAddress))
        {
            _ = sb.Append(OrdinalTagComparisonPrefix).Append(Escape(property.MemberAddress!))
                .Append(OrdinalTagComparisonSuffix);
        }

        _ = sb.AppendLine(")")
            .AppendLine(BlockOpenBrace)
            .Append("            var typedValue = (").Append(property.TypeName).AppendLine(")checkedValue;");

        if (property.Kind == WriteOnlyKind)
        {
            _ = sb.Append("            ").Append(property.PropertyName).AppendLine(" = typedValue;");
        }

        _ = sb.Append("            client.Write(\"").Append(Escape(writeAddress)).Append("\", typedValue");
        if (property.Id is not null)
        {
            _ = sb.Append(", id: \"").Append(Escape(property.Id)).Append('"');
        }

        _ = sb.AppendLine(");")
            .AppendLine("            return;")
            .AppendLine(BlockCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends structured write collection logic.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="properties">The structured write properties.</param>
    private static void AppendStructuredWriteCollector(
        StringBuilder sb,
        IReadOnlyList<StructuredWritePropertySpec> properties)
    {
        AppendRequiresUnreferencedCodeAttribute(sb);
        _ = sb.Append("    private bool TryAddTwinCatRxStructuredWrite(string tag, object? value, ")
            .Append(GeneratedDictionaryPrefix)
            .Append(GeneratedStructuredWriteListPrefix)
            .AppendLine("string WriteAddress, object Value, string? Id)>> structuredWrites)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        var checkedValue = value ?? throw new ArgumentNullException(nameof(value), " +
                "\"TwinCATRx write values cannot be null.\");");

        foreach (var property in properties)
        {
            AppendStructuredWriteCollectorBranch(sb, property);
        }

        _ = sb.AppendLine("        return false;")
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends one structured write collection branch.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="structuredProperty">The structured write property.</param>
    private static void AppendStructuredWriteCollectorBranch(
        StringBuilder sb,
        StructuredWritePropertySpec structuredProperty)
    {
        var property = structuredProperty.Property;
        var structuredTarget = structuredProperty.Target;
        var writeAddress = GetWriteAddress(property);
        _ = sb.Append("        if (string.Equals(tag, nameof(").Append(property.PropertyName)
            .Append("), StringComparison.OrdinalIgnoreCase)")
            .Append(" || string.Equals(tag, \"").Append(Escape(writeAddress))
            .Append("\", StringComparison.OrdinalIgnoreCase)")
            .Append(" || string.Equals(tag, \"").Append(Escape(structuredTarget.MemberAddress))
            .Append("\", StringComparison.OrdinalIgnoreCase)")
            .AppendLine(")")
            .AppendLine(BlockOpenBrace)
            .Append("            var typedValue = (").Append(property.TypeName).AppendLine(")checkedValue;");

        if (property.Kind == WriteOnlyKind)
        {
            _ = sb.Append("            ").Append(property.PropertyName).AppendLine(" = typedValue;");
        }

        _ = sb.Append("            AddTwinCatRxStructuredWrite(structuredWrites, \"")
            .Append(Escape(structuredTarget.RootAddress)).Append("\", \"")
            .Append(Escape(structuredTarget.MemberAddress)).Append("\", \"")
            .Append(Escape(writeAddress)).Append("\", typedValue, ");
        AppendNullableStringLiteral(sb, property.Id);
        _ = sb.AppendLine(");")
            .AppendLine("            return true;")
            .AppendLine(BlockCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends structured write flushing logic.</summary>
    /// <param name="sb">The string builder.</param>
    private static void AppendStructuredWriteFlusher(StringBuilder sb)
    {
        AppendRequiresUnreferencedCodeAttribute(sb);
        _ = sb.Append("    private void WriteTwinCatRxStructures(TwinCatRxClientContract client, ")
            .Append(GeneratedDictionaryPrefix)
            .Append(GeneratedStructuredWriteListPrefix)
            .AppendLine("string WriteAddress, object Value, string? Id)>> structuredWrites)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        foreach (var structuredWrite in structuredWrites)")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            if (TryWriteTwinCatRxStructure(structuredWrite.Key, structuredWrite.Value))")
            .AppendLine(NestedBlockOpenBrace)
            .AppendLine("                continue;")
            .AppendLine(NestedBlockCloseBrace)
            .AppendLine()
            .AppendLine("            foreach (var value in structuredWrite.Value)")
            .AppendLine(NestedBlockOpenBrace)
            .AppendLine("                client.Write(value.WriteAddress, value.Value, id: value.Id);")
            .AppendLine(NestedBlockCloseBrace)
            .AppendLine(BlockCloseBrace)
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends the structured bulk write helper.</summary>
    /// <param name="sb">The string builder.</param>
    private static void AppendStructuredWriteHelper(StringBuilder sb)
    {
        AppendRequiresUnreferencedCodeAttribute(sb);
        _ = sb.Append("    private static void AddTwinCatRxStructuredWrite(")
            .Append(GeneratedDictionaryPrefix)
            .Append(GeneratedStructuredWriteListPrefix)
            .Append("string WriteAddress, object Value, string? Id)>> structuredWrites, string variable, ")
            .AppendLine("string memberAddress, string writeAddress, object value, string? id)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        if (!structuredWrites.TryGetValue(variable, out var values))")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            values = [];")
            .AppendLine("            structuredWrites[variable] = values;")
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        values.Add((memberAddress, writeAddress, value, id));")
            .AppendLine(ClassCloseBrace)
            .AppendLine()
            .AppendLine("    private bool TryWriteTwinCatRxStructure(string variable, " +
                "System.Collections.Generic.IReadOnlyList<(string MemberAddress, string WriteAddress, object Value, " +
                "string? Id)> values)")
            .AppendLine(ClassOpenBrace)
            .AppendLine("        if (!_twinCatRxStructures.TryGetValue(variable, out var structure))")
            .AppendLine(BlockOpenBrace)
            .AppendLine(IndentedReturnFalse)
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        try")
            .AppendLine(BlockOpenBrace)
            .AppendLine("        using var clone = TwinCatRxApiExtensions.CreateClone(structure);")
            .AppendLine("        for (var i = 0; i < values.Count; i++)")
            .AppendLine(BlockOpenBrace)
            .AppendLine("            var value = values[i];")
            .AppendLine("            TwinCatRxHashTableExtensions.Value(clone, value.MemberAddress, value.Value);")
            .AppendLine(BlockCloseBrace)
            .AppendLine("        var structuredValue = clone.Structure;")
            .AppendLine("        if (structuredValue is null)")
            .AppendLine(BlockOpenBrace)
            .AppendLine(IndentedReturnFalse)
            .AppendLine(BlockCloseBrace)
            .AppendLine()
            .AppendLine("        RequireTwinCatRxClient().Write(variable, structuredValue);")
            .AppendLine("        return true;")
            .AppendLine(BlockCloseBrace)
            .AppendLine("        catch (Exception)")
            .AppendLine(BlockOpenBrace)
            .AppendLine(IndentedReturnFalse)
            .AppendLine(BlockCloseBrace)
            .AppendLine(ClassCloseBrace)
            .AppendLine();
    }

    /// <summary>Appends the generated trimming annotation used by structured HashTableRx writes.</summary>
    /// <param name="sb">The string builder.</param>
    private static void AppendRequiresUnreferencedCodeAttribute(StringBuilder sb)
    {
        _ = sb.AppendLine(Net5OrGreaterDirective)
            .AppendLine(
                $"{RequiresUnreferencedCodePrefix}\"Structured writes use HashTableRx structure materialization.\")]")
            .AppendLine(EndIfDirective);
    }

    /// <summary>Appends a nullable string literal to generated source.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="value">The string value.</param>
    private static void AppendNullableStringLiteral(StringBuilder sb, string? value)
    {
        if (value is null)
        {
            _ = sb.Append("null");
            return;
        }

        _ = sb.Append('"').Append(Escape(value)).Append('"');
    }
}
