// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

global using static CP.IO.Ports.Tests.TestValues;

namespace CP.IO.Ports.Tests;

/// <summary>Named values shared by tests.</summary>
internal static class TestValues
{
    /// <summary>Gets the shared text value.</summary>
    internal const string HelloText = "Hello";

    /// <summary>Gets the shared floating-point value.</summary>
    internal const double NegativeTwelvePointFive = -12.5;

    /// <summary>Gets the byte value for A.</summary>
    internal const byte ByteLetterA = 65;

    /// <summary>Gets the byte value for B.</summary>
    internal const byte ByteLetterB = 66;

    /// <summary>Gets the byte value for C.</summary>
    internal const byte ByteLetterC = 67;

    /// <summary>Gets the byte value seven.</summary>
    internal const byte ByteSeven = 7;

    /// <summary>Gets the byte value eight.</summary>
    internal const byte ByteEight = 8;

    /// <summary>Gets the byte value nine.</summary>
    internal const byte ByteNine = 9;

    /// <summary>Gets the shared short value.</summary>
    internal const short ShortLetterC = 67;

    /// <summary>Gets the integer value two.</summary>
    internal const int Two = 2;

    /// <summary>Gets the integer value three.</summary>
    internal const int Three = 3;

    /// <summary>Gets the integer value four.</summary>
    internal const int Four = 4;

    /// <summary>Gets the integer value five.</summary>
    internal const int Five = 5;

    /// <summary>Gets the integer value seven.</summary>
    internal const int Seven = 7;

    /// <summary>Gets the integer value eight.</summary>
    internal const int Eight = 8;

    /// <summary>Gets the integer value nine.</summary>
    internal const int Nine = 9;

    /// <summary>Gets the integer value twenty-five.</summary>
    internal const int TwentyFive = 25;

    /// <summary>Gets the answer value.</summary>
    internal const int Answer = 42;

    /// <summary>Gets the integer value fifty.</summary>
    internal const int Fifty = 50;

    /// <summary>Gets the integer value for A.</summary>
    internal const int LetterA = 65;

    /// <summary>Gets the integer value for B.</summary>
    internal const int LetterB = 66;

    /// <summary>Gets the integer value for C.</summary>
    internal const int LetterC = 67;

    /// <summary>Gets the integer value one hundred.</summary>
    internal const int Hundred = 100;

    /// <summary>Gets the integer value one hundred twenty-three.</summary>
    internal const int OneHundredTwentyThree = 123;

    /// <summary>Gets the integer value two hundred.</summary>
    internal const int TwoHundred = 200;

    /// <summary>Gets the integer value five hundred.</summary>
    internal const int FiveHundred = 500;

    /// <summary>Gets the integer value one thousand twenty-four.</summary>
    internal const int OneThousandTwentyFour = 1024;

    /// <summary>Gets the integer value two thousand.</summary>
    internal const int TwoThousand = 2000;

    /// <summary>Gets the integer value three thousand.</summary>
    internal const int ThreeThousand = 3000;

    /// <summary>Gets the default baud rate.</summary>
    internal const int DefaultBaudRate = 9600;

    /// <summary>Gets the high baud rate.</summary>
    internal const int HighBaudRate = 115_200;
}
