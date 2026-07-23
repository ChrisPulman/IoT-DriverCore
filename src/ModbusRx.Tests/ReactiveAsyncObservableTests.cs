// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Reactive.Device;
using ReactiveAsyncExtensions = IoT.DriverCore.ModbusRx.Reactive.ModbusAsyncObservableExtensions;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Tests for async observable adapters.</summary>
public class ReactiveAsyncObservableTests
{
    /// <summary>Verifies that an IP master read stream can be consumed as an async observable.</summary>
    /// <returns>A task.</returns>
    [TUnit.Core.Test]
    public async Task ReadHoldingRegistersObservable_DisconnectedSource_EmitsErrorTupleAsync()
    {
        var error = new InvalidOperationException("offline");
        var source = Observable.Return((false, (Exception?)error, (ModbusIpMaster?)null));

        var result = await ReactiveAsyncExtensions
            .ToObservable(ReactiveAsyncExtensions.ReadHoldingRegistersObservable(source, 0, 1, Num.Value10))
            .FirstAsync();

        Assert.Null(result.Data);
        Assert.Same(error, result.Error);
    }

    /// <summary>Verifies async source overloads bridge back through existing polling operators.</summary>
    /// <returns>A task.</returns>
    [TUnit.Core.Test]
    public async Task ReadInputs_WithAsyncSource_EmitsErrorTupleAsync()
    {
        var error = new InvalidOperationException("offline");
        var connection = Observable.Return((false, (Exception?)error, (ModbusIpMaster?)null));
        var source = ReactiveAsyncExtensions.ToModbusObservable(connection);

        var result = await ReactiveAsyncExtensions
            .ToObservable(ReactiveAsyncExtensions.ReadInputs(source, 0, 1, Num.Value10))
            .FirstAsync();

        Assert.Null(result.Data);
        Assert.Same(error, result.Error);
    }
}
#endif
