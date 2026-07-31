// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Message;
#else
using IoT.Driver.ModbusRx.Message;
#endif
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Utility;
#else
using IoT.Driver.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.IO;
#else
namespace IoT.Driver.ModbusRx.IO;
#endif

/// <summary>Refined Abstraction - http://en.wikipedia.org/wiki/Bridge_Pattern.</summary>
internal sealed class ModbusAsciiTransport : ModbusSerialTransport
{
    /// <summary>Initializes a new instance of the Modbus Ascii Transport class.</summary>
    /// <param name="streamResource">The stream Resource value.</param>
    internal ModbusAsciiTransport(IStreamResource streamResource)
        : base(streamResource) => Debug.Assert(streamResource is not null, "Argument streamResource cannot be null.");

    internal override byte[] BuildMessageFrame(IModbusMessage message)
    {
        var msgFrame = message.ToMessageFrame();

        var msgFrameAscii = ModbusUtility.GetAsciiBytes(msgFrame);
        var lrcAscii = ModbusUtility.GetAsciiBytes(ModbusUtility.CalculateLrc(msgFrame));
        var newLineAsciiBytes = new byte[] { (byte)'\r', (byte)'\n' };

        using var frame = new MemoryStream(1 + msgFrameAscii.Length + lrcAscii.Length + newLineAsciiBytes.Length);
        frame.WriteByte((byte)':');
        frame.Write(msgFrameAscii, 0, msgFrameAscii.Length);
        frame.Write(lrcAscii, 0, lrcAscii.Length);
        frame.Write(newLineAsciiBytes, 0, newLineAsciiBytes.Length);

        return frame.ToArray();
    }

    internal override bool ChecksumsMatch(IModbusMessage message, byte[] messageFrame) =>
        ModbusUtility.CalculateLrc(message.ToMessageFrame()) == messageFrame[messageFrame.GetUpperBound(0)];

    internal override Task<byte[]> ReadRequestAsync() =>
        ReadRequestResponseAsync();

    internal override Task<IModbusMessage> ReadResponseAsync<T>(Func<T> responseFactory) =>
        CreateResponseAsync(ReadRequestResponseAsync(), responseFactory);

    /// <summary>Executes the Read Request Response operation.</summary>
    /// <returns>The result.</returns>
    internal async Task<byte[]> ReadRequestResponseAsync()
    {
        // read message frame, removing frame start ':'
        var frameHex = (await StreamResourceUtility.ReadLineAsync(StreamResource)).Substring(1);

        // convert hex to bytes
        var frame = ModbusUtility.HexToBytes(frameHex);
        Debug.WriteLine($"RX: {string.Join(", ", frame)}");

        if (frame.Length < 3)
        {
            throw new IOException("Premature end of stream, message truncated.");
        }

        return frame;
    }
}
