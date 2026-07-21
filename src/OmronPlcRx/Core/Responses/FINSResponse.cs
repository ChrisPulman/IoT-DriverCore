// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Enums;
using OmronPlcRx.Reactive.Core.Requests;
#else
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Core.Requests;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Responses;
#else
namespace OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the f in sr es po ns e type.</summary>
internal sealed class FINSResponse
{
    /// <summary>Stores the h ea de rl en gt h value.</summary>
    internal const int HeaderLength = 10;

    /// <summary>Stores the c om ma nd le ng th value.</summary>
    internal const int CommandLength = 2;

    /// <summary>Stores the r es po ns ec od el en gt h value.</summary>
    internal const int ResponseCodeLength = 2;

    /// <summary>Stores specific response code messages.</summary>
    private static readonly Dictionary<int, string> ResponseCodeMessages = new()
    {
        [GetResponseCodeKey(0x00, 0x01)] = "Normal Completion (0x00) - Service was Canceled (0x01)",
        [GetResponseCodeKey(0x01, 0x01)] =
            "Local Node Error (0x01) - The Local Node was not found within the Network (0x01)",
        [GetResponseCodeKey(0x02, 0x01)] =
            "Destination Node Error (0x02) - The Destination Node was not found within the Network (0x01)",
        [GetResponseCodeKey(0x02, 0x02)] =
            "Destination Node Error (0x02) - The Destination Unit could not be found (0x02)",
        [GetResponseCodeKey(0x02, 0x04)] = "Destination Node Error (0x02) - The Destination Node was Busy (0x04)",
        [GetResponseCodeKey(0x02, 0x05)] = "Destination Node Error (0x02) - Response Timeout (0x05)",
        [GetResponseCodeKey(0x03, 0x01)] = "Controller Error (0x03) - Communications Controller Error (0x01)",
        [GetResponseCodeKey(0x03, 0x02)] = "Controller Error (0x03) - CPU Unit Error (0x02)",
        [GetResponseCodeKey(0x03, 0x03)] = "Controller Error (0x03) - Controller Board Error (0x03)",
        [GetResponseCodeKey(0x03, 0x04)] = "Controller Error (0x03) - Unit Number Error (0x04)",
        [GetResponseCodeKey(0x04, 0x01)] = "Service Unsupported Error (0x04) - Undefined Command (0x01)",
        [GetResponseCodeKey(0x04, 0x02)] =
            "Service Unsupported Error (0x04) - Command Not Supported by Model/Version (0x02)",
        [GetResponseCodeKey(0x05, 0x01)] = "Routing Table Error (0x05) - Destination Address Setting Error (0x01)",
        [GetResponseCodeKey(0x05, 0x02)] = "Routing Table Error (0x05) - No Routing Tables (0x02)",
        [GetResponseCodeKey(0x05, 0x03)] = "Routing Table Error (0x05) - Routing Table Error (0x03)",
        [GetResponseCodeKey(0x05, 0x04)] = "Routing Table Error (0x05) - Too Many Relays (0x04)",
        [GetResponseCodeKey(0x10, 0x01)] = "Command Format Error (0x10) - Command Data is too Long (0x01)",
        [GetResponseCodeKey(0x10, 0x02)] = "Command Format Error (0x10) - Command Data is too Short (0x02)",
        [GetResponseCodeKey(0x10, 0x03)] =
            "Command Format Error (0x10) - Elements Length and Values Length do not Match (0x03)",
        [GetResponseCodeKey(0x10, 0x04)] = "Command Format Error (0x10) - Command Format Error (0x04)",
        [GetResponseCodeKey(0x10, 0x05)] = "Command Format Error (0x10) - Header Error (0x05)",
        [GetResponseCodeKey(0x11, 0x01)] = "Parameter Error (0x11) - No Memory Area Specified (0x01)",
        [GetResponseCodeKey(0x11, 0x02)] = "Parameter Error (0x11) - Access Size Error (0x02)",
        [GetResponseCodeKey(0x11, 0x03)] = "Parameter Error (0x11) - Address Range Error (0x03)",
        [GetResponseCodeKey(0x11, 0x04)] = "Parameter Error (0x11) - Address Range Exceeded (0x04)",
        [GetResponseCodeKey(0x11, 0x06)] = "Parameter Error (0x11) - Program Missing (0x06)",
        [GetResponseCodeKey(0x11, 0x09)] = "Parameter Error (0x11) - Relational Error (0x09)",
        [GetResponseCodeKey(0x11, 0x0A)] = "Parameter Error (0x11) - Duplicate Data Access (0x0A)",
        [GetResponseCodeKey(0x11, 0x0B)] = "Parameter Error (0x11) - Response Data is too Long (0x0B)",
        [GetResponseCodeKey(0x11, 0x0C)] = "Parameter Error (0x11) - Parameter Error (0x0C)",
        [GetResponseCodeKey(0x20, 0x02)] = "Read not Possible Error (0x20) - The Program Area is Protected (0x02)",
        [GetResponseCodeKey(0x20, 0x03)] = "Read not Possible Error (0x20) - Table Missing (0x03)",
        [GetResponseCodeKey(0x20, 0x04)] = "Read not Possible Error (0x20) - Data Missing (0x04)",
        [GetResponseCodeKey(0x20, 0x05)] = "Read not Possible Error (0x20) - Program Missing (0x05)",
        [GetResponseCodeKey(0x20, 0x06)] = "Read not Possible Error (0x20) - File Missing (0x06)",
        [GetResponseCodeKey(0x20, 0x07)] = "Read not Possible Error (0x20) - Data Mismatch (0x07)",
        [GetResponseCodeKey(0x21, 0x01)] = "Write not Possible Error (0x21) - The Specified Area is Read-Only (0x01)",
        [GetResponseCodeKey(0x21, 0x02)] = "Write not Possible Error (0x21) - The Program Area is Protected (0x02)",
        [GetResponseCodeKey(0x21, 0x03)] = "Write not Possible Error (0x21) - Cannot Register (0x03)",
        [GetResponseCodeKey(0x21, 0x05)] = "Write not Possible Error (0x21) - Program Missing (0x05)",
        [GetResponseCodeKey(0x21, 0x06)] = "Write not Possible Error (0x21) - File Missing (0x06)",
        [GetResponseCodeKey(0x21, 0x07)] = "Write not Possible Error (0x21) - File Name already Exists (0x07)",
        [GetResponseCodeKey(0x21, 0x08)] = "Write not Possible Error (0x21) - Cannot Change (0x08)",
        [GetResponseCodeKey(0x22, 0x01)] =
            "Not Executable in Current Mode (0x22) - Not Possible during Execution (0x01)",
        [GetResponseCodeKey(0x22, 0x02)] = "Not Executable in Current Mode (0x22) - Not Possible while Running (0x02)",
        [GetResponseCodeKey(0x22, 0x03)] = "Not Executable in Current Mode (0x22) - PLC is in Program Mode (0x03)",
        [GetResponseCodeKey(0x22, 0x04)] = "Not Executable in Current Mode (0x22) - PLC is in Debug Mode (0x04)",
        [GetResponseCodeKey(0x22, 0x05)] = "Not Executable in Current Mode (0x22) - PLC is in Monitor Mode (0x05)",
        [GetResponseCodeKey(0x22, 0x06)] = "Not Executable in Current Mode (0x22) - PLC is in Run Mode (0x06)",
        [GetResponseCodeKey(0x22, 0x07)] =
            "Not Executable in Current Mode (0x22) - Specified Node is not a Polling Node (0x07)",
        [GetResponseCodeKey(0x22, 0x08)] = "Not Executable in Current Mode (0x22) - Step Cannot be Executed (0x08)",
        [GetResponseCodeKey(0x23, 0x01)] = "No Such Device (0x23) - File Device Missing (0x01)",
        [GetResponseCodeKey(0x23, 0x02)] = "No Such Device (0x23) - Memory Missing (0x02)",
        [GetResponseCodeKey(0x23, 0x03)] = "No Such Device (0x23) - Clock Missing (0x03)",
        [GetResponseCodeKey(0x24, 0x01)] = "Cannot Start/Stop (0x24) - Table Missing (0x01)",
    };

