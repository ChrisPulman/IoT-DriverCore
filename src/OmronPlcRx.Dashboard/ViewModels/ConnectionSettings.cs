// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>Stores dashboard PLC connection settings.</summary>
public sealed class ConnectionSettings : ReactiveObject
{
    /// <summary>Stores the Toolbus baud rate.</summary>
    private const int ToolbusBaudRate = 115_200;

    /// <summary>Stores the Toolbus data-bit count.</summary>
    private const int ToolbusDataBits = 8;

    /// <summary>Stores the default maximum serial frame length.</summary>
    private const int DefaultMaximumFrameLength = 1_004;

    /// <summary>Gets or sets the local FINS node identifier.</summary>
    public byte LocalNodeId { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 11;

    /// <summary>Gets or sets the remote FINS node identifier.</summary>
    public byte RemoteNodeId { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 1;

    /// <summary>Gets or sets the selected connection method.</summary>
    public ConnectionMethod Method
    {
        get => field;
        set
        {
            var previous = field;
            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (previous == value)
            {
                return;
            }

            RemoteNodeId = (value, RemoteNodeId) switch
            {
                (ConnectionMethod.Serial, 1) => 0,
                (not ConnectionMethod.Serial, 0) => 1,
                _ => RemoteNodeId,
            };

            RaiseDependentProperty(nameof(IsSerial));
        }
    } = ConnectionMethod.TCP;

    /// <summary>Gets or sets the PLC host name or IP address.</summary>
    public string Host { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = "192.168.2.220";

    /// <summary>Gets or sets the PLC port.</summary>
    public int Port { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 9_600;

    /// <summary>Gets or sets the communication timeout in milliseconds.</summary>
    public int Timeout { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 2_000;

    /// <summary>Gets or sets the retry count.</summary>
    public int Retries { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 1;

    /// <summary>Gets or sets the polling interval in milliseconds.</summary>
    public int PollMs { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 200;

    /// <summary>Gets or sets the serial port name.</summary>
    public string SerialPortName { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = "COM3";

    /// <summary>Gets or sets the serial protocol.</summary>
    public OmronSerialProtocol SerialProtocol
    {
        get => field;
        set
        {
            var previous = field;
            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (previous == value)
            {
                return;
            }

            if (value == OmronSerialProtocol.Toolbus)
            {
                BaudRate = ToolbusBaudRate;
                DataBits = ToolbusDataBits;
                Parity = Parity.None;
                StopBits = StopBits.One;
                Handshake = Handshake.None;
                RtsEnable = true;
                DtrEnable = false;
                RemoteNodeId = 0;
                MaximumFrameLength = DefaultMaximumFrameLength;
            }

            RaiseDependentProperty(nameof(IsHostLinkFins));
        }
    } = OmronSerialProtocol.HostLinkFins;

    /// <summary>Gets or sets the serial baud rate.</summary>
    public int BaudRate { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 9_600;

    /// <summary>Gets or sets the serial data bit count.</summary>
    public int DataBits { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = 7;

    /// <summary>Gets or sets the serial parity.</summary>
    public Parity Parity { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = Parity.Even;

    /// <summary>Gets or sets the serial stop bits.</summary>
    public StopBits StopBits { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = StopBits.Two;

    /// <summary>Gets or sets the serial handshake mode.</summary>
    public Handshake Handshake { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = Handshake.None;

    /// <summary>Gets or sets a value indicating whether RTS is enabled.</summary>
    public bool RtsEnable { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets a value indicating whether DTR is enabled.</summary>
    public bool DtrEnable { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the Host Link unit number.</summary>
    public byte HostLinkUnitNumber { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the Host Link response wait time.</summary>
    public byte ResponseWaitTime { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the Host Link FINS frame mode.</summary>
    public OmronHostLinkFinsFrameMode FrameMode { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = OmronHostLinkFinsFrameMode.Direct;

    /// <summary>Gets or sets the maximum serial frame length.</summary>
    public int MaximumFrameLength { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
        = DefaultMaximumFrameLength;

    /// <summary>Gets a value indicating whether the serial connection method is selected.</summary>
    public bool IsSerial => Method == ConnectionMethod.Serial;

    /// <summary>Gets a value indicating whether Host Link FINS is selected.</summary>
    public bool IsHostLinkFins => SerialProtocol == OmronSerialProtocol.HostLinkFins;

    /// <summary>Creates serial options from the current settings.</summary>
    /// <returns>The configured serial options.</returns>
    public OmronSerialOptions ToSerialOptions()
    {
        var serialPortName = SerialPortName;
        var protocol = SerialProtocol;
        var baudRate = BaudRate;
        var dataBits = DataBits;
        var parity = Parity;
        var stopBits = StopBits;
        var handshake = Handshake;
        var rtsEnable = RtsEnable;
        var dtrEnable = DtrEnable;
        var hostLinkUnitNumber = HostLinkUnitNumber;
        var responseWaitTime = ResponseWaitTime;
        var frameMode = FrameMode;
        var maximumFrameLength = MaximumFrameLength;

        return new(serialPortName)
        {
            Protocol = protocol,
            BaudRate = baudRate,
            DataBits = dataBits,
            Parity = parity,
            StopBits = stopBits,
            Handshake = handshake,
            RtsEnable = rtsEnable,
            DtrEnable = dtrEnable,
            HostLinkUnitNumber = hostLinkUnitNumber,
            ResponseWaitTime = responseWaitTime,
            FrameMode = frameMode,
            MaximumFrameLength = maximumFrameLength,
        };
    }

    /// <summary>Raises a dependent-property change notification.</summary>
    /// <param name="propertyName">Dependent property name.</param>
    private void RaiseDependentProperty(string propertyName) =>
        ((IReactiveObject)this).RaisePropertyChanged(
            new PropertyChangedEventArgs(propertyName));
}
