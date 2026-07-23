// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using IoT.DriverCore.S7PlcRx.Enums;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests for internal S7MultiVar request building.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7MultiVarRequestBuilderTests
{
    /// <summary>Maximum number of items that a single S7 request can contain.</summary>
    private const int MaximumRequestItemCount = 255;

    /// <summary>Address increment between successive test items.</summary>
    private const int ItemAddressStride = 2;

    /// <summary>Minimum encoded length of a read-variable request.</summary>
    private const int MinimumReadRequestLength = 31;

    /// <summary>Minimum encoded length of a write-variable request.</summary>
    private const int MinimumWriteRequestLength = 35;

    /// <summary>Gets the compact representation displayed by the debugger.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? GetType().Name;
    }

    /// <summary>Ensures a read-var request is built with a valid TPKT header and correct item count.</summary>
    [Test]
    public void BuildReadVarRequest_WithSingleItem_ShouldBuildPacket()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var s7MultiVar = GetS7MultiVarType();
        var readItemType = GetNestedType(s7MultiVar, "ReadItem");

        var items = (System.Collections.IList)(Activator.CreateInstance(typeof(List<>).MakeGenericType(readItemType))
            ?? throw new InvalidOperationException("Read item collection could not be created."));
        _ = items.Add(Activator.CreateInstance(readItemType, DataType.DataBlock, 1, 0, 1, "T0")
            ?? throw new InvalidOperationException("Read item could not be created."));

        var method = s7MultiVar.GetMethod("BuildReadVarRequest", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildReadVarRequest was not found.");

        var bytes = (byte[])(method.Invoke(null, [items])
            ?? throw new InvalidOperationException("BuildReadVarRequest returned null."));

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(MinimumReadRequestLength));

        // TPKT
        Assert.That(bytes[0], Is.EqualTo(0x03));
        Assert.That(bytes[1], Is.EqualTo(0x00));
        Assert.That(bytes[2], Is.EqualTo(0x00));

        // function = Read Var (0x04) and item count
        Assert.That(bytes[17], Is.EqualTo(0x04));
        Assert.That(bytes[18], Is.EqualTo(0x01));
    }

    /// <summary>Ensures a write-var request includes a non-zero data section and correct item count.</summary>
    [Test]
    public void BuildWriteVarRequest_WithSingleItem_ShouldBuildPacket()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var s7MultiVar = GetS7MultiVarType();
        var writeItemType = GetNestedType(s7MultiVar, "WriteItem");

        var items = (System.Collections.IList)(Activator.CreateInstance(typeof(List<>).MakeGenericType(writeItemType))
            ?? throw new InvalidOperationException("Write item collection could not be created."));
        _ = items.Add(Activator.CreateInstance(
            writeItemType,
            DataType.DataBlock,
            1,
            0,
            1,
            (byte)0x02,
            (byte[])[0x12, 0x34],
            "W0") ?? throw new InvalidOperationException("Write item could not be created."));

        var method = s7MultiVar.GetMethod("BuildWriteVarRequest", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildWriteVarRequest was not found.");

        var bytes = (byte[])(method.Invoke(null, [items])
            ?? throw new InvalidOperationException("BuildWriteVarRequest returned null."));

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(MinimumWriteRequestLength));

        // TPKT
        Assert.That(bytes[0], Is.EqualTo(0x03));
        Assert.That(bytes[1], Is.EqualTo(0x00));
        Assert.That(bytes[2], Is.EqualTo(0x00));

        // function = Write Var (0x05) and item count
        Assert.That(bytes[17], Is.EqualTo(0x05));
        Assert.That(bytes[18], Is.EqualTo(0x01));

        // data length should be non-zero for write
        var dataLen = (bytes[15] << 8) | bytes[16];
        Assert.That(dataLen, Is.GreaterThan(0));
    }

    /// <summary>Ensures item count constraint is enforced for read requests.</summary>
    [Test]
    public void BuildReadVarRequest_WhenMoreThan255Items_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var s7MultiVar = GetS7MultiVarType();
        var readItemType = GetNestedType(s7MultiVar, "ReadItem");

        var listType = typeof(List<>).MakeGenericType(readItemType);
        var items = (System.Collections.IList)(Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException("Read item collection could not be created."));
        for (var i = 0; i <= MaximumRequestItemCount; i++)
        {
            _ = items.Add(Activator.CreateInstance(
                readItemType,
                DataType.DataBlock,
                1,
                i * ItemAddressStride,
                1,
                $"T{i}") ?? throw new InvalidOperationException("Read item could not be created."));
        }

        var method = s7MultiVar.GetMethod("BuildReadVarRequest", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildReadVarRequest was not found.");

        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [items]))
            ?? throw new InvalidOperationException("Expected an invocation exception.");
        Assert.That(ex.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>Gets the internal <c>S7MultiVar</c> type.</summary>
    /// <returns>The resolved internal multi-variable type.</returns>
    private static Type GetS7MultiVarType()
    {
        var asm = typeof(RxS7).Assembly;
        return asm.GetType("IoT.DriverCore.S7PlcRx.Core.S7MultiVar", throwOnError: false)
            ?? throw new InvalidOperationException("S7MultiVar was not found.");
    }

    /// <summary>Gets a non-public nested type by name.</summary>
    /// <param name="parent">The containing type.</param>
    /// <param name="name">The nested type name.</param>
    /// <returns>The resolved nested type.</returns>
    private static Type GetNestedType(Type parent, string name)
    {
        return parent.GetNestedType(name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Nested type '{name}' was not found.");
    }
}
