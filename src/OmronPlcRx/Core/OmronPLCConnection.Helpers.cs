// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Channels;
using OmronPlcRx.Reactive.Core.Requests;
using OmronPlcRx.Reactive.Core.Responses;
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Core.Channels;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core;
#else
namespace OmronPlcRx.Core;
#endif

/// <summary>Contains Omron PLC connection validation and lifecycle helpers.</summary>
internal sealed partial class OmronPLCConnection
{
    /// <summary>Creates the configured transport channel.</summary>
    /// <param name="options">Transport options.</param>
    /// <returns>The configured channel.</returns>
    private static BaseChannel CreateChannel(OmronConnectionOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var remoteHost = OmronPLCConnectionMetadata.ValidateRemoteHost(options.RemoteHost);
        OmronPLCConnectionMetadata.ValidatePort(options.ConnectionMethod, options.Port);
        return options.ConnectionMethod switch
        {
            ConnectionMethod.Serial => new SerialHostLinkFinsChannel(
                options.SerialOptions ?? new OmronSerialOptions(remoteHost)),
            ConnectionMethod.UDP => new UDPChannel(remoteHost, options.Port),
            _ => new TCPChannel(remoteHost, options.Port),
        };
    }

    /// <summary>Gets a display name for a memory data type.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="dataType">The data type value.</param>
    /// <returns>The data type display name, or an empty string for an unnamed value.</returns>
    private static string GetDataTypeName<TEnum>(TEnum dataType)
        where TEnum : struct, Enum
    {
#if NET6_0_OR_GREATER
        return Enum.GetName(dataType) ?? string.Empty;
#else
        return Enum.GetName(typeof(TEnum), dataType) ?? string.Empty;
#endif
    }

