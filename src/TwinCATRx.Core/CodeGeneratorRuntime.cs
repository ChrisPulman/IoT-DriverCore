// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>Provides native Beckhoff symbol loading for <see cref="CodeGenerator"/>.</summary>
internal sealed class CodeGeneratorRuntime : ICodeGeneratorRuntime
{
    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public void LoadSymbols(string adsAddress, int port, Action<IEnumerable<ISymbol>> consumeSymbols)
    {
        using var client = new AdsClient();
        client.Connect(adsAddress, port);
        var loader = SymbolLoaderFactory.Create(client, SymbolLoaderSettings.Default);
        consumeSymbols(loader.Symbols);
    }

    /// <inheritdoc/>
    public void LoadSymbols(int port, Action<IEnumerable<ISymbol>> consumeSymbols)
    {
        using var client = new AdsClient();
        client.Connect(port);
        var loader = SymbolLoaderFactory.Create(client, SymbolLoaderSettings.Default);
        consumeSymbols(loader.Symbols);
    }

    /// <inheritdoc/>
    public object ReadSymbol(string adsAddress, int port, string variable, Type variableType)
    {
        using var client = new AdsClient();
        client.Connect(adsAddress, port);
        return client.ReadAny(client.CreateVariableHandle(variable), variableType);
    }
}
