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
using OmronPlcRx.Reactive.Results;
#else
using OmronPlcRx.Core.Channels;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core;
#else
namespace OmronPlcRx.Core;
#endif

/// <summary>Provides Omron FINS operations over supported transports.</summary>
internal sealed partial class OmronPLCConnection : IDisposable
{
    /// <summary>Stores the data type message prefix.</summary>
    private const string DataTypeMessagePrefix = "The Data Type '";

    /// <summary>Stores the data type message suffix.</summary>
    private const string DataTypeMessageSuffix = "' is not Supported on this PLC";

    /// <summary>Stores the data type name suffix.</summary>
    private const string DataTypeNameSuffix = "' Data Type";

    /// <summary>Stores the maximum address message prefix.</summary>
    private const string MaximumAddressMessagePrefix = "The Address is greater than the Maximum Address for the ";

    /// <summary>Stores the maximum start address message prefix.</summary>
    private const string MaximumStartAddressMessagePrefix =
        "The Start Address and Length combined are greater than the Maximum Address for the ";

    /// <summary>Stores the maximum write start address message prefix.</summary>
    private const string MaximumWriteStartAddressMessagePrefix =
        "The Start Address and Values Array Length combined are greater than the Maximum Address for the ";

    /// <summary>Stores the channel initialization failure message prefix.</summary>
    private const string ChannelInitializationFailureMessagePrefix =
        "Failed to Create the Ethernet UDP Communication Channel for Omron PLC";

    /// <summary>Stores the channel timeout failure message prefix.</summary>
    private const string ChannelTimeoutFailureMessagePrefix =
        "Failed to Create the Ethernet UDP Communication Channel within the Timeout Period for Omron PLC";

    /// <summary>Stores the channel disposed message suffix.</summary>
    private const string ChannelDisposedMessageSuffix = " - The underlying Socket Connection has been Closed";

    /// <summary>Stores the PLC must be initialized message.</summary>
    private const string PlcMustBeInitializedMessage =
        "This Omron PLC must be Initialized first before any Requests can be Processed";

#if NET9_0_OR_GREATER
    /// <summary>Executes the i si ni ti al iz ed lo ck operation.</summary>
    private readonly Lock _isInitializedLock = new();
#else
    /// <summary>Executes the i si ni ti al iz ed lo ck operation.</summary>
    private readonly object _isInitializedLock = new();
#endif

    /// <summary>Stores the i si ni ti al iz ed value.</summary>
    private bool _isInitialized;

