// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Core.Reactive;
#else
namespace CP.TwinCatRx.Core;
#endif

/// <summary>Interface for Node Emulator.</summary>
/// <seealso cref="IDisposable"/>
public interface INodeEmulator : IDisposable
{
    /// <summary>Gets the nodes.</summary>
    /// <value>The nodes.</value>
    HashSet<INodeEmulator>? Nodes { get; }

    /// <summary>Gets or sets the tag.</summary>
    /// <value>The tag.</value>
    object? Tag { get; set; }

    /// <summary>Gets or sets the text.</summary>
    /// <value>The text.</value>
    string Text { get; set; }
}
