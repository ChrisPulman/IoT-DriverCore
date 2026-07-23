// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the ResponceExtensions type.</summary>
internal static class ResponceExtensions
{
    /// <inheritdoc/>
    extension(Responce result)
    {
        /// <summary>Marks the response as failed.</summary>
        /// <param name="error">The error description.</param>
        /// <param name="errorCode">The protocol error code.</param>
        /// <param name="exception">The associated exception.</param>
        /// <returns>The updated response.</returns>
        internal Responce Fail(string error, int errorCode = 0, Exception? exception = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.IsSucceed = false;
            result.ErrCode = errorCode;
            result.Exception = exception;
            result.Err = error;
            return result.EndTime();
        }

        /// <summary>Creates an untyped response with the same status.</summary>
        /// <returns>The untyped response.</returns>
        internal Responce ToBaseResponse()
        {
            ArgumentNullException.ThrowIfNull(result);
            var response = new Responce();
            _ = response.SetErrInfo(result);
            return response.EndTime();
        }
    }

    /// <inheritdoc/>
    extension<T>(Responce<T> result)
    {
        /// <summary>Marks the typed response as failed.</summary>
        /// <param name="error">The error description.</param>
        /// <param name="errorCode">The protocol error code.</param>
        /// <param name="exception">The associated exception.</param>
        /// <returns>The updated response.</returns>
        internal Responce<T> Fail(string error, int errorCode = 0, Exception? exception = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.IsSucceed = false;
            result.ErrCode = errorCode;
            result.Exception = exception;
            result.Err = error;
            return result.EndTime();
        }
    }
}