    /// <summary>Initializes a new instance of the <see cref="OmronPLCConnection"/> class.</summary>
    /// <param name="options">Transport and request options.</param>
    internal OmronPLCConnection(OmronConnectionOptions options)
        : this(
            options,
            CreateChannel(options),
            PlcType.Unknown,
            null,
            null,
            false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPLCConnection"/> class.</summary>
    /// <param name="options">Transport and request options.</param>
    /// <param name="channel">The injected channel.</param>
    /// <param name="plcType">The PLC type to use for validation.</param>
    /// <param name="controllerModel">The controller model.</param>
    /// <param name="controllerVersion">The controller version.</param>
    /// <param name="isInitialized">A value indicating whether the connection starts initialized.</param>
    internal OmronPLCConnection(
        OmronConnectionOptions options,
        BaseChannel channel,
        PlcType plcType,
        string? controllerModel,
        string? controllerVersion,
        bool isInitialized)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        OmronPLCConnectionMetadata.ValidateNodeIdentifiers(
            options.LocalNodeId,
            options.RemoteNodeId,
            options.ConnectionMethod);
        LocalNodeID = options.LocalNodeId;
        RemoteNodeID = options.RemoteNodeId;
        ConnectionMethod = options.ConnectionMethod;
        RemoteHost = OmronPLCConnectionMetadata.ValidateRemoteHost(options.RemoteHost);
        OmronPLCConnectionMetadata.ValidatePort(options.ConnectionMethod, options.Port);
        Port = options.ConnectionMethod == ConnectionMethod.Serial ? 0 : options.Port;

        if (options.Timeout <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The Timeout Value cannot be less than 1");
        }

        Timeout = options.Timeout;

        if (options.Retries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The Retries Value cannot be Negative");
        }

        Retries = options.Retries;
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        PlcType = plcType;
        ControllerModel = controllerModel;
        ControllerVersion = controllerVersion;
        _isInitialized = isInitialized;
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets the local node id value.</summary>
    internal byte LocalNodeID { get; }

    /// <summary>Gets the remote node id value.</summary>
    internal byte RemoteNodeID { get; }

    /// <summary>Gets the connection method value.</summary>
    internal ConnectionMethod ConnectionMethod { get; }

    /// <summary>Gets the remote host value.</summary>
    internal string RemoteHost { get; }

    /// <summary>Gets the port value.</summary>
    internal int Port { get; } = 9600;

    /// <summary>Gets or sets the timeout value.</summary>
    internal int Timeout { get; set; }

    /// <summary>Gets or sets the retries value.</summary>
    internal int Retries { get; set; }

    /// <summary>Gets the plc type value.</summary>
    internal PlcType PlcType { get; private set; } = PlcType.Unknown;

    /// <summary>Gets the is initialized value.</summary>
    internal bool IsInitialized
    {
        get
        {
            lock (_isInitializedLock)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>Gets the controller model value.</summary>
    internal string? ControllerModel { get; private set; }

    /// <summary>Gets the controller version value.</summary>
    internal string? ControllerVersion { get; private set; }

    /// <summary>Gets the maximum words per read for the detected PLC type.</summary>
    internal ushort MaximumReadWordLength => PlcType == PlcType.CP1
        ? ProtocolConstants.FourHundredNinetyNineUShort
        : ProtocolConstants.NineHundredNinetyNineUShort;

    /// <summary>Gets the maximum words per write for the detected PLC type.</summary>
    internal ushort MaximumWriteWordLength => PlcType == PlcType.CP1
        ? ProtocolConstants.FourHundredNinetySixUShort
        : ProtocolConstants.NineHundredNinetySixUShort;

    /// <summary>Gets the channel value.</summary>
    internal BaseChannel Channel { get; }

    /// <summary>Gets the is n series value.</summary>
    internal bool IsNSeries => PlcType switch
    {
        PlcType.NJ101
        or PlcType.NJ301
        or PlcType.NJ501
        or PlcType.NX1P2
        or PlcType.NX102
        or PlcType.NX701
        or PlcType.NY512
        or PlcType.NY532
        or PlcType.NJ_NX_NY_Series => true,
        _ => false,
    };

    /// <summary>Gets the is c series value.</summary>
    internal bool IsCSeries => PlcType switch
    {
        PlcType.CP1 or PlcType.CJ2 or PlcType.C_Series => true,
        _ => false,
    };

    /// <summary>Initializes the communication channel and queries controller information.</summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        // Initialize the Channel
        try
        {
            await Channel.InitializeAsync(Timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException(
                CreateChannelMessage(
                    ChannelInitializationFailureMessagePrefix,
                    ChannelDisposedMessageSuffix));
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException(CreateChannelMessage(ChannelTimeoutFailureMessagePrefix));
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException(
                CreateChannelMessage(ChannelInitializationFailureMessagePrefix),
                e);
        }

        await RequestControllerInformationAsync(cancellationToken);

        lock (_isInitializedLock)
        {
            _isInitialized = true;
        }
    }

    /// <summary>Releases the connection and its transport resources.</summary>
    internal void Dispose()
    {
        Dispose(true);
    }

    /// <summary>Read a single bit value.</summary>
    /// <param name="address">The word address containing the target bit.</param>
    /// <param name="bitIndex">The bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read bit result.</returns>
    internal Task<ReadBitsResult> ReadBitAsync(
        ushort address,
        byte bitIndex,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken) =>
        ReadBitsAsync(address, bitIndex, 1, dataType, cancellationToken);

    /// <summary>Read a sequence of bit values.</summary>
    /// <param name="address">The word address containing the first bit.</param>
    /// <param name="startBitIndex">The starting bit index within the word (0-15).</param>
    /// <param name="length">Number of bits to read (1-16, not crossing word boundary).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read bits result including values and transmission metrics.</returns>
    internal async Task<ReadBitsResult> ReadBitsAsync(
        ushort address,
        byte startBitIndex,
        byte length,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        if (startBitIndex > ProtocolConstants.Fifteen)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startBitIndex),
                "The Start Bit Index cannot be greater than 15");
        }

        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
        }

