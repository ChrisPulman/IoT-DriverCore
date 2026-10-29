// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Provides an active local connection for deterministic client branch tests without native ADS.</summary>
internal static class ClientConnectionTestState
{
    /// <summary>Includes private forwarding properties and internal connection state.</summary>
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary>Names the active connection field.</summary>
    private const string ConnectionField = "_connection";

    /// <summary>Gets connection state exposed by either a field or a forwarding property.</summary>
    /// <typeparam name="T">The expected member type.</typeparam>
    /// <param name="client">The client or connection scope.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member value.</returns>
    internal static T Get<T>(object client, string name) =>
        (T)(client.GetType().GetProperty(name, InstanceMembers)?.GetValue(client)
            ?? client.GetType().GetField(name, InstanceMembers)?.GetValue(client)
            ?? throw new MissingMemberException(client.GetType().FullName, name));

    /// <summary>Sets connection state exposed by either a field or a forwarding property.</summary>
    /// <param name="client">The client or connection scope.</param>
    /// <param name="name">The member name.</param>
    /// <param name="value">The member value.</param>
    internal static void Set(object client, string name, object value)
    {
        if (client.GetType().GetProperty(name, InstanceMembers) is { } property)
        {
            property.SetValue(client, value);
            return;
        }

        var field = client.GetType().GetField(name, InstanceMembers)
            ?? throw new MissingFieldException(client.GetType().FullName, name);
        field.SetValue(client, value);
    }

    /// <summary>Activates the client's initial local state and its actual connection resource collection.</summary>
    /// <param name="client">The client.</param>
    internal static void Activate(object client)
    {
        var connection = Get<object>(client, "_lastConnection");
        Set(client, ConnectionField, connection);
        Set(client, "_cleanup", Get<object>(connection, "Resources"));
    }

    /// <summary>Invokes a private branch in the same execution context used by connection callbacks.</summary>
    /// <param name="client">The client.</param>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The method result.</returns>
    internal static object? Invoke(object client, string name, params object?[] arguments)
    {
        if (client.GetType().GetField(ConnectionField, InstanceMembers)?.GetValue(client) is null)
        {
            Activate(client);
        }

        var method = client.GetType().GetMethod(name, InstanceMembers)
            ?? throw new MissingMethodException(client.GetType().FullName, name);
        var context = Get<object>(client, "_executingConnection");
        var value = context.GetType().GetProperty("Value")!;
        var previous = value.GetValue(context);
        value.SetValue(context, Get<object>(client, ConnectionField));
        try
        {
            return method.Invoke(client, arguments);
        }
        finally
        {
            value.SetValue(context, previous);
        }
    }
}