    /// <summary>Stores main response code message prefixes.</summary>
    private static readonly Dictionary<byte, string> MainResponseCodeMessages = new()
    {
        [0x01] = "Local Node Error",
        [0x02] = "Destination Node Error",
        [0x03] = "Controller Error",
        [0x04] = "Service Unsupported Error",
        [0x05] = "Routing Table Error",
        [0x10] = "Command Format Error",
        [0x11] = "Parameter Error",
        [0x20] = "Read not Possible Error",
        [0x21] = "Write not Possible Error",
        [0x22] = "Not Executable in Current Mode",
        [0x23] = "No Such Device",
        [0x24] = "Cannot Start/Stop",
    };

    /// <summary>Initializes a new instance of the <see cref="FINSResponse"/> class.</summary>
    private FINSResponse()
    {
    }

    /// <summary>Gets or sets the service id value.</summary>
    internal byte ServiceID { get; set; }

    /// <summary>Gets the function code value.</summary>
    internal byte FunctionCode { get; private set; }

    /// <summary>Gets or sets the sub function code value.</summary>
    internal byte SubFunctionCode { get; set; }

    /// <summary>Gets or sets the main response code value.</summary>
    internal byte MainResponseCode { get; set; }

    /// <summary>Gets or sets the sub response code value.</summary>
    internal byte SubResponseCode { get; set; }

