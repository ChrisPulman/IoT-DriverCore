// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Provides numeric values shared by the Modbus protocol implementation.</summary>
internal static class NumericConstants
{
    /// <summary>One tenth.</summary>
    internal const double OneTenth = 0.1;

    /// <summary>One half.</summary>
    internal const double OneHalf = 0.5;

    /// <summary>One thousand represented as a double.</summary>
    internal const double OneThousandDouble = 1000.0;

    /// <summary>The value two.</summary>
    internal const int Two = 2;

    /// <summary>The value three.</summary>
    internal const int Three = 3;

    /// <summary>The value four.</summary>
    internal const int Four = 4;

    /// <summary>The value five.</summary>
    internal const int Five = 5;

    /// <summary>The value six.</summary>
    internal const int Six = 6;

    /// <summary>The value seven.</summary>
    internal const int Seven = 7;

    /// <summary>The value eight.</summary>
    internal const int Eight = 8;

    /// <summary>The value nine.</summary>
    internal const int Nine = 9;

    /// <summary>The value ten.</summary>
    internal const int Ten = 10;

    /// <summary>The value eleven.</summary>
    internal const int Eleven = 11;

    /// <summary>The value sixteen.</summary>
    internal const int Sixteen = 16;

    /// <summary>Thirty two.</summary>
    internal const int ThirtyTwo = 32;

    /// <summary>Sixty one.</summary>
    internal const int SixtyOne = 61;

    /// <summary>Sixty two.</summary>
    internal const int SixtyTwo = 62;

    /// <summary>Sixty four.</summary>
    internal const int SixtyFour = 64;

    /// <summary>One hundred.</summary>
    internal const int OneHundred = 100;

    /// <summary>One hundred and one.</summary>
    internal const int OneHundredOne = 101;

    /// <summary>One hundred and twenty one.</summary>
    internal const int OneHundredTwentyOne = 121;

    /// <summary>One hundred and twenty three.</summary>
    internal const int OneHundredTwentyThree = 123;

    /// <summary>One hundred and twenty five.</summary>
    internal const int OneHundredTwentyFive = 125;

    /// <summary>One hundred and twenty eight.</summary>
    internal const int OneHundredTwentyEight = 128;

    /// <summary>Two hundred and forty seven.</summary>
    internal const int TwoHundredFortySeven = 247;

    /// <summary>Five hundred.</summary>
    internal const int FiveHundred = 500;

    /// <summary>Five hundred and two.</summary>
    internal const int FiveHundredTwo = 502;

    /// <summary>One thousand.</summary>
    internal const int OneThousand = 1000;

    /// <summary>One thousand nine hundred and sixty eight.</summary>
    internal const int OneThousandNineHundredSixtyEight = 1968;

    /// <summary>Two thousand.</summary>
    internal const int TwoThousand = 2000;

    /// <summary>Three thousand.</summary>
    internal const int ThreeThousand = 3000;

    /// <summary>Ten thousand.</summary>
    internal const int TenThousand = 10_000;

    /// <summary>The maximum signed 16-bit integer.</summary>
    internal const int Int16Maximum = 32_767;

    /// <summary>The number of values representable by an unsigned 16-bit integer.</summary>
    internal const int UShortRange = 65_536;

    /// <summary>The maximum unsigned 16-bit integer.</summary>
    internal const ushort UShortMaximum = 65_535;
}
