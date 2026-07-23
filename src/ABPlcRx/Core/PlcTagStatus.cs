// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using libplctag.NativeImport;

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Status code operation.</summary>
public static class PlcTagStatus
{
    /// <summary>Operation in progress. Not an error.</summary>
    public static readonly int StatusPending = 1;

    /// <summary>No error.</summary>
    public static readonly int StatusOK;

    /// <summary>The operation was aborted.</summary>
    public static readonly int ErrErrAbort = -1;

    /// <summary>The operation failed due to incorrect remote-system configuration.</summary>
    public static readonly int ErrBadConfig = -2;

    /// <summary>The connection failed, for example because the remote PLC was power cycled.</summary>
    public static readonly int ErrBadConnection = -3;

    /// <summary>
    /// The data received from the remote PLC was undecipherable or otherwise not able to be processed.
    /// Can also be returned from a remote system that cannot process the data sent to it.
    /// </summary>
    public static readonly int ErrBadData = -4;

    /// <summary>Usually returned from a remote system when something addressed does not exist.</summary>
    public static readonly int ErrBadDevice = -5;

    /// <summary>Usually returned when the library is unable to connect to a remote system.</summary>
    public static readonly int ErrBadGateway = -6;

    /// <summary>A common error return when something is not correct with the tag creation attribute string.</summary>
    public static readonly int ErrBadParam = -7;

    /// <summary>Usually returned when the remote system returned an unexpected response.</summary>
    public static readonly int ErrBadReply = -8;

    /// <summary>Usually returned by a remote system when something is not in a good state.</summary>
    public static readonly int ErrBadStatus = -9;

    /// <summary>An error occurred trying to close some resource.</summary>
    public static readonly int ErrClose = -10;

    /// <summary>An error occurred trying to create some internal resource.</summary>
    public static readonly int ErrCreate = -11;

    /// <summary>A remote-system error caused by a duplicate value, such as a connection ID.</summary>
    public static readonly int ErrDuplicate = -12;

    /// <summary>An error was returned when trying to encode some data such as a tag name.</summary>
    public static readonly int ErrEncode = -13;

    /// <summary>An internal library error that should be very unusual to see.</summary>
    public static readonly int ErrMutexDestroy = -14;

    /// <summary>An internal library error that should be very unusual to see.</summary>
    public static readonly int ErrMutexInit = -15;

    /// <summary>An internal library error that should be very unusual to see.</summary>
    public static readonly int ErrMutexLock = -16;

    /// <summary>An internal library error that should be very unusual to see.</summary>
    public static readonly int ErrMutexUnlock = -17;

    /// <summary>Often returned from the remote system when an operation is not permitted.</summary>
    public static readonly int ErrNotAllowed = -18;

    /// <summary>Often returned from the remote system when something is not found.</summary>
    public static readonly int ErrNotFound = -19;

    /// <summary>Returned when a valid operation is not implemented.</summary>
    public static readonly int ErrNotImplemented = -20;

    /// <summary>Returned when expected data is not present.</summary>
    public static readonly int ErrNoData = -21;

    /// <summary>Similar to NOT_FOUND.</summary>
    public static readonly int ErrNoMatch = -22;

    /// <summary>Returned by the library when memory allocation fails.</summary>
    public static readonly int ErrNoMem = -23;

    /// <summary>Returned by the remote system when some resource allocation fails.</summary>
    public static readonly int ErrNoResources = -24;

    /// <summary>An internal error that can also indicate an invalid API handle.</summary>
    public static readonly int ErrNullPtr = -25;

    /// <summary>Returned when an error occurs opening a resource such as a socket.</summary>
    public static readonly int ErrOpen = -26;

    /// <summary>Usually returned when trying to write a value into a tag outside of the tag data bounds.</summary>
    public static readonly int ErrOutOfBounds = -27;

    /// <summary>Returned when an error occurs during a read operation, usually related to socket problems.</summary>
    public static readonly int ErrRead = -28;

    /// <summary>An unspecified or untranslatable remote error causes this.</summary>
    public static readonly int ErrRemoteErr = -29;

    /// <summary>An internal library error. If you see this, it is likely that everything is about to crash.</summary>
    public static readonly int ErrThreadCreate = -30;

    /// <summary>Another internal library error that should be very unlikely to see.</summary>
    public static readonly int ErrThreadJoin = -31;

    /// <summary>An operation took too long and timed out.</summary>
    public static readonly int ErrTimeout = -32;

    /// <summary>More data was returned than was expected.</summary>
    public static readonly int ErrTooLarge = -33;

    /// <summary>Insufficient data was returned from the remote system.</summary>
    public static readonly int ErrTooSmall = -34;

    /// <summary>The operation is not supported on the remote system.</summary>
    public static readonly int ErrUnsupported = -35;

    /// <summary>A Winsock-specific error occurred (only on Windows).</summary>
    public static readonly int ErrWinsock = -36;

    /// <summary>An error occurred trying to write, usually to a socket.</summary>
    public static readonly int ErrWrite = -37;

    /// <summary>Check code in error.</summary>
    /// <param name="code">The code.</param>
    /// <returns>
    /// A Value.
    /// </returns>
    public static bool IsError(int code) => code != StatusPending && code != StatusOK;

    /// <summary>Decode error.</summary>
    /// <param name="code">Error code.</param>
    /// <returns>A Value.</returns>
    public static string DecodeError(int code) => plctag.plc_tag_decode_error(code);
}
