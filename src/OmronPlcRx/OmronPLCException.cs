// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Represents errors that occur during Omron PLC communication or processing.</summary>
public class OmronPLCException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="OmronPLCException"/> class.</summary>
    public OmronPLCException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPLCException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public OmronPLCException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPLCException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OmronPLCException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