    /// <summary>Validates arguments for a bit write.</summary>
    /// <param name="values">Bit values to write.</param>
    /// <param name="address">Starting word address.</param>
    /// <param name="startBitIndex">Starting bit index.</param>
    /// <param name="dataType">Bit memory area.</param>
    private void ValidateBitWriteArguments(
        bool[] values,
        ushort address,
        byte startBitIndex,
        MemoryBitDataType dataType)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        ThrowIfNotInitialized();
        if (startBitIndex > ProtocolConstants.Fifteen)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startBitIndex),
                "The Start Bit Index cannot be greater than 15");
        }

        if (values.Length == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                "The Values Array cannot be Empty");
        }

        if (startBitIndex + values.Length > ProtocolConstants.Sixteen)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                "The Values Array Length was greater than the Maximum Allowed of 16 Bits");
        }

        if (!ValidateBitDataType(dataType))
        {
            throw new ArgumentException(
                DataTypeMessagePrefix
                    + GetDataTypeName(dataType)
                    + DataTypeMessageSuffix,
                nameof(dataType));
        }

        if (ValidateBitAddress(address, dataType))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(address),
            $"{MaximumAddressMessagePrefix}'{GetDataTypeName(dataType)}{DataTypeNameSuffix}");
    }

    /// <summary>Releases resources used by the client and channel.</summary>
    /// <param name="disposing">True to dispose managed resources; otherwise, false.</param>
    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Channel.Dispose();
        lock (_isInitializedLock)
        {
            _isInitialized = false;
        }
    }

    /// <summary>Throws when the PLC connection has not been initialized.</summary>
    private void ThrowIfNotInitialized()
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException(PlcMustBeInitializedMessage);
            }
        }
    }

    /// <summary>Creates a channel failure message for the configured remote endpoint.</summary>
    /// <param name="messagePrefix">The failure message prefix.</param>
    /// <param name="messageSuffix">The optional failure message suffix.</param>
    /// <returns>The composed channel failure message.</returns>
    private string CreateChannelMessage(string messagePrefix, string messageSuffix = "")
    {
        var remoteEndpoint = $"{RemoteHost}:{Port}";
        return $"{messagePrefix} '{remoteEndpoint}'{messageSuffix}";
    }

    /// <summary>Validates a bit memory address.</summary>
    /// <param name="address">Address to validate.</param>
    /// <param name="dataType">Bit memory area.</param>
    /// <returns>True when the address is valid.</returns>
    private bool ValidateBitAddress(ushort address, MemoryBitDataType dataType) =>
        dataType switch
        {
            MemoryBitDataType.DataMemory => address
                < (PlcType == PlcType.NX1P2
                    ? ProtocolConstants.SixteenThousand
                    : ProtocolConstants.ThirtyTwoThousandSevenHundredSixtyEight),
            MemoryBitDataType.CommonIO =>
                address < ProtocolConstants.SixThousandOneHundredFortyFour,
            MemoryBitDataType.Work => address < ProtocolConstants.FiveHundredTwelve,
            MemoryBitDataType.Holding =>
                address < ProtocolConstants.OneThousandFiveHundredThirtySix,
            MemoryBitDataType.Auxiliary => address
                < (PlcType == PlcType.CJ2
                    ? ProtocolConstants.ElevenThousandFiveHundredThirtySix
                    : ProtocolConstants.NineHundredSixty),
            _ => false,
        };

    /// <summary>Validates a bit memory area.</summary>
    /// <param name="dataType">Bit memory area.</param>
    /// <returns>True when the memory area is supported.</returns>
    private bool ValidateBitDataType(MemoryBitDataType dataType) =>
        dataType switch
        {
            MemoryBitDataType.DataMemory => PlcType != PlcType.CP1,
            MemoryBitDataType.CommonIO
            or MemoryBitDataType.Work
            or MemoryBitDataType.Holding => true,
            MemoryBitDataType.Auxiliary => !IsNSeries,
            _ => false,
        };

    /// <summary>Validates a word memory range.</summary>
    /// <param name="startAddress">Starting address.</param>
    /// <param name="length">Range length.</param>
    /// <param name="dataType">Word memory area.</param>
    /// <returns>True when the range is valid.</returns>
    private bool ValidateWordStartAddress(
        ushort startAddress,
        int length,
        MemoryWordDataType dataType) =>
        dataType switch
        {
            MemoryWordDataType.DataMemory => startAddress + (length - 1)
                < (PlcType == PlcType.NX1P2
                    ? ProtocolConstants.SixteenThousand
                    : ProtocolConstants.ThirtyTwoThousandSevenHundredSixtyEight),
            MemoryWordDataType.CommonIO => startAddress + (length - 1)
                < ProtocolConstants.SixThousandOneHundredFortyFour,
            MemoryWordDataType.Work =>
                startAddress + (length - 1) < ProtocolConstants.FiveHundredTwelve,
            MemoryWordDataType.Holding => startAddress + (length - 1)
                < ProtocolConstants.OneThousandFiveHundredThirtySix,
            MemoryWordDataType.Auxiliary => startAddress + (length - 1)
                < (PlcType == PlcType.CJ2
                    ? ProtocolConstants.ElevenThousandFiveHundredThirtySix
                    : ProtocolConstants.NineHundredSixty),
            _ => false,
        };

    /// <summary>Validates a word memory area.</summary>
    /// <param name="dataType">Word memory area.</param>
    /// <returns>True when the memory area is supported.</returns>
    private bool ValidateWordDataType(MemoryWordDataType dataType) =>
        dataType switch
        {
            MemoryWordDataType.DataMemory
            or MemoryWordDataType.CommonIO
            or MemoryWordDataType.Work
            or MemoryWordDataType.Holding => true,
            MemoryWordDataType.Auxiliary => !IsNSeries,
            _ => false,
        };

    /// <summary>Requests controller identity and version information.</summary>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task RequestControllerInformationAsync(CancellationToken cancellationToken)
    {
        var request = ReadCPUUnitDataRequest.CreateNew(this);
        var requestResult = await Channel
            .ProcessRequestAsync(request, Timeout, Retries, cancellationToken)
            .ConfigureAwait(false);
        var result = ReadCPUUnitDataResponse.ExtractData(requestResult.Response);
        if (!string.IsNullOrEmpty(result.ControllerModel))
        {
            ControllerModel = result.ControllerModel;
            PlcType = OmronPLCConnectionMetadata.GetPLCType(ControllerModel);
        }

        if (!(result.ControllerVersion?.Length > 0))
        {
            return;
        }

        ControllerVersion = result.ControllerVersion;
    }
}
