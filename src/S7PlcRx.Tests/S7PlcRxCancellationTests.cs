// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Enums;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests;

/// <summary>Tests cancellation-aware APIs.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7PlcRxCancellationTests
{
    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures pre-canceled tokens cancel ValueAsync immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ValueAsync_WhenCanceled_ShouldThrowOperationCanceledExceptionAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1)));
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "T0", "DB1.DBW0").SetPolling(false);

        using var cts = new CancellationTokenSource();
#if NETFRAMEWORK
        cts.Cancel();
#else
        await cts.CancelAsync();
#endif

        await TUnitAssert.That(() => plc.ReadAsync(
            new LogicalTagKey<ushort>("T0"),
            cts.Token)).Throws<OperationCanceledException>();
    }
}
