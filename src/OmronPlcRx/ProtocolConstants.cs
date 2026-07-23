// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Named numeric values shared by the Omron protocol codecs.</summary>
internal static class ProtocolConstants
{
    /// <summary>Gets the named value two.</summary>
    internal const int Two = 2;

    /// <summary>Gets the named value three.</summary>
    internal const int Three = 3;

    /// <summary>Gets the named value four.</summary>
    internal const int Four = 4;

    /// <summary>Gets the named value five.</summary>
    internal const int Five = 5;

    /// <summary>Gets the named value six.</summary>
    internal const int Six = 6;

    /// <summary>Gets the named value eight.</summary>
    internal const int Eight = 8;

    /// <summary>Gets the named value nine.</summary>
    internal const int Nine = 9;

    /// <summary>Gets the named value ten.</summary>
    internal const int Ten = 10;

    /// <summary>Gets the named double value ten.</summary>
    internal const double TenDouble = 10D;

    /// <summary>Gets the named value fifteen.</summary>
    internal const int Fifteen = 15;

    /// <summary>Gets the named value sixteen.</summary>
    internal const int Sixteen = 16;

    /// <summary>Gets the named value twenty.</summary>
    internal const int Twenty = 20;

    /// <summary>Gets the named value twenty-one.</summary>
    internal const int TwentyOne = 21;

    /// <summary>Gets the named value twenty-two.</summary>
    internal const int TwentyTwo = 22;

    /// <summary>Gets the named value twenty-three.</summary>
    internal const int TwentyThree = 23;

    /// <summary>Gets the named value twenty-four.</summary>
    internal const int TwentyFour = 24;

    /// <summary>Gets the named value twenty-five.</summary>
    internal const int TwentyFive = 25;

    /// <summary>Gets the named value thirty-one.</summary>
    internal const int ThirtyOne = 31;

    /// <summary>Gets the named value fifty.</summary>
    internal const int Fifty = 50;

    /// <summary>Gets the named value seventy.</summary>
    internal const int Seventy = 70;

    /// <summary>Gets the named value one hundred.</summary>
    internal const int OneHundred = 100;

    /// <summary>Gets the named value two hundred fifty.</summary>
    internal const int TwoHundredFifty = 250;

    /// <summary>Gets the named value two hundred fifty-five.</summary>
    internal const int TwoHundredFiftyFive = 255;

    /// <summary>Gets the named unsigned value four hundred ninety-six.</summary>
    internal const ushort FourHundredNinetySixUShort = 496;

    /// <summary>Gets the named unsigned value four hundred ninety-nine.</summary>
    internal const ushort FourHundredNinetyNineUShort = 499;

    /// <summary>Gets the named value five hundred twelve.</summary>
    internal const int FiveHundredTwelve = 512;

    /// <summary>Gets the named value nine hundred sixty.</summary>
    internal const int NineHundredSixty = 960;

    /// <summary>Gets the named unsigned value nine hundred ninety-six.</summary>
    internal const ushort NineHundredNinetySixUShort = 996;

    /// <summary>Gets the named unsigned value nine hundred ninety-nine.</summary>
    internal const ushort NineHundredNinetyNineUShort = 999;

    /// <summary>Gets the named value one thousand four.</summary>
    internal const int OneThousandFour = 1004;

    /// <summary>Gets the named value one thousand five hundred thirty-six.</summary>
    internal const int OneThousandFiveHundredThirtySix = 1536;

    /// <summary>Gets the named value one thousand nine hundred.</summary>
    internal const int OneThousandNineHundred = 1900;

    /// <summary>Gets the named value two thousand.</summary>
    internal const int TwoThousand = 2000;

    /// <summary>Gets the named value six thousand one hundred forty-four.</summary>
    internal const int SixThousandOneHundredFortyFour = 6144;

    /// <summary>Gets the named value eleven thousand five hundred thirty-six.</summary>
    internal const int ElevenThousandFiveHundredThirtySix = 11_536;

    /// <summary>Gets the named value sixteen thousand.</summary>
    internal const int SixteenThousand = 16_000;

    /// <summary>Gets the named value thirty-two thousand seven hundred sixty-eight.</summary>
    internal const int ThirtyTwoThousandSevenHundredSixtyEight = 32_768;

    /// <summary>Gets the named value one hundred fifteen thousand two hundred.</summary>
    internal const int OneHundredFifteenThousandTwoHundred = 115_200;
}
