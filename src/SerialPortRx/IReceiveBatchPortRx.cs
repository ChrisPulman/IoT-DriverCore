// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Represents a receive port that publishes the original boundaries of received byte batches.</summary>
public interface IReceiveBatchPortRx : IPortRx
{
    /// <summary>Gets the raw byte batches received after opening the port.</summary>
    IObservable<byte[]> DataReceivedBatches { get; }
}