        if (startBitIndex + length > ProtocolConstants.Sixteen)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "The Start Bit Index and Length combined are greater than the Maximum Allowed of 16 Bits");
        }

        if (!ValidateBitDataType(dataType))
        {
            throw new ArgumentException(
                DataTypeMessagePrefix
                    + GetDataTypeName(dataType)
                    + DataTypeMessageSuffix,
                nameof(dataType));
        }

        if (!ValidateBitAddress(address, dataType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                $"{MaximumAddressMessagePrefix}'{GetDataTypeName(dataType)}{DataTypeNameSuffix}");
        }

        var request = ReadMemoryAreaBitRequest.CreateNew(this, address, startBitIndex, length, dataType);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        return new ReadBitsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Values = ReadMemoryAreaBitResponse.ExtractValues(request, requestResult.Response),
        };
    }

    /// <summary>Read a single word value.</summary>
    /// <param name="address">The starting address to read.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read words result including values and transmission metrics.</returns>
    internal Task<ReadWordsResult> ReadWordAsync(
        ushort address,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken) =>
        ReadWordsAsync(address, 1, dataType, cancellationToken);

    /// <summary>Read a sequence of word values.</summary>
    /// <param name="startAddress">The starting address to read.</param>
    /// <param name="length">Number of words to read.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read words result including values and transmission metrics.</returns>
    internal async Task<ReadWordsResult> ReadWordsAsync(
        ushort startAddress,
        ushort length,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
        }

        if (length > MaximumReadWordLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"The Length cannot be greater than {MaximumReadWordLength}");
        }

        if (!ValidateWordDataType(dataType))
        {
            throw new ArgumentException(
                DataTypeMessagePrefix
                    + GetDataTypeName(dataType)
                    + DataTypeMessageSuffix,
                nameof(dataType));
        }

        if (!ValidateWordStartAddress(startAddress, length, dataType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startAddress),
                $"{MaximumStartAddressMessagePrefix}'{GetDataTypeName(dataType)}{DataTypeNameSuffix}");
        }

        var request = ReadMemoryAreaWordRequest.CreateNew(this, startAddress, length, dataType);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        return new ReadWordsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Values = ReadMemoryAreaWordResponse.ExtractValues(request, requestResult.Response),
        };
    }

    /// <summary>Write a single bit value.</summary>
    /// <param name="value">The bit value to write.</param>
    /// <param name="address">The word address containing the target bit.</param>
    /// <param name="bitIndex">The bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write bits result containing transmission metrics.</returns>
    internal Task<WriteBitsResult> WriteBitAsync(
        bool value,
        ushort address,
        byte bitIndex,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken) =>
        WriteBitsAsync([value], address, bitIndex, dataType, cancellationToken);

    /// <summary>Write a sequence of bit values.</summary>
    /// <param name="values">The bit values to write.</param>
    /// <param name="address">The word address containing the first bit.</param>
    /// <param name="startBitIndex">The starting bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write bits result containing transmission metrics.</returns>
    internal async Task<WriteBitsResult> WriteBitsAsync(
        bool[] values,
        ushort address,
        byte startBitIndex,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken)
    {
        ValidateBitWriteArguments(values, address, startBitIndex, dataType);

        var request = WriteMemoryAreaBitRequest.CreateNew(this, address, startBitIndex, dataType, values);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteMemoryAreaBitResponse.Validate(request, requestResult.Response);

        return new WriteBitsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>Write a single word value.</summary>
    /// <param name="value">The word value to write.</param>
    /// <param name="address">The starting address to write.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write words result containing transmission metrics.</returns>
    internal Task<WriteWordsResult> WriteWordAsync(
        short value,
        ushort address,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken) =>
        WriteWordsAsync([value], address, dataType, cancellationToken);

    /// <summary>Write a sequence of word values.</summary>
    /// <param name="values">The word values to write.</param>
    /// <param name="startAddress">The starting address to write.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write words result containing transmission metrics.</returns>
    internal async Task<WriteWordsResult> WriteWordsAsync(
        short[] values,
        ushort startAddress,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        ThrowIfNotInitialized();

        if (values.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The Values Array cannot be Empty");
        }

        if (values.Length > MaximumWriteWordLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                $"The Values Array Length cannot be greater than {MaximumWriteWordLength}");
        }

        if (!ValidateWordDataType(dataType))
        {
            throw new ArgumentException(
                DataTypeMessagePrefix
                    + GetDataTypeName(dataType)
                    + DataTypeMessageSuffix,
                nameof(dataType));
        }

        if (!ValidateWordStartAddress(startAddress, values.Length, dataType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startAddress),
                $"{MaximumWriteStartAddressMessagePrefix}'{GetDataTypeName(dataType)}{DataTypeNameSuffix}");
        }

        var request = WriteMemoryAreaWordRequest.CreateNew(this, startAddress, dataType, values);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteMemoryAreaWordResponse.Validate(request, requestResult.Response);

        return new WriteWordsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>Read the current PLC real-time clock value.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read clock result.</returns>
    internal async Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        var request = ReadClockRequest.CreateNew(this);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        var result = ReadClockResponse.ExtractClock(request, requestResult.Response);

        return new ReadClockResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Clock = result.ClockDateTime,
            DayOfWeek = result.DayOfWeek,
        };
    }

    /// <summary>Write the PLC real-time clock value.</summary>
    /// <param name="newDateTime">The new date and time.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write clock result.</returns>
    internal Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken) =>
        WriteClockAsync(newDateTime, (int)newDateTime.DayOfWeek, cancellationToken);

    /// <summary>Write the PLC real-time clock value with a specific day-of-week.</summary>
    /// <param name="newDateTime">The new date and time.</param>
    /// <param name="newDayOfWeek">The day of week (0-6).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write clock result.</returns>
    internal async Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        var minDateTime = new DateTimeOffset(1998, 1, 1, 0, 0, 0, TimeSpan.Zero);

        if (newDateTime < minDateTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newDateTime),
                $"The Date Time Value cannot be less than '{minDateTime}'");
        }

        var maxDateTime = new DateTimeOffset(2069, 12, 31, 23, 59, 59, TimeSpan.Zero);

        if (newDateTime > maxDateTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newDateTime),
                $"The Date Time Value cannot be greater than '{maxDateTime}'");
        }

        if (newDayOfWeek < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newDayOfWeek), "The Day of Week Value cannot be less than 0");
        }

        if (newDayOfWeek > ProtocolConstants.Six)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newDayOfWeek),
                "The Day of Week Value cannot be greater than 6");
        }

        var request = WriteClockRequest.CreateNew(this, newDateTime.DateTime, (byte)newDayOfWeek);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteClockResponse.Validate(request, requestResult.Response);

        return new WriteClockResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>Read the PLC scan cycle time statistics.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read cycle time result with minimum/maximum/average values.</returns>
    internal async Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        if (IsNSeries && PlcType != PlcType.NJ101 && PlcType != PlcType.NJ301 && PlcType != PlcType.NJ501)
        {
            throw new OmronPLCException("Read Cycle Time is not Supported on the NX/NY Series PLC");
        }

        var request = ReadCycleTimeRequest.CreateNew(this);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        var result = ReadCycleTimeResponse.ExtractCycleTime(request, requestResult.Response);

        return new ReadCycleTimeResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            MinimumCycleTime = result.MinimumCycleTime,
            MaximumCycleTime = result.MaximumCycleTime,
            AverageCycleTime = result.AverageCycleTime,
        };
    }
}
