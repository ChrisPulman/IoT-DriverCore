// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Provides a deterministic snapshot of native ADS operations issued to an in-memory client.</summary>
public sealed class InMemoryAdsOperationMetrics
{
    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsOperationMetrics"/> class.</summary>
    /// <param name="readOperations">The number of native read attempts.</param>
    /// <param name="writeOperations">The number of native write attempts.</param>
    /// <param name="notificationPublications">The number of notification publication attempts.</param>
    public InMemoryAdsOperationMetrics(long readOperations, long writeOperations, long notificationPublications)
    {
        ReadOperations = readOperations;
        WriteOperations = writeOperations;
        NotificationPublications = notificationPublications;
    }

    /// <summary>Gets the number of native read attempts.</summary>
    public long ReadOperations { get; }

    /// <summary>Gets the number of native write attempts.</summary>
    public long WriteOperations { get; }

    /// <summary>Gets the number of notification publication attempts.</summary>
    public long NotificationPublications { get; }
}
