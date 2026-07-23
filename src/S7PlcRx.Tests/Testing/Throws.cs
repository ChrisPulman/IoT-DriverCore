// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Creates exception constraints.</summary>
public static class Throws
{
    /// <summary>Gets a constraint for an exception instance of the supplied type.</summary>
    /// <typeparam name="TException">The type parameter.</typeparam>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 76.</param>
    /// <returns>The result.</returns>
    public static ThrowsConstraint InstanceOf<TException>(params TException[] typeMarker)
        where TException : Exception
    {
        _ = typeMarker;
        return new(typeof(TException));
    }
}
