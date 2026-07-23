// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Operations that can be recorded or faulted by <see cref="ABPlcSimulator"/>.</summary>
public enum ABPlcSimulatorOperation
{
    /// <summary>Create a tag handle.</summary>
    Create,

    /// <summary>Destroy a tag handle.</summary>
    Destroy,

    /// <summary>Abort outstanding tag IO.</summary>
    Abort,

    /// <summary>Query tag status.</summary>
    GetStatus,

    /// <summary>Lock a tag handle.</summary>
    Lock,

    /// <summary>Unlock a tag handle.</summary>
    Unlock,

    /// <summary>Read device memory into a tag handle.</summary>
    Read,

    /// <summary>Write a tag handle into device memory.</summary>
    Write,
}
