// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Converters;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises deterministic response and conversion guard branches without device I/O.</summary>
public sealed class OmronPureBranchCoverageTests
{
    /// <summary>Gets the loopback address used for in-memory protocol requests.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the service identifier attached to test requests.</summary>
    private const byte ServiceIdentifier = 3;

    /// <summary>Gets a known but unmapped FINS main response code.</summary>
    private const byte KnownMainResponseCode = 1;

    /// <summary>Gets an unmapped FINS sub response code.</summary>
    private const byte UnmappedSubResponseCode = 2;

    /// <summary>Gets an unknown FINS main response code.</summary>
    private const byte UnknownMainResponseCode = 0x7E;

    /// <summary>Gets the expected single-word memory read length.</summary>
    private const int WordLength = 1;

    /// <summary>Gets a negative value with both BCD digits populated.</summary>
    private const short NegativeBcdValue = -12;

    /// <summary>Verifies BCD conversion validates internal byte length limits and encodes negative values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdConverter_ValidatesPrivateBoundsAndNegativeDigitsAsync()
    {
        var zeroLengthException = CaptureInnerException(() => ConvertBcdBytes([]));
        var oversizedLengthException = CaptureInnerException(() => ConvertBcdBytes([0, 0, 0, 0, 0]));

        await Assert.That(zeroLengthException).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(oversizedLengthException).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(NegativeBcdValue))).IsEqualTo("1200");
    }

    /// <summary>Verifies pure response parsers reject a null response data payload consistently.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ResponseExtractors_RejectNullPayloadsAsync()
    {
        using var connection = CreateConnection();
        var bitRequest = ReadMemoryAreaBitRequest.CreateNew(
            connection,
            address: 0,
            startBitIndex: 0,
            length: WordLength,
            MemoryBitDataType.CommonIO);
        var wordRequest = ReadMemoryAreaWordRequest.CreateNew(
            connection,
            startAddress: 0,
            length: WordLength,
            MemoryWordDataType.DataMemory);
        var clockRequest = ReadClockRequest.CreateNew(connection);
        var cycleTimeRequest = ReadCycleTimeRequest.CreateNew(connection);

        await Assert.That(CaptureFinsException(() => ReadMemoryAreaBitResponse.ExtractValues(bitRequest, CreateNullDataResponse(bitRequest))))
            .IsNotNull();
        await Assert.That(CaptureFinsException(() => ReadMemoryAreaWordResponse.ExtractValues(wordRequest, CreateNullDataResponse(wordRequest))))
            .IsNotNull();
        await Assert.That(CaptureFinsException(() => ReadClockResponse.ExtractClock(clockRequest, CreateNullDataResponse(clockRequest))))
            .IsNotNull();
        await Assert.That(CaptureFinsException(() => ReadCycleTimeResponse.ExtractCycleTime(cycleTimeRequest, CreateNullDataResponse(cycleTimeRequest))))
            .IsNotNull();
        await Assert.That(CaptureFinsException(() => ReadCPUUnitDataResponse.ExtractData(CreateNullDataResponse(clockRequest))))
            .IsNotNull();
    }

    /// <summary>Verifies CPU data extraction rejects an empty payload and preserves its FINS error contract.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task CpuUnitDataResponse_RejectsEmptyPayloadAsync()
    {
        using var connection = CreateConnection();
        var request = ReadClockRequest.CreateNew(connection);

        var exception = CaptureFinsException(() => ReadCPUUnitDataResponse.ExtractData(CreateResponse(request, [])));

        await Assert.That(exception.Message).Contains("0");
    }

    /// <summary>Verifies generic NJ, NX, and NY model prefixes resolve to the N-series type.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionMetadata_ClassifiesGenericNSeriesPrefixesAsync()
    {
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NJ999")).IsEqualTo(PlcType.NJ_NX_NY_Series);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NX999")).IsEqualTo(PlcType.NJ_NX_NY_Series);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NY999")).IsEqualTo(PlcType.NJ_NX_NY_Series);
    }

    /// <summary>Verifies FINS response errors use both known and unknown default messages.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponse_UsesKnownAndUnknownDefaultErrorMessagesAsync()
    {
        using var connection = CreateConnection();
        var request = ReadClockRequest.CreateNew(connection);

        var knownException = CaptureFinsException(
            () => CreateResponse(request, [], KnownMainResponseCode, UnmappedSubResponseCode));
        var unknownException = CaptureFinsException(
            () => CreateResponse(request, [], UnknownMainResponseCode, UnmappedSubResponseCode));

        await Assert.That(knownException.Message).Contains("Local Node Error");
        await Assert.That(unknownException.Message).Contains("Unknown Error");
    }

    /// <summary>Creates an in-memory UDP connection without opening a socket.</summary>
    /// <returns>The connection.</returns>
    private static OmronPLCConnection CreateConnection() =>
        new(new OmronConnectionOptions(LocalNode, RemoteNode, ConnectionMethod.UDP, LoopbackAddress));

    /// <summary>Creates a successful FINS response and replaces its payload with null.</summary>
    /// <param name="request">The source FINS request.</param>
    /// <returns>A response with a null data payload.</returns>
    private static FINSResponse CreateNullDataResponse(FINSRequest request)
    {
        var response = CreateResponse(request, []);
        var dataProperty = typeof(FINSResponse).GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic);
        dataProperty!.SetValue(response, null);
        return response;
    }

    /// <summary>Creates a FINS response with the selected response codes.</summary>
    /// <param name="request">The source FINS request.</param>
    /// <param name="data">The response payload.</param>
    /// <param name="mainResponseCode">The FINS main response code.</param>
    /// <param name="subResponseCode">The FINS sub response code.</param>
    /// <returns>The parsed FINS response.</returns>
    private static FINSResponse CreateResponse(
        FINSRequest request,
        byte[] data,
        byte mainResponseCode = 0,
        byte subResponseCode = 0)
    {
        _ = request.BuildMessage(ServiceIdentifier);
        var messageLength = FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength + data.Length;
        var message = new byte[messageLength];
        message[FINSResponse.HeaderLength - 1] = request.ServiceID;
        message[FINSResponse.HeaderLength] = request.FunctionCode;
        message[FINSResponse.HeaderLength + 1] = request.SubFunctionCode;
        message[FINSResponse.HeaderLength + FINSResponse.CommandLength] = mainResponseCode;
        message[FINSResponse.HeaderLength + FINSResponse.CommandLength + 1] = subResponseCode;
        Array.Copy(data, 0, message, FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength, data.Length);
        return FINSResponse.CreateNew(message, request);
    }

    /// <summary>Invokes the BCD byte decoder's private length validation routine.</summary>
    /// <param name="bytes">The BCD input bytes.</param>
    private static void ConvertBcdBytes(byte[] bytes)
    {
        var method = typeof(BCDConverter).GetMethod("ConvertToBinaryBytes", BindingFlags.NonPublic | BindingFlags.Static);
        _ = method!.Invoke(null, [bytes]);
    }

    /// <summary>Captures the exception thrown from reflection invocation.</summary>
    /// <param name="action">The action that is expected to throw.</param>
    /// <returns>The underlying exception.</returns>
    private static Exception CaptureInnerException(Action action)
    {
        try
        {
            action();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return exception.InnerException;
        }

        throw new InvalidOperationException("Expected the action to throw.");
    }

    /// <summary>Captures an expected FINS exception.</summary>
    /// <param name="action">The action that is expected to throw.</param>
    /// <returns>The captured exception.</returns>
    private static FINSException CaptureFinsException(Action action)
    {
        try
        {
            action();
        }
        catch (FINSException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected a FINS exception.");
    }
}
