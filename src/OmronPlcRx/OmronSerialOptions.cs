// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Gets or sets the omron serial options value.</summary>
public sealed record OmronSerialOptions
{
    /// <summary>Initializes a new instance of the <see cref="OmronSerialOptions"/> class.</summary>
    /// <param name="portName">Serial port name, e.g. COM1 or /dev/ttyUSB0.</param>
    public OmronSerialOptions(string portName)
    {
        if (portName is null || portName.Trim().Length == 0)
        {
            throw new ArgumentException("Serial port name cannot be null or whitespace.", nameof(portName));
        }

        PortName = portName;
    }

    /// <summary>Gets or sets the port name value.</summary>
    public string PortName { get; init; }

    /// <summary>Gets or sets the baud rate value.</summary>
    public int BaudRate { get; init; } = 9600;

    /// <summary>Gets or sets the data bits value.</summary>
    public int DataBits { get; init; } = 7;

    /// <summary>Gets or sets the parity value.</summary>
    public Parity Parity { get; init; } = Parity.Even;

    /// <summary>Gets or sets the stop bits value.</summary>
    public StopBits StopBits { get; init; } = StopBits.Two;

    /// <summary>Gets or sets the handshake value.</summary>
    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>Gets or sets the rts enable value.</summary>
    public bool RtsEnable { get; init; }

    /// <summary>Gets or sets the dtr enable value.</summary>
    public bool DtrEnable { get; init; }

    /// <summary>Gets or sets the protocol value.</summary>
    public OmronSerialProtocol Protocol { get; init; } = OmronSerialProtocol.HostLinkFins;

    /// <summary>Gets or sets the host link unit number value.</summary>
    public byte HostLinkUnitNumber { get; init; }

    /// <summary>Gets or sets the response wait time value.</summary>
    public byte ResponseWaitTime { get; init; }

    /// <summary>Gets or sets the frame mode value.</summary>
    public OmronHostLinkFinsFrameMode FrameMode { get; init; } = OmronHostLinkFinsFrameMode.Direct;

    /// <summary>Gets or sets the maximum frame length value.</summary>
    public int MaximumFrameLength { get; init; } = 1004;

    /// <summary>Creates Toolbus serial options using common Omron Toolbus port settings.</summary>
    /// <param name="portName">Serial port name, e.g. COM1 or /dev/ttyUSB0.</param>
    /// <returns>Serial options configured for Toolbus FINS framing.</returns>
    public static OmronSerialOptions CreateToolbus(string portName) => new(portName)
    {
        Protocol = OmronSerialProtocol.Toolbus,
        BaudRate = ProtocolConstants.OneHundredFifteenThousandTwoHundred,
        DataBits = ProtocolConstants.Eight,
        Parity = Parity.None,
        StopBits = StopBits.One,
        Handshake = Handshake.None,
        RtsEnable = true,
        DtrEnable = false,
        MaximumFrameLength = ProtocolConstants.OneThousandFour,
    };

    /// <summary>Validates this options instance.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(OmronSerialProtocol), Protocol))
        {
            throw new ArgumentOutOfRangeException(nameof(Protocol), "The serial protocol is invalid.");
        }

        if (Protocol == OmronSerialProtocol.HostLinkFins && HostLinkUnitNumber > ProtocolConstants.ThirtyOne)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HostLinkUnitNumber),
                "The Host Link unit number must be between 0 and 31.");
        }

        if (Protocol == OmronSerialProtocol.HostLinkFins && ResponseWaitTime > 0x0F)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ResponseWaitTime),
                "The response wait time must be between 0 and 15.");
        }

        if (BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaudRate), "The baud rate must be greater than zero.");
        }

        if (DataBits < ProtocolConstants.Five || DataBits > ProtocolConstants.Eight)
        {
            throw new ArgumentOutOfRangeException(nameof(DataBits), "The data bit count must be between 5 and 8.");
        }

        if (MaximumFrameLength > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(MaximumFrameLength),
            "The maximum frame length must be greater than zero.");
    }
}
