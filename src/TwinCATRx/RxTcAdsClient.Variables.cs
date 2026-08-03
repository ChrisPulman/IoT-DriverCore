// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if REACTIVE_SHIM
using IoT.Driver.TwinCATRx.Core.Reactive;
using CoreTwinCatRxExtensions = IoT.Driver.TwinCATRx.Core.Reactive.TwinCatRxExtensions;
using RxNotification = IoT.Driver.TwinCATRx.Core.Reactive.INotification;
#else
using IoT.Driver.TwinCATRx.Core;
using CoreTwinCatRxExtensions = IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions;
using RxNotification = IoT.Driver.TwinCATRx.Core.INotification;
#endif
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient
{
    /// <summary>Synchronizes generated data-type cleanup, creation, and loading across client instances.</summary>
    private static readonly object GeneratedDataTypeFileLock = new();

    /// <summary>Tracks variable prefixes whose stale files were cleaned in this process.</summary>
    private static readonly HashSet<string> CleanedGeneratedDataTypePrefixes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds the generated data type file prefix.</summary>
    /// <param name="variable">The PLC variable name.</param>
    /// <returns>The generated data type file prefix.</returns>
    private static string BuildDataTypesFileName(string variable)
    {
#if NET
        return variable.StartsWith('.')
            ? $"PLC_{variable.Remove(0, 1)}"
            : $"PLC_{variable}";
#else
        return variable.StartsWith(".", StringComparison.Ordinal)
            ? $"PLC_{variable.Remove(0, 1)}"
            : $"PLC_{variable}";
#endif
    }

    /// <summary>Builds the stable generated assembly path for a client instance.</summary>
    /// <param name="dataTypesBaseName">The generated data type file prefix.</param>
    /// <param name="generatedAssemblyIdentifier">The stable client assembly identifier.</param>
    /// <returns>The generated assembly path.</returns>
    private static string BuildDataTypesFilePath(
        string dataTypesBaseName,
        string generatedAssemblyIdentifier) =>
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"{dataTypesBaseName}{generatedAssemblyIdentifier}.dll");

    /// <summary>Resolves a previously generated PLC type assembly when it is available.</summary>
    /// <param name="dataTypesFileName">The generated assembly path.</param>
    /// <param name="generatedTypeName">The fully qualified generated type name.</param>
    /// <returns>The existing generated type, or <see langword="null"/> when it is unavailable.</returns>
    [RequiresUnreferencedCode("Loads a dynamically generated PLC type by name.")]
    [RequiresDynamicCode("Loads a dynamically generated PLC assembly.")]
    private static Type? GetExistingGeneratedType(string dataTypesFileName, string generatedTypeName) =>
        File.Exists(dataTypesFileName)
            ? CoreTwinCatRxExtensions.GetType(dataTypesFileName, generatedTypeName)
            : null;

    /// <summary>Claims the one stale-file cleanup pass for a generated data type prefix.</summary>
    /// <param name="dataTypesBaseName">The generated data type file prefix.</param>
    /// <returns><see langword="true"/> only for the first claim in this process.</returns>
    private static bool TryBeginGeneratedDataTypeCleanup(string dataTypesBaseName) =>
        CleanedGeneratedDataTypePrefixes.Add(dataTypesBaseName);

    /// <summary>Deletes stale generated data type files.</summary>
    /// <param name="dataTypesBaseName">The generated data type file prefix.</param>
    /// <param name="retainedFilePath">The current client's generated assembly path to retain.</param>
    private static void DeleteGeneratedDataTypeFiles(string dataTypesBaseName, string retainedFilePath)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        foreach (var file in DirectoryInfoExtensions.GetFilesWhere(
            directory,
            file => IsGeneratedDataTypeFile(file.Name, dataTypesBaseName)
                && !string.Equals(file.FullName, retainedFilePath, StringComparison.OrdinalIgnoreCase)))
        {
            _ = TryDeleteGeneratedDataTypeFile(file.FullName);
        }
    }

    /// <summary>Determines whether a DLL belongs to one exact generated data type prefix.</summary>
    /// <param name="fileName">The generated assembly file name.</param>
    /// <param name="dataTypesBaseName">The generated data type file prefix.</param>
    /// <returns><see langword="true"/> when the filename has a supported generated identifier.</returns>
    private static bool IsGeneratedDataTypeFile(string fileName, string dataTypesBaseName)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!nameWithoutExtension.StartsWith(dataTypesBaseName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var identifier = nameWithoutExtension.Substring(dataTypesBaseName.Length);
        return (identifier.Length == 32 && Guid.TryParseExact(identifier, "N", out _))
            || (identifier.Length == 18
                && long.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }

    /// <summary>Attempts to delete a generated type assembly that may still be loaded by the current process.</summary>
    /// <param name="filePath">The generated assembly path.</param>
    /// <returns><see langword="true"/> when the file was deleted; otherwise, <see langword="false"/>.</returns>
    private static bool TryDeleteGeneratedDataTypeFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
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
    private Exception? CreateNotificationVariables(
        IList<RxNotification>? notifications,
        IAdsClientRuntime client)
    {
        if (notifications is null)
        {
            return null;
        }

        var isTwinCat3 = client.Port >= TwinCat3Port;
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
    private void CreateNotificationVariable(
        RxNotification notification,
        IAdsClientRuntime client,
        bool isTwinCat3)
    {
        var notificationVariable = notification.Variable ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notificationVariable))
        {
            return;
        }

        var dataTypesBaseName = BuildDataTypesFileName(notificationVariable);
        var dataTypesFileName = BuildDataTypesFilePath(dataTypesBaseName, _generatedAssemblyIdentifier);
        Type? type;
        lock (GeneratedDataTypeFileLock)
        {
            if (TryBeginGeneratedDataTypeCleanup(dataTypesBaseName))
            {
                DeleteGeneratedDataTypeFiles(dataTypesBaseName, dataTypesFileName);
            }

            type = ResolveNotificationType(notificationVariable, dataTypesFileName, isTwinCat3);
        }

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
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <returns>The resolved CLR type.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private Type? ResolveNotificationType(
        string notificationVariable,
        string dataTypesFileName,
        bool isTwinCat3)
    {
        var nodeEmulator = _codeGenerator?.SearchSymbols(notificationVariable);
        var symbol = (ISymbol?)nodeEmulator?.Tag;
        var notificationType = symbol?.TypeName;
        var generatedTypeName = $"IoT.Driver.TwinCATRx.{notificationType}";
        var existingType = GetExistingGeneratedType(dataTypesFileName, generatedTypeName);
        if (existingType is not null)
        {
            return existingType;
        }

        if (_codeGenerator?.CreateDll(nodeEmulator, dataTypesFileName, isTwinCat3: isTwinCat3) == true)
        {
            var generatedSource = _codeGenerator.CreateCSharpCodeString(nodeEmulator, isTwinCat3: isTwinCat3);
            var generatedCode = $"{Path.GetFileName(dataTypesFileName)}${generatedSource}";
            _code.Add(generatedCode);
            var generatedType = CoreTwinCatRxExtensions.GetType(dataTypesFileName, generatedTypeName);
            if (generatedType is not null)
            {
                return generatedType;
            }
        }

        return TryResolvePlcType(notificationType, out var type) ? type : null;
    }

    /// <summary>Creates the write variables.</summary>
    /// <param name="writeVariables">The write variables.</param>
    /// <param name="client">The client.</param>
    /// <returns>A Value.</returns>
    [RequiresUnreferencedCode("May rely on dynamic type generation depending on PLC type definitions.")]
    [RequiresDynamicCode("May rely on dynamic type generation depending on PLC type definitions.")]
    private Exception? CreateWriteVariables(
        IList<IWriteVariable>? writeVariables,
        IAdsClientRuntime client)
    {
        if (writeVariables is null)
        {
            return null;
        }

        var isTC3 = client.Port >= TwinCat3Port;
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
    private void CreateWriteVariable(
        IWriteVariable writeVariable,
        IAdsClientRuntime client,
        bool isTwinCat3)
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
