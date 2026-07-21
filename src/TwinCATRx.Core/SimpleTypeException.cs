// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Core.Reactive;
#else
namespace CP.TwinCatRx.Core;
#endif

/// <summary>Exception thrown when a simple type is not supported.</summary>
/// <seealso cref="Exception" />
[Serializable]
public class SimpleTypeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SimpleTypeException"/> class.</summary>
    public SimpleTypeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimpleTypeException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public SimpleTypeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimpleTypeException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    /// The exception that caused this exception, or null when no inner exception is specified.
    /// </param>
    public SimpleTypeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
