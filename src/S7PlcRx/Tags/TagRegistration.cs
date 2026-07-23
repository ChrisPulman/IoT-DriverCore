// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive;
#else
namespace IoT.DriverCore.S7PlcRx;
#endif

/// <summary>Represents a tag and the PLC to which it is registered.</summary>
public sealed class TagRegistration
{
    /// <summary>Initializes a new instance of the <see cref="TagRegistration"/> class.</summary>
    /// <param name="tag">The registered tag.</param>
    /// <param name="plc">The PLC instance.</param>
    internal TagRegistration(ITag? tag, IRxS7? plc)
    {
        Tag = tag;
        Plc = plc;
    }

    /// <summary>Gets the registered tag.</summary>
    public ITag? Tag { get; }

    /// <summary>Gets the PLC instance.</summary>
    public IRxS7? Plc { get; }

    /// <summary>Enables polling for the tag.</summary>
    /// <returns>This registration.</returns>
    public TagRegistration SetPolling() => SetPolling(true);

    /// <summary>Enables or disables polling for the tag.</summary>
    /// <param name="polling">Whether polling is enabled.</param>
    /// <returns>This registration.</returns>
    public TagRegistration SetPolling(bool polling)
    {
        Tag?.SetDoNotPoll(!polling);
        return this;
    }

    /// <summary>Deconstructs the registration.</summary>
    /// <param name="tag">The registered tag.</param>
    /// <param name="plc">The PLC instance.</param>
    public void Deconstruct(out ITag? tag, out IRxS7? plc)
    {
        tag = Tag;
        plc = Plc;
    }
}
