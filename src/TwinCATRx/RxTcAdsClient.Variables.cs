// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if REACTIVE_SHIM
using CP.TwinCatRx.Core.Reactive;
using CoreTwinCatRxExtensions = CP.TwinCatRx.Core.Reactive.TwinCatRxExtensions;
using RxNotification = CP.TwinCatRx.Core.Reactive.INotification;
#else
using CP.TwinCatRx.Core;
using CoreTwinCatRxExtensions = CP.TwinCatRx.Core.TwinCatRxExtensions;
using RxNotification = CP.TwinCatRx.Core.INotification;
#endif
using TwinCAT.Ads;
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient
{
    /// <summary>Builds the generated data type file prefix.</summary>
    /// <param name="variable">The PLC variable name.</param>
    /// <returns>The generated data type file prefix.</returns>
    private static string BuildDataTypesFileName(string variable) =>
        variable.StartsWith(".")
            ? $"PLC_{variable.Remove(0, 1)}"
            : $"PLC_{variable}";

    /// <summary>Deletes stale generated data type files.</summary>
    /// <param name="dataTypesBaseName">The generated data type file prefix.</param>
    private static void DeleteGeneratedDataTypeFiles(string dataTypesBaseName)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        foreach (var file in DirectoryInfoExtensions.GetFilesWhere(
            directory,
            file => file.Name.Contains(dataTypesBaseName)))
        {
            File.Delete(file.FullName);
        }
    }

    /// <summary>Tries to resolve a primitive PLC type to a CLR type.</summary>
    /// <param name="plcType">The PLC type name.</param>
    /// <param name="type">The resolved CLR type.</param>
    /// <returns><c>true</c> when the PLC type was resolved.</returns>
    [RequiresUnreferencedCode("Uses type name lookup for PLC primitive mappings.")]
    private static bool TryResolvePlcType(string? plcType, out Type? type)
    {
        type = null;
        try
        {
            var types = CodeGenerator.PLCToCSharpTypeConverter(plcType).Split(',');
            type = Type.GetType(types[0]);
            return type is not null;
        }
        catch (UnsuportedTypeException)
        {
            return false;
        }
    }

    /// <summary>Finds the configured notification array length for a variable.</summary>
    /// <param name="variable">The PLC variable.</param>
    /// <returns>The configured array length, or <c>-1</c> when none is configured.</returns>
    private int FindNotificationArrayLength(string variable)
    {
        var notifications = Settings?.Notifications;
        if (notifications is null)
        {
            return -1;
        }

        for (var i = 0; i < notifications.Count; i++)
        {
            var notification = notifications[i];
            if (string.Equals(notification.Variable, variable, StringComparison.OrdinalIgnoreCase))
            {
                return notification.ArraySize;
            }
        }

        return -1;
    }

    /// <summary>Resolves a read handle, type, and array length for a PLC variable.</summary>
    /// <param name="variable">The PLC variable.</param>
    /// <param name="arrayLength">The requested array length.</param>
    /// <param name="handle">The resolved ADS handle.</param>
    /// <param name="type">The resolved value type.</param>
    /// <param name="readLength">The resolved read length.</param>
    /// <returns><c>true</c> when a read target was resolved.</returns>
    private bool TryGetReadTarget(
        string variable,
        int? arrayLength,
        out uint? handle,
        out Type? type,
        out int readLength)
    {
        handle = null;
        type = null;
        readLength = -1;
        if (string.IsNullOrWhiteSpace(variable) || !_typeInfo.TryGetValue(variable, out type))
        {
            return false;
        }

        if (!TryGetReadHandle(variable, out handle, out readLength))
        {
            return false;
        }

        if (!type.IsArray && type != typeof(string))
        {
            return true;
        }

        if (readLength > 0)
        {
            return true;
        }

        if (arrayLength.HasValue)
        {
            readLength = arrayLength.Value;
            return true;
        }

        throw new ArgumentOutOfRangeException(nameof(arrayLength), "arrayLength must be set to the size of the Array");
    }

    /// <summary>Resolves a read handle for a PLC variable.</summary>
    /// <param name="variable">The PLC variable.</param>
    /// <param name="handle">The resolved ADS handle.</param>
    /// <param name="arrayLength">The registered array length.</param>
    /// <returns><c>true</c> when a handle was resolved.</returns>
    private bool TryGetReadHandle(string variable, out uint? handle, out int arrayLength)
    {
        if (ReadWriteHandleInfo.TryGetValue(variable, out handle))
        {
            arrayLength = FindNotificationArrayLength(variable);
            return true;
        }

        if (WriteHandleInfo.TryGetValue(variable, out var writeHandle))
        {
            handle = writeHandle.Handle;
            arrayLength = writeHandle.ArrayLength;
            return true;
        }

        handle = null;
        arrayLength = -1;
        return false;
    }

    /// <summary>Creates the notification variables.</summary>
    /// <param name="notifications">The notifications.</param>
    /// <param name="client">The client.</param>
    /// <returns>A Value.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private Exception? CreateNotificationVariables(IList<RxNotification>? notifications, AdsClient client)
    {
        if (notifications is null)
        {
            return null;
        }

        var isTwinCat3 = client.Address?.Port >= TwinCat3Port;
        for (var i = 0; i < notifications.Count; i++)
        {
            var notification = notifications[i];
            if (i == 0 && string.IsNullOrEmpty(notification.Variable))
            {
                continue;
            }

            try
            {
                CreateNotificationVariable(notification, client, isTwinCat3);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        return null;
    }

    /// <summary>Creates a notification variable registration.</summary>
    /// <param name="notification">The notification to register.</param>
    /// <param name="client">The ADS client.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private void CreateNotificationVariable(RxNotification notification, AdsClient client, bool isTwinCat3)
    {
        var notificationVariable = notification.Variable ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notificationVariable))
        {
            return;
        }

        var identifier = _timeProvider.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture);
        var dataTypesBaseName = BuildDataTypesFileName(notificationVariable);
        DeleteGeneratedDataTypeFiles(dataTypesBaseName);

        var dataTypesFileName = $"{dataTypesBaseName}{identifier}.dll";
        var type = ResolveNotificationType(notificationVariable, dataTypesFileName, identifier, isTwinCat3);
        if (type is null)
        {
            return;
        }

        var handle = client.CreateVariableHandle(notificationVariable);
        ReadWriteHandleInfo[notificationVariable] = handle;
        _readWriteVariablesByHandle[handle] = notificationVariable;
        _typeInfo[notificationVariable] = type;
    }

    /// <summary>Resolves the CLR type used by a notification variable.</summary>
    /// <param name="notificationVariable">The notification variable name.</param>
    /// <param name="dataTypesFileName">The generated data type file name.</param>
    /// <param name="identifier">The generated code identifier.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <returns>The resolved CLR type.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private Type? ResolveNotificationType(
        string notificationVariable,
        string dataTypesFileName,
        string identifier,
        bool isTwinCat3)
    {
        var nodeEmulator = _codeGenerator?.SearchSymbols(notificationVariable);
        var symbol = (ISymbol?)nodeEmulator?.Tag;
        var notificationType = symbol?.TypeName;
        if (_codeGenerator?.CreateDll(nodeEmulator, dataTypesFileName, isTwinCat3: isTwinCat3) == true)
        {
            var generatedCode = BuildDataTypesFileName(notificationVariable);
            var generatedSource = _codeGenerator.CreateCSharpCodeString(nodeEmulator, isTwinCat3: isTwinCat3);
            generatedCode += $"{identifier}.dll${generatedSource}";
            _code.Add(generatedCode);
            return CoreTwinCatRxExtensions.GetType(dataTypesFileName, $"TwinCATRx.{notificationType}");
        }

        return TryResolvePlcType(notificationType, out var type) ? type : null;
    }

    /// <summary>Creates the write variables.</summary>
    /// <param name="writeVariables">The write variables.</param>
    /// <param name="client">The client.</param>
    /// <returns>A Value.</returns>
    [RequiresUnreferencedCode("May rely on dynamic type generation depending on PLC type definitions.")]
    [RequiresDynamicCode("May rely on dynamic type generation depending on PLC type definitions.")]
    private Exception? CreateWriteVariables(IList<IWriteVariable>? writeVariables, AdsClient client)
    {
        if (writeVariables is null)
        {
            return null;
        }

        var isTC3 = client.Address?.Port >= TwinCat3Port;
        foreach (var writeVariable in writeVariables)
        {
            try
            {
                CreateWriteVariable(writeVariable, client, isTC3);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        return null;
    }

    /// <summary>Creates a write variable registration.</summary>
    /// <param name="writeVariable">The write variable.</param>
    /// <param name="client">The ADS client.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    [RequiresUnreferencedCode("May rely on dynamic type generation depending on PLC type definitions.")]
    [RequiresDynamicCode("May rely on dynamic type generation depending on PLC type definitions.")]
    private void CreateWriteVariable(IWriteVariable writeVariable, AdsClient client, bool isTwinCat3)
    {
        var variable = writeVariable.Variable ?? string.Empty;
        if (string.IsNullOrEmpty(variable))
        {
            return;
        }

        var handle = client.CreateVariableHandle(variable);
        WriteHandleInfo[variable] = (handle, writeVariable.ArraySize);
        _writeVariablesByHandle[handle] = variable;

        var nodeEmulator = _codeGenerator?.SearchSymbols(variable);
        if (nodeEmulator is null)
        {
            return;
        }

        var symbol = (ISymbol?)nodeEmulator.Tag;
        var notificationType = symbol?.TypeName;
        if (TryResolvePlcType(notificationType, out var type) && type is not null)
        {
            _typeInfo[variable] = type;
            return;
        }

        var generatedCode = BuildDataTypesFileName(variable);
        generatedCode += $".dll${_codeGenerator?.CreateCSharpCodeString(nodeEmulator, isTwinCat3: isTwinCat3)}";
        _code.Add(generatedCode);
    }
}
