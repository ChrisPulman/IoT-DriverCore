// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Specifies the S7Plus wire representation of a value.</summary>
public enum S7SymbolicDataType
{
    /// <summary>A null value.</summary>
    Null = 0,

    /// <summary>A Boolean value.</summary>
    Bool = 1,

    /// <summary>An unsigned 8-bit integer.</summary>
    USInt = 0x2,

    /// <summary>An unsigned 16-bit integer.</summary>
    UInt = 3,

    /// <summary>An unsigned variable-length 32-bit integer.</summary>
    UDInt = 0x4,

    /// <summary>An unsigned variable-length 64-bit integer.</summary>
    ULInt = 0x5,

    /// <summary>A signed 8-bit integer.</summary>
    SInt = 6,

    /// <summary>A signed 16-bit integer.</summary>
    Int = 7,

    /// <summary>A signed variable-length 32-bit integer.</summary>
    DInt = 0x8,

    /// <summary>A signed variable-length 64-bit integer.</summary>
    LInt = 9,

    /// <summary>An eight-bit binary value.</summary>
    Byte = 0x0a,

    /// <summary>A sixteen-bit binary value.</summary>
    Word = 0x0b,

    /// <summary>A thirty-two-bit binary value.</summary>
    DWord = 0x0c,

    /// <summary>A sixty-four-bit binary value.</summary>
    LWord = 0x0d,

    /// <summary>An IEEE 754 single-precision number.</summary>
    Real = 0x0e,

    /// <summary>An IEEE 754 double-precision number.</summary>
    LReal = 0x0f,

    /// <summary>A 64-bit timestamp.</summary>
    Timestamp = 0x10,

    /// <summary>A signed variable-length duration.</summary>
    Timespan = 0x11,

    /// <summary>A fixed-width relation identifier.</summary>
    RID = 0x12,

    /// <summary>A variable-length attribute identifier.</summary>
    AID = 0x13,

    /// <summary>A root identifier and byte payload.</summary>
    Blob = 0x14,

    /// <summary>A UTF-8 string.</summary>
    WString = 0x15,

    /// <summary>A nested typed value.</summary>
    Variant = 0x16,

    /// <summary>A structured value.</summary>
    Struct = 0x17,

    /// <summary>A reserved wire identifier that cannot encode a value.</summary>
    Reserved = 0x18,

    /// <summary>An S7 string byte payload.</summary>
    S7String = 0x19,
}
