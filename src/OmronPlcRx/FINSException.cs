// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>An exception that represents a FINS protocol error or invalid response.</summary>
public class FINSException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FINSException"/> class.</summary>
    public FINSException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FINSException" /> class with a message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public FINSException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FINSException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public FINSException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
