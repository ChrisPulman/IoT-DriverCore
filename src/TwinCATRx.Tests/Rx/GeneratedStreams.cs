// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.TwinCATRx;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Generated stream test fixture.</summary>
[TwinCatReactiveStream(".A", typeof(int), PropertyName = "AValue", ObservableName = "AValueObservable")]
internal sealed partial class GeneratedStreams
{
    /// <summary>Gets the generated type marker.</summary>
    internal string GeneratedTypeMarker => nameof(GeneratedStreams);
}
