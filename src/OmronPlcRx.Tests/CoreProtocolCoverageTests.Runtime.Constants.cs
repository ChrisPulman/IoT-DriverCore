// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Tests;

/// <summary>Provides constants shared by runtime protocol coverage fixtures.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Provides the controller version returned by the protocol fixture.</summary>
    private const string ControllerVersion = "1.23";

    /// <summary>Provides the DNS host used by TCP endpoint tests.</summary>
    private const string LocalHostName = "localhost";

    /// <summary>Provides the common data-memory word address.</summary>
    private const int DataMemoryAddress = 100;

    /// <summary>Provides a one-item count.</summary>
    private const int SingleWordCount = 1;

    /// <summary>Provides a two-item count.</summary>
    private const int PairCount = 2;

    /// <summary>Provides a three-item count.</summary>
    private const int TripleCount = 3;

    /// <summary>Provides a four-item count.</summary>
    private const int FourCount = 4;

    /// <summary>Provides the duration used by result value fixtures.</summary>
    private const double ResultDuration = 5.5D;

    /// <summary>Provides the maximum valid bit offset.</summary>
    private const int FifteenBitOffset = 15;

    /// <summary>Provides the first invalid bit offset.</summary>
    private const int SixteenBitOffset = 16;

    /// <summary>Provides an invalid data-memory word address.</summary>
    private const int InvalidWordAddress = 32_768;

    /// <summary>Provides the maximum invalid word-count boundary.</summary>
    private const int MaximumWordCount = 1000;

    /// <summary>Provides an invalid write-word array size.</summary>
    private const int InvalidWriteWordCount = 997;

    /// <summary>Provides the standard FINS TCP port.</summary>
    private const int TcpPort = 9600;

    /// <summary>Provides the first invalid TCP or UDP port.</summary>
    private const int InvalidPort = 65_536;

    /// <summary>Provides the socket timeout used by loopback tests.</summary>
    private const int SocketTimeoutMilliseconds = 1000;

    /// <summary>Provides the first invalid weekday value.</summary>
    private const int MaximumWeekday = 7;

    /// <summary>Provides the protocol weekday used by clock tests.</summary>
    private const byte Weekday = 2;

    /// <summary>Provides the expected bytes sent by a word read request.</summary>
    private const int ExpectedBytesSent = 18;

    /// <summary>Provides the expected bytes received by a word read request.</summary>
    private const int ExpectedBytesReceived = 16;

    /// <summary>Provides the expected average cycle time returned by the PLC fixture.</summary>
    private const double ExpectedAverageCycleTime = 12.3D;

    /// <summary>Provides the expected maximum cycle time returned by the PLC fixture.</summary>
    private const double ExpectedMaximumCycleTime = 45.6D;
}
