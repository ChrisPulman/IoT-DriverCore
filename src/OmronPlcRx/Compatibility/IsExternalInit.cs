// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET462 || NET472 || NET481
namespace System.Runtime.CompilerServices;

/// <summary>Supports compiler-generated init-only setters on older target frameworks.</summary>
internal static class IsExternalInit
{
    /// <summary>Gets a value indicating whether the compiler marker is available.</summary>
    internal static bool IsSupported => true;
}
#endif
