// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveTwinCatRx = IoT.DriverCore.TwinCATRx.Reactive;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Generated System.Reactive stream test fixture.</summary>
[ReactiveTwinCatRx.TwinCatReactiveStream(
    ".ReactiveA",
    typeof(int),
    PropertyName = "ReactiveAValue",
    ObservableName = "ReactiveAValueObservable")]
internal sealed partial class GeneratedReactiveStreams
{
    /// <summary>Gets the generated type marker.</summary>
    internal string GeneratedTypeMarker => nameof(GeneratedReactiveStreams);
}
