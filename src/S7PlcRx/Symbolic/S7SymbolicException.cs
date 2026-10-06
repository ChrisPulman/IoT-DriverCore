// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif
/// <summary>Reports a controller error returned by the symbolic protocol.</summary>
public sealed class S7SymbolicException : Exception
{
    /// <summary>Initializes a new instance of the <see cref = "S7SymbolicException"/> class.</summary>
    public S7SymbolicException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref = "S7SymbolicException"/> class.</summary>
    /// <param name = "message">The message.</param>
    public S7SymbolicException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref = "S7SymbolicException"/> class.</summary>
    /// <param name = "message">The message.</param>
    /// <param name = "innerException">The innerException.</param>
    public S7SymbolicException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref = "S7SymbolicException"/> class.</summary>
    /// <param name = "message">The message.</param>
    /// <param name = "errorCode">The errorCode.</param>
    public S7SymbolicException(string message, ulong errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the controller error code.</summary>
    public ulong ErrorCode { get; }
}
