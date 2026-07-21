// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Message;
#else
namespace ModbusRx.Message;
#endif

/// <summary>Methods specific to a modbus request message.</summary>
public interface IModbusRequest : IModbusMessage
{
    /// <summary>Validate the specified response against the current request.</summary>
    /// <param name="response">The response.</param>
    void ValidateResponse(IModbusMessage response);
}