    /// <summary>Gets the data value.</summary>
    internal byte[]? Data { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="message">The m es sa ge value.</param>
    /// <param name="request">The r eq ue st value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static FINSResponse CreateNew(Memory<byte> message, FINSRequest request)
    {
        if (message.Length < HeaderLength + CommandLength + ResponseCodeLength)
        {
            throw new FINSException("The FINS Response Message Length was too short");
        }

        var response = new FINSResponse();

        var header = message.Slice(0, HeaderLength).ToArray();

        response.ServiceID = header[9];

        var command = message.Slice(HeaderLength, CommandLength).ToArray();

        if (!ValidateFunctionCode(command[0]))
        {
            throw new FINSException($"Invalid Function Code '{command[0]}'");
        }

        response.FunctionCode = command[0];

        if (response.FunctionCode != request.FunctionCode)
        {
            var responseFunctionCode = Enum.GetName(typeof(FunctionCode), response.FunctionCode);
            var requestFunctionCode = Enum.GetName(typeof(FunctionCode), request.FunctionCode);
            throw new FINSException(
                $"Unexpected Function Code '{responseFunctionCode}' - Expecting '{requestFunctionCode}'");
        }

        if (!ValidateSubFunctionCode(command[0], command[1]))
        {
            throw new FINSException(
                $"Invalid Sub Function Code '{command[1]}' for Function Code '{command[0]}'");
        }

        response.SubFunctionCode = command[1];

        if (response.SubFunctionCode != request.SubFunctionCode)
        {
            var responseSubFunctionCode = GetSubFunctionCodeName(response.FunctionCode, response.SubFunctionCode);
            var requestSubFunctionCode = GetSubFunctionCodeName(request.FunctionCode, request.SubFunctionCode);
            throw new FINSException(
                $"Unexpected Sub Function Code '{responseSubFunctionCode}' - Expecting '{requestSubFunctionCode}'");
        }

        var responseCode = message.Slice(HeaderLength + CommandLength, ResponseCodeLength).ToArray();

        if (HasNetworkRelayError(responseCode[0]))
        {
            throw new FINSException("A Network Relay Error has occurred");
        }

        response.MainResponseCode = GetMainResponseCode(responseCode[0]);

        response.SubResponseCode = GetSubResponseCode(responseCode[1]);

        ThrowIfResponseError(response.MainResponseCode, response.SubResponseCode);

        ValidateServiceId(request.ServiceID, response.ServiceID);

        response.Data =
            message.Length > HeaderLength + CommandLength + ResponseCodeLength
                ? message.Slice(HeaderLength + CommandLength + ResponseCodeLength).ToArray()
                : Array.Empty<byte>();

        return response;
    }

    /// <summary>Represents the v al id at ef un ct io nc od e enumeration.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static bool ValidateFunctionCode(byte functionCode) =>
        Enum.IsDefined(typeof(FunctionCode), (int)functionCode);

    /// <summary>Initializes a new instance of the <see cref="ValidateSubFunctionCode"/> class.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <param name="subFunctionCode">The s ub fu nc ti on co de value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static bool ValidateSubFunctionCode(byte functionCode, byte subFunctionCode) =>
        (FunctionCode)functionCode switch
    {
        Enums.FunctionCode.AccessRights => Enum.IsDefined(typeof(AccessRightsFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.Debugging => Enum.IsDefined(typeof(DebuggingFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ErrorLog =>
            Enum.IsDefined(typeof(ErrorLogFunctionCode), (int)subFunctionCode)
            || Enum.IsDefined(typeof(FinsWriteLogFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.FileMemory => Enum.IsDefined(typeof(FileMemoryFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MachineConfiguration =>
            Enum.IsDefined(typeof(MachineConfigurationFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MemoryArea => Enum.IsDefined(typeof(MemoryAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MessageDisplay => Enum.IsDefined(typeof(MessageDisplayFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.OperatingMode => Enum.IsDefined(typeof(OperatingModeFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ParameterArea => Enum.IsDefined(typeof(ParameterAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ProgramArea => Enum.IsDefined(typeof(ProgramAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.SerialGateway => Enum.IsDefined(typeof(SerialGatewayFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.Status => Enum.IsDefined(typeof(StatusFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.TimeData => Enum.IsDefined(typeof(TimeDataFunctionCode), (int)subFunctionCode),
        _ => false,
    };

    /// <summary>Validates that the response belongs to the request.</summary>
    /// <param name="requestServiceId">Request service identifier.</param>
    /// <param name="responseServiceId">Response service identifier.</param>
    private static void ValidateServiceId(byte requestServiceId, byte responseServiceId)
    {
        if (requestServiceId == responseServiceId)
        {
            return;
        }

        const string prefix = "The Service ID for the FINS Request '";
        throw new FINSException(
            $"{prefix}{requestServiceId}' did not match the FINS Response '{responseServiceId}'");
    }

    /// <summary>Initializes a new instance of the <see cref="GetSubFunctionCodeName"/> class.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <param name="subFunctionCode">The s ub fu nc ti on co de value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static string? GetSubFunctionCodeName(byte functionCode, byte subFunctionCode) =>
        (FunctionCode)functionCode switch
    {
        Enums.FunctionCode.AccessRights => Enum.GetName(typeof(AccessRightsFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.Debugging => Enum.GetName(typeof(DebuggingFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ErrorLog =>
            Enum.IsDefined(typeof(ErrorLogFunctionCode), (int)subFunctionCode)
                ? Enum.GetName(typeof(ErrorLogFunctionCode), (int)subFunctionCode)
                : Enum.GetName(typeof(FinsWriteLogFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.FileMemory => Enum.GetName(typeof(FileMemoryFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MachineConfiguration =>
            Enum.GetName(typeof(MachineConfigurationFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MemoryArea => Enum.GetName(typeof(MemoryAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.MessageDisplay => Enum.GetName(typeof(MessageDisplayFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.OperatingMode => Enum.GetName(typeof(OperatingModeFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ParameterArea => Enum.GetName(typeof(ParameterAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.ProgramArea => Enum.GetName(typeof(ProgramAreaFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.SerialGateway => Enum.GetName(typeof(SerialGatewayFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.Status => Enum.GetName(typeof(StatusFunctionCode), (int)subFunctionCode),
        Enums.FunctionCode.TimeData => Enum.GetName(typeof(TimeDataFunctionCode), (int)subFunctionCode),
        _ => "Unknown",
    };

    /// <summary>Initializes a new instance of the <see cref="HasNetworkRelayError"/> class.</summary>
    /// <param name="responseCode">The r es po ns ec od e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool HasNetworkRelayError(byte responseCode) => (responseCode & (1 << 7)) != 0;

    /// <summary>Initializes a new instance of the <see cref="GetMainResponseCode"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte GetMainResponseCode(byte value)
    {
        const byte includedBits = 0x7F;

        return (byte)(value & includedBits);
    }

    /// <summary>Initializes a new instance of the <see cref="GetSubResponseCode"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte GetSubResponseCode(byte value)
    {
        const byte includedBits = 0x3F;

        return (byte)(value & includedBits);
    }

    /// <summary>Initializes a new instance of the <see cref="ThrowIfResponseError"/> class.</summary>
    /// <param name="mainCode">The m ai nc od e value.</param>
    /// <param name="subCode">The s ub co de value.</param>
    private static void ThrowIfResponseError(byte mainCode, byte subCode)
    {
        if (!TryGetResponseErrorMessage(mainCode, subCode, out var message))
        {
            return;
        }

        throw new FINSException(message);
    }

    /// <summary>Gets a response code dictionary key.</summary>
    /// <param name="mainCode">The main response code.</param>
    /// <param name="subCode">The sub response code.</param>
    /// <returns>The response code key.</returns>
    private static int GetResponseCodeKey(byte mainCode, byte subCode) => (mainCode << 8) | subCode;

    /// <summary>Attempts to get a response error message.</summary>
    /// <param name="mainCode">The main response code.</param>
    /// <param name="subCode">The sub response code.</param>
    /// <param name="message">The response error message.</param>
    /// <returns>A value indicating whether the response is an error.</returns>
    private static bool TryGetResponseErrorMessage(byte mainCode, byte subCode, out string message)
    {
        message = string.Empty;
        if (mainCode == 0 && subCode == 0)
        {
            return false;
        }

        if (ResponseCodeMessages.TryGetValue(GetResponseCodeKey(mainCode, subCode), out var responseMessage))
        {
            message = responseMessage ?? string.Empty;
            return true;
        }

        if (mainCode == 0)
        {
            message = string.Empty;
            return false;
        }

        message = GetDefaultResponseErrorMessage(mainCode, subCode);
        return true;
    }

    /// <summary>Gets the default response error message.</summary>
    /// <param name="mainCode">The main response code.</param>
    /// <param name="subCode">The sub response code.</param>
    /// <returns>The default response error message.</returns>
    private static string GetDefaultResponseErrorMessage(byte mainCode, byte subCode) =>
        MainResponseCodeMessages.TryGetValue(mainCode, out var mainMessage)
            ? $"{mainMessage} (0x{mainCode:X2}) - Sub Response Code (0x{subCode:X2})"
            : $"Unknown Error - Main Response Code (0x{mainCode:X2}) - Sub Response Code (0x{subCode:X2})";
}
