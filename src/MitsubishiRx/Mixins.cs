// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the Mixins type.</summary>
internal static class Mixins
{
    /// <inheritdoc/>
    extension(Socket? socket)
    {
        /// <summary>Closes the socket without surfacing teardown exceptions.</summary>
        internal void SafeClose()
        {
            try
            {
                if (socket?.Connected ?? false)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            try
            {
                socket?.Close();
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
