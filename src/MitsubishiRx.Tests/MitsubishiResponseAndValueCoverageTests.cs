// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides response and value coverage tests.</summary>
internal sealed class MitsubishiResponseAndValueCoverageTests
{
    /// <summary>Stores the expected copied error code.</summary>
    private const int CopiedErrorCode = 17;

    /// <summary>Stores the expected copied response value.</summary>
    private const int CopiedResponseValue = 123;

    /// <summary>Stores the expected error-list entry count.</summary>
    private const int ErrorListEntryCount = 2;

    /// <summary>Stores the <c>FailedMessage</c> test value.</summary>
    private const string FailedMessage = "failed";

    /// <summary>Stores the <c>FirstValueName</c> test value.</summary>
    private const string FirstValueName = "first";

    /// <summary>Stores the ignored value supplied with a null response.</summary>
    private const int IgnoredNullSourceValue = 42;

    /// <summary>Stores the route module-station number.</summary>
    private const byte ModuleStationNumber = 4;

    /// <summary>Stores the route network number.</summary>
    private const byte NetworkNumber = 2;

    /// <summary>Stores the route PC number.</summary>
    private const byte PcNumber = 3;

    /// <summary>Stores the route self-station number.</summary>
    private const byte SelfStationNumber = 5;

    /// <summary>Stores the snapshot count value.</summary>
    private const int SnapshotCountValue = 12;

    /// <summary>Executes the TypedResponseConstructorsHandleNullSources operation.</summary>
    /// <returns>The TypedResponseConstructorsHandleNullSources operation result.</returns>
    [Test]
    internal async Task TypedResponseConstructorsHandleNullSourcesAsync()
    {
        var copyOnly = new Responce<int>((Responce)null!);
        var copyWithValue = new Responce<int>((Responce)null!, IgnoredNullSourceValue);

        await Assert.That(copyOnly.Value).IsEqualTo(default(int));
        await Assert.That(copyWithValue.Value).IsEqualTo(default(int));
        await Assert.That(copyOnly.IsSucceed).IsTrue();
        await Assert.That(copyWithValue.IsSucceed).IsTrue();
    }

    /// <summary>Executes the TypedResponseCopiesBaseResponseErrors operation.</summary>
    /// <returns>The TypedResponseCopiesBaseResponseErrors operation result.</returns>
    [Test]
    internal async Task TypedResponseCopiesBaseResponseErrorsAsync()
    {
        var source = new Responce
        {
            IsSucceed = false,
            Err = FailedMessage,
            ErrCode = CopiedErrorCode,
            Request = "request",
            Response = "response",
            Exception = new InvalidOperationException("boom"),
        };
        source.ErrList.Add(FirstValueName);
        source.ErrList.Add(FirstValueName);

        var typed = new Responce<int>(source, CopiedResponseValue);

        await Assert.That(typed.Value).IsEqualTo(CopiedResponseValue);
        await Assert.That(typed.IsSucceed).IsFalse();
        await Assert.That(typed.Err).IsEqualTo(FailedMessage);
        await Assert.That(typed.ErrCode).IsEqualTo(CopiedErrorCode);
        await Assert.That(typed.Request).IsEqualTo("request");
        await Assert.That(typed.Response).IsEqualTo("response");
        await Assert.That(typed.Exception).IsNotNull();
        await Assert.That(typed.ErrList.Count).IsEqualTo(ErrorListEntryCount);
        await Assert.That(typed.ErrList[0]).IsEqualTo(FailedMessage);
        await Assert.That(typed.ErrList[1]).IsEqualTo(FirstValueName);
    }

    /// <summary>Executes the TypedResponseSetErrInfoIgnoresNullSource operation.</summary>
    /// <returns>The TypedResponseSetErrInfoIgnoresNullSource operation result.</returns>
    [Test]
    internal async Task TypedResponseSetErrInfoIgnoresNullSourceAsync()
    {
        var typed = new Responce<string>("ok");

        var returned = typed.SetErrInfo(null!);

        await Assert.That(returned).IsSameReferenceAs(typed);
        await Assert.That(returned.Value).IsEqualTo("ok");
        await Assert.That(returned.IsSucceed).IsTrue();
    }

    /// <summary>Executes the TagGroupSnapshotReportsMissingAndMismatchedValues operation.</summary>
    /// <returns>The TagGroupSnapshotReportsMissingAndMismatchedValues operation result.</returns>
    [Test]
    internal async Task TagGroupSnapshotReportsMissingAndMismatchedValuesAsync()
    {
        var snapshot = new MitsubishiTagGroupSnapshot(
            "Line",
            new Dictionary<string, object?>
            {
                ["Count"] = SnapshotCountValue,
                ["Empty"] = null,
            });

        await Assert.That(snapshot.GetOptional(new LogicalTagKey<int>("Missing"))).IsEqualTo(default(int));
        await Assert.That(snapshot.GetOptional(new LogicalTagKey<string>("Count"))).IsNull();
        _ = Assert.Throws<KeyNotFoundException>(
            () => snapshot.GetRequired(new LogicalTagKey<int>("Missing")));
        _ = Assert.Throws<InvalidCastException>(
            () => snapshot.GetRequired(new LogicalTagKey<string>("Empty")));
    }

    /// <summary>Executes the SerialRouteStoresConstructorValues operation.</summary>
    /// <returns>The SerialRouteStoresConstructorValues operation result.</returns>
    [Test]
    internal async Task SerialRouteStoresConstructorValuesAsync()
    {
        var route = new MitsubishiSerialRoute(
            1,
            NetworkNumber,
            PcNumber,
            0x1234,
            ModuleStationNumber,
            SelfStationNumber);

        await Assert.That(route.StationNumber).IsEqualTo((byte)1);
        await Assert.That(route.NetworkNumber).IsEqualTo(NetworkNumber);
        await Assert.That(route.PcNumber).IsEqualTo(PcNumber);
        await Assert.That(route.RequestDestinationModuleIoNumber).IsEqualTo((ushort)0x1234);
        await Assert.That(route.RequestDestinationModuleStationNumber).IsEqualTo(ModuleStationNumber);
        await Assert.That(route.SelfStationNumber).IsEqualTo(SelfStationNumber);
    }
}
