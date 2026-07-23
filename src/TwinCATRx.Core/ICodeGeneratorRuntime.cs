// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>Defines the native symbol operations consumed by <see cref="CodeGenerator"/>.</summary>
internal interface ICodeGeneratorRuntime : IDisposable
{
    /// <summary>Loads symbols from a remote ADS route.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    /// <param name="consumeSymbols">Consumes symbols while their native connection is active.</param>
    void LoadSymbols(string adsAddress, int port, Action<IEnumerable<ISymbol>> consumeSymbols);

    /// <summary>Loads symbols from a local ADS port.</summary>
    /// <param name="port">The ADS port.</param>
    /// <param name="consumeSymbols">Consumes symbols while their native connection is active.</param>
    void LoadSymbols(int port, Action<IEnumerable<ISymbol>> consumeSymbols);

    /// <summary>Reads one native symbol.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    /// <param name="variable">The variable name.</param>
    /// <param name="variableType">The variable type.</param>
    /// <returns>The native value.</returns>
    object ReadSymbol(string adsAddress, int port, string variable, Type variableType);
}
