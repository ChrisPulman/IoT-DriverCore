// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Reflection;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Enums;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises FINS response identity, subfunction, relay, and response-code validation.</summary>
public sealed class FinsResponseValidationCoverageTests
{
    /// <summary>Gets the minimum complete FINS response length.</summary>
    private const int ResponseLength = 14;

    /// <summary>Gets the response service identifier offset.</summary>
    private const int ServiceIdOffset = 9;

    /// <summary>Gets the response function code offset.</summary>
    private const int FunctionCodeOffset = 10;

    /// <summary>Gets the response subfunction code offset.</summary>
    private const int SubfunctionCodeOffset = 11;

    /// <summary>Gets the main response code offset.</summary>
    private const int MainResponseCodeOffset = 12;

    /// <summary>Gets the sub response code offset.</summary>
    private const int SubResponseCodeOffset = 13;

    /// <summary>Gets the local FINS node.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Verifies every FINS function family validates and resolves a subfunction name.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponse_ValidatesEveryFunctionFamilyAsync()
    {
        var validCombinations = 0;
        foreach (var function in Enum.GetValues<FunctionCode>())
        {
            for (var candidate = 0; candidate <= byte.MaxValue; candidate++)
            {
                var subfunction = (byte)candidate;
                if (!FINSResponse.ValidateSubFunctionCode((byte)function, subfunction))
                {
                    continue;
                }

                var name = InvokeSubfunctionName((byte)function, subfunction);
                await Assert.That(name).IsNotNull();
                validCombinations++;
            }
        }

        await Assert.That(validCombinations > 0).IsTrue();
        await Assert.That(FINSResponse.ValidateSubFunctionCode(byte.MaxValue, byte.MaxValue)).IsFalse();
    }

    /// <summary>Verifies mismatched response identity and malformed messages are rejected.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponse_RejectsMalformedAndMismatchedResponsesAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);

        await AssertThrowsAsync(() => FINSResponse.CreateNew(new byte[ResponseLength - 1], request));
        await AssertThrowsAsync(
            () => FINSResponse.CreateNew(
                CreateResponse(request, byte.MaxValue, request.SubFunctionCode, 0, 0),
                request));
        await AssertThrowsAsync(
            () => FINSResponse.CreateNew(
                CreateResponse(
                    request,
                    (byte)FunctionCode.MemoryArea,
                    (byte)MemoryAreaFunctionCode.Read,
                    0,
                    0),
                request));
        await AssertThrowsAsync(
            () => FINSResponse.CreateNew(
                CreateResponse(request, request.FunctionCode, byte.MaxValue, 0, 0),
                request));
        await AssertThrowsAsync(
            () => FINSResponse.CreateNew(
                CreateResponse(
                    request,
                    request.FunctionCode,
                    (byte)TimeDataFunctionCode.WriteClock,
                    0,
                    0),
                request));

        var serviceMismatch = CreateResponse(
            request,
            request.FunctionCode,
            request.SubFunctionCode,
            0,
            0);
        serviceMismatch.Span[ServiceIdOffset]++;
        await AssertThrowsAsync(() => FINSResponse.CreateNew(serviceMismatch, request));
    }

    /// <summary>Verifies relay, mapped, generic, and ignored response-code paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponse_MapsAllResponseCodeCategoriesAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        var codes = new (byte Main, byte Sub, bool Throws)[]
        {
            (0, 0, false),
            (0, 1, true),
            (0, byte.MaxValue, false),
            (1, 1, true),
            (1, byte.MaxValue, true),
            (byte.MaxValue, byte.MaxValue, true),
            (0x80, 0, true),
        };

        foreach (var code in codes)
        {
            var response = CreateResponse(
                request,
                request.FunctionCode,
                request.SubFunctionCode,
                code.Main,
                code.Sub);
            if (code.Throws)
            {
                await AssertThrowsAsync(() => FINSResponse.CreateNew(response, request));
            }
            else
            {
                var parsed = FINSResponse.CreateNew(response, request);
                await Assert.That(parsed.ServiceID).IsEqualTo(request.ServiceID);
            }
        }
    }

    /// <summary>Creates a binary FINS response with selected identity and response codes.</summary>
    /// <param name="request">Request used for service identity.</param>
    /// <param name="function">Function code.</param>
    /// <param name="subfunction">Subfunction code.</param>
    /// <param name="mainCode">Main response code.</param>
    /// <param name="subCode">Sub response code.</param>
    /// <returns>The binary response.</returns>
    private static Memory<byte> CreateResponse(
        FINSRequest request,
        byte function,
        byte subfunction,
        byte mainCode,
        byte subCode)
    {
        var response = new byte[ResponseLength];
        response[0] = 0xC0;
        response[ServiceIdOffset] = request.ServiceID;
        response[FunctionCodeOffset] = function;
        response[SubfunctionCodeOffset] = subfunction;
        response[MainResponseCodeOffset] = mainCode;
        response[SubResponseCodeOffset] = subCode;
        return response;
    }

    /// <summary>Invokes the private subfunction-name formatter for branch-complete validation.</summary>
    /// <param name="function">Function code.</param>
    /// <param name="subfunction">Subfunction code.</param>
    /// <returns>The resolved subfunction name.</returns>
    private static string? InvokeSubfunctionName(byte function, byte subfunction)
    {
        var method = typeof(FINSResponse).GetMethod(
            "GetSubFunctionCodeName",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(FINSResponse).FullName,
                "GetSubFunctionCodeName");
        return (string?)method.Invoke(null, [function, subfunction]);
    }

    /// <summary>Creates an initialized connection used to construct FINS requests.</summary>
    /// <returns>The request connection.</returns>
    private static OmronPLCConnection CreateRequestConnection()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        return new(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                IPAddress.Loopback.ToString()),
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);
    }

    /// <summary>Captures and verifies a synchronous FINS exception.</summary>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertThrowsAsync(Action action)
    {
        try
        {
            action();
        }
        catch (FINSException exception)
        {
            await Assert.That(exception.Message).IsNotEmpty();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(FINSException)}.");
    }
}
