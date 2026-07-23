// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Represents a deterministic in-memory ADS operation failure.</summary>
public sealed class InMemoryAdsException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsException"/> class.</summary>
    public InMemoryAdsException()
        : this(InMemoryAdsOperation.Connect, "An in-memory ADS operation failed.", null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public InMemoryAdsException(string message)
        : this(InMemoryAdsOperation.Connect, message, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The failure that caused this exception.</param>
    public InMemoryAdsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsException"/> class.</summary>
    /// <param name="operation">The failed operation.</param>
    /// <param name="message">The failure message.</param>
    public InMemoryAdsException(InMemoryAdsOperation operation, string message)
        : this(operation, message, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsException"/> class.</summary>
    /// <param name="operation">The failed operation.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="variable">The optional ADS variable involved in the failure.</param>
    public InMemoryAdsException(InMemoryAdsOperation operation, string message, string? variable)
        : base(message)
    {
        Operation = operation;
        Variable = variable;
    }

    /// <summary>Gets the failed operation.</summary>
    public InMemoryAdsOperation Operation { get; }

    /// <summary>Gets the optional ADS variable involved in the failure.</summary>
    public string? Variable { get; }
}
