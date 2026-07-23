// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes small generated-attribute, serial-route, and socket-helper surfaces.</summary>
internal sealed class MitsubishiPublicSurfaceCompletionTests
{
    /// <summary>Stores the generated attribute namespace.</summary>
    private const string AttributeNamespace = "IoT.DriverCore.MitsubishiRx.";

    /// <summary>Exercises generated attribute constructors and property accessors in the product assembly.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GeneratedProductAttributesExposeTheirArgumentsAsync()
    {
        var assembly = typeof(MitsubishiRx).Assembly;
        var cases = new[]
        {
            ("MitsubishiTagAttribute", "TagName", "Value"),
            ("MitsubishiTagClientAttribute", "ClientMemberName", "Client"),
            ("MitsubishiTagClientSchemaAttribute", "SchemaJson", "{}"),
        };

        foreach (var (typeName, propertyName, value) in cases)
        {
            var type = assembly.GetType(AttributeNamespace + typeName, throwOnError: true)!;
            var instance = Activator.CreateInstance(type, value);
            var property = type.GetProperty(propertyName)!;
            await Assert.That(property.GetValue(instance)).IsEqualTo(value);
        }
    }

    /// <summary>Exercises the serial route projection and safe socket closure helper.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialRouteAndSafeSocketClosureProjectConfiguredStateAsync()
    {
        var serial = new MitsubishiSerialOptions(
            "SIM",
            StationNumber: 1,
            NetworkNumber: 2,
            PcNumber: 3,
            RequestDestinationModuleIoNumber: 4,
            RequestDestinationModuleStationNumber: 5,
            SelfStationNumber: 6);
        var route = serial.Route;
        Socket? missing = null;
        missing.SafeClose();

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var accept = listener.AcceptSocketAsync();
        await client.ConnectAsync(endpoint);
        using var server = await accept;
        client.SafeClose();
        server.SafeClose();

        await Assert.That(route.StationNumber).IsEqualTo(serial.StationNumber);
        await Assert.That(route.NetworkNumber).IsEqualTo(serial.NetworkNumber);
        await Assert.That(route.PcNumber).IsEqualTo(serial.PcNumber);
        await Assert.That(client.Connected).IsFalse();
    }
}
