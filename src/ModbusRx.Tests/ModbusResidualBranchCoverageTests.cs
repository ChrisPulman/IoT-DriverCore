// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reflection;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.LogicalTags;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.ModbusRx.Utility;
using Moq;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises deterministic residual branches in Modbus protocol primitives.</summary>
public sealed class ModbusResidualBranchCoverageTests
{
    /// <summary>The second representative value.</summary>
    private const int Two = 2;

    /// <summary>The third representative value.</summary>
    private const int Three = 3;

    /// <summary>The fourth representative value.</summary>
    private const int Four = 4;

    /// <summary>The configured fast scan interval.</summary>
    private const int FastScanIntervalMilliseconds = 10;

    /// <summary>The data-length helper name.</summary>
    private const string GetDataLengthMethod = "GetDataLength";

    /// <summary>The range helper name.</summary>
    private const string IsAddressInRangeMethod = "IsAddressInRange";

    /// <summary>The array equality helper name.</summary>
    private const string ArraysEqualMethod = "ArraysEqual";

    /// <summary>Exercises span validation and conversion success branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task UtilityAndConversionBranches_AreDeterministicAsync()
    {
        await NativeAssert.That(
                () => ModbusUtility.NetworkBytesToHostUInt16([0], new ushort[1]))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => ModbusUtility.NetworkBytesToHostUInt16([0, 1], Array.Empty<ushort>()))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.HexToBytes("0".AsSpan(), new byte[1]))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.HexToBytes("00".AsSpan(), Array.Empty<byte>()))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.HexToBytes("GG".AsSpan(), new byte[1]))
            .Throws<FormatException>();
        await NativeAssert.That(() => ModbusUtility.CalculateCrc([1], new byte[1]))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.WriteSingle(1F, new ushort[1], false))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.ReadDouble(new ushort[3], false))
            .Throws<ArgumentException>();

        var single = new ushort[2];
        var doubleValue = new ushort[4];
        Create.FromFloatCore(1F, single, 0, false);
        Create.FromDoubleCore(1D, doubleValue, 0, false);
        await NativeAssert.That(Create.ToFloatCore(single, 0, false)).IsEqualTo(1F);
        await NativeAssert.That(Create.ToDoubleCore(doubleValue, 0, false)).IsEqualTo(1D);
    }

    /// <summary>Exercises array conversion null and short-buffer branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ArrayConversions_HandleNullAndShortBuffersAsync()
    {
        ushort[]? missing = null;
        await NativeAssert.That(Create.ToFloatCore(missing, 0, false)).IsNull();
        await NativeAssert.That(Create.ToFloatCore([1], 0, false)).IsNull();
        await NativeAssert.That(Create.ToDoubleCore(missing, 0, false)).IsNull();
        await NativeAssert.That(Create.ToDoubleCore([1, Two, Three], 0, false)).IsNull();

        Create.FromFloatCore(1F, null!, 0, false);
        var shortSingle = new ushort[1];
        Create.FromFloatCore(1F, shortSingle, 0, false);
        Create.FromDoubleCore(1D, null!, 0, false);
        var shortDouble = new ushort[Three];
        Create.FromDoubleCore(1D, shortDouble, 0, false);
        await NativeAssert.That(shortSingle).IsEquivalentTo(new ushort[1]);
        await NativeAssert.That(shortDouble).IsEquivalentTo(new ushort[Three]);
    }

    /// <summary>Exercises nullable request state and response validation branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ReadWriteRequest_HandlesUninitializedStateAsync()
    {
        var request = new ReadWriteMultipleRegistersRequest();
        await NativeAssert.That(() => request.ProtocolDataUnit).Throws<InvalidOperationException>();
        await NativeAssert.That(request.ToString()).Contains("Write  holding registers");
        request.ValidateResponse(null!);

        SetPrivateProperty(
            request,
            nameof(ReadWriteMultipleRegistersRequest.ReadRequest),
            new ReadHoldingInputRegistersRequest(Modbus.ReadHoldingRegisters, 1, 0, 1));
        await NativeAssert.That(() => request.ProtocolDataUnit).Throws<InvalidOperationException>();
        await NativeAssert.That(() => request.ValidateResponse(null!)).Throws<IOException>();
    }

    /// <summary>Exercises optimized parser argument and resource-lifecycle branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task OptimizedParser_RejectsNullAndIncompleteFramesAsync()
    {
        await NativeAssert.That(() => OptimizedModbusMessageFactory.ParseReadHoldingRegistersResponse(null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.ParseReadHoldingRegistersResponse([1, Three, Four, 0, 1]))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => OptimizedModbusMessageFactory.ParseReadCoilsResponse(null!, 1))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.ParseReadCoilsResponse([1, 1, Two, 1, 0], 1))
            .Throws<ArgumentException>();
        OptimizedModbusMessageFactory.DisposeSharedResources();
        await NativeAssert.That(OptimizedModbusMessageFactory.ValidateMessageCrc(null!)).IsFalse();
    }

    /// <summary>Exercises discriminated-union formatting and register collection ownership.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task UnionAndRegisterCollection_ExerciseAllShapesAsync()
    {
        var optionB = DiscriminatedUnion<string, string>.CreateB("second");
        var nullA = DiscriminatedUnion<string, string>.CreateA(null!);
        var nullB = DiscriminatedUnion<string, string>.CreateB(null!);
        var unset = new DiscriminatedUnion<string, string>();
        await NativeAssert.That(optionB.ToString()).IsEqualTo("second");
        await NativeAssert.That(nullA.ToString()).IsNull();
        await NativeAssert.That(nullB.ToString()).IsNull();
        await NativeAssert.That(unset.ToString()).IsNull();

        IList<ushort> mutable = [1, Two];
        IList<ushort> readOnly = new ReadOnlyCollection<ushort>([Three, Four]);
        var mutableCollection = new RegisterCollection(mutable);
        var copiedCollection = new RegisterCollection(readOnly);
        await NativeAssert.That(mutableCollection.ToString()).IsEqualTo("{1, 2}");
        await NativeAssert.That(copiedCollection.ToString()).IsEqualTo("{3, 4}");
        await NativeAssert.That(() => new RegisterCollection((IList<ushort>)null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises enhanced-observation pure helper branches without elapsed time.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnhancedObservationHelpers_ExerciseAllValueShapesAsync()
    {
        bool[] boolValues = [true, false];
        ushort[] registerValues = [1, Two];
        var boolArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            1,
            ModbusDataType.Coil,
            boolValues);
        var registerArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            1,
            ModbusDataType.HoldingRegister,
            registerValues);

        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, boolArgs)).IsEqualTo(Two);
        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, registerArgs)).IsEqualTo(Two);
        SetPrivateProperty(boolArgs, nameof(DataStoreEventArgs.Data), null);
        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, boolArgs)).IsEqualTo(0);
        await NativeAssert.That(
                Invoke<bool>(IsAddressInRangeMethod, (ushort)1, Two, (ushort)Two, (ushort)Two))
            .IsTrue();
        await NativeAssert.That(
                Invoke<bool>(IsAddressInRangeMethod, (ushort)1, 1, (ushort)Two, (ushort)1))
            .IsFalse();
        await NativeAssert.That(
                Invoke<bool>(IsAddressInRangeMethod, (ushort)Three, 1, (ushort)1, (ushort)1))
            .IsFalse();
    }

    /// <summary>Exercises enhanced-observation generic array helper branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnhancedArrayHelpers_ExerciseAllValueShapesAsync()
    {
        int[] one = [1];
        int[] oneTwo = [1, Two];
        int[] oneThree = [1, Three];
        int[] empty = [0, 0];
        int[] populated = [0, 1];
        await NativeAssert.That(InvokeGeneric<bool>(ArraysEqualMethod, one, oneTwo)).IsFalse();
        await NativeAssert.That(InvokeGeneric<bool>(ArraysEqualMethod, oneTwo, oneThree)).IsFalse();
        await NativeAssert.That(InvokeGeneric<bool>(ArraysEqualMethod, oneTwo, oneTwo)).IsTrue();
        await NativeAssert.That(InvokeGeneric<bool>("IsArrayEmpty", empty)).IsTrue();
        await NativeAssert.That(InvokeGeneric<bool>("IsArrayEmpty", populated)).IsFalse();
    }

    /// <summary>Exercises enhanced-observation null guards, fallback time, and empty snapshots.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnhancedObservation_ValidatesServerAndEmptyStoreAsync()
    {
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveDataChangesEventDriven(null!))
            .Throws<ArgumentNullException>();
        using var server = new ModbusServer
        {
            DataStore = null,
        };
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveDataChangesEventDriven(server, null, null))
            .Throws<InvalidOperationException>();

        var optimized = EnhancedModbusServerExtensions.ObserveDataChangesOptimized(server, 1, null);
        var snapshot = Invoke<ModbusServerDataSnapshot>("CreateSnapshot", server, TimeProvider.System);
        await NativeAssert.That(optimized).IsNotNull();
        await NativeAssert.That(snapshot.IsEmpty).IsTrue();
    }

    /// <summary>Exercises every discriminated data-length option including null values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnhancedDataLength_HandlesNullAndUnsetUnionValuesAsync()
    {
        bool[] noValues = [];
        var args = DataStoreEventArgs.CreateDataStoreEventArgs(
            0,
            ModbusDataType.Coil,
            noValues);
        var nullBooleans = DiscriminatedUnion<
            ReadOnlyCollection<bool>,
            ReadOnlyCollection<ushort>>.CreateA(null!);
        var nullRegisters = DiscriminatedUnion<
            ReadOnlyCollection<bool>,
            ReadOnlyCollection<ushort>>.CreateB(null!);
        var unset = new DiscriminatedUnion<
            ReadOnlyCollection<bool>,
            ReadOnlyCollection<ushort>>();

        SetPrivateProperty(args, nameof(DataStoreEventArgs.Data), nullBooleans);
        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, args)).IsEqualTo(0);
        SetPrivateProperty(args, nameof(DataStoreEventArgs.Data), nullRegisters);
        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, args)).IsEqualTo(0);
        SetPrivateProperty(args, nameof(DataStoreEventArgs.Data), unset);
        await NativeAssert.That(Invoke<int>(GetDataLengthMethod, args)).IsEqualTo(0);
    }

    /// <summary>Exercises logical-client constructor and collection guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ValidatesOptionalDependenciesAndCollectionsAsync()
    {
        var master = new Mock<IModbusMaster>(MockBehavior.Strict);
        await NativeAssert.That(
                () => new ModbusLogicalTagClient(null!, null, null, null))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => new ModbusLogicalTagClient(master.Object, null, TimeSpan.Zero, null))
            .Throws<ArgumentOutOfRangeException>();
        using var client = new ModbusLogicalTagClient(master.Object, null, null, null);
        await NativeAssert.That(
                async () => await client.WriteManyAsync([null!]))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                async () => await client.ObserveManyAsync(null!).GetAsyncEnumerator().MoveNextAsync())
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises logical-client cancellation and unsupported raw areas.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_CancelsReadsAndRejectsUnsupportedAreasAsync()
    {
        var master = new Mock<IModbusMaster>(MockBehavior.Strict);
        using var client = new ModbusLogicalTagClient(master.Object, null, null, null);
        _ = client.CreateTag(new ModbusTagConfiguration(
            "Cancelable",
            1,
            ModbusDataArea.HoldingRegister,
            0,
            1,
            typeof(ushort)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await NativeAssert.That(
                async () => await client.ReadAsync("Cancelable", cancellation.Token))
            .Throws<OperationCanceledException>();
        await NativeAssert.That(
                async () => await InvokePrivateTask<Array>(
                    client,
                    "ReadRawAsync",
                    (byte)1,
                    (ModbusDataArea)int.MaxValue,
                    (ushort)0,
                    (ushort)1))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                () => InvokePrivate(
                    client,
                    "WriteRawAsync",
                    (byte)1,
                    ModbusDataArea.InputRegister,
                    (ushort)0,
                    new ushort[1],
                    false))
            .Throws<TargetInvocationException>();
    }

    /// <summary>Exercises logical-client registered and fallback scan intervals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ResolvesConfiguredAndFallbackScanIntervalsAsync()
    {
        var master = new Mock<IModbusMaster>(MockBehavior.Strict);
        using var client = new ModbusLogicalTagClient(master.Object, null, TimeSpan.FromSeconds(1), null);
        _ = client.CreateTag(new ModbusTagConfiguration(
            "Fast",
            1,
            ModbusDataArea.HoldingRegister,
            0,
            1,
            typeof(ushort))
        {
            ScanInterval = TimeSpan.FromMilliseconds(FastScanIntervalMilliseconds),
        });

        var configured = InvokePrivate(client, "GetScanInterval", "Fast");
        var fallback = InvokePrivate(client, "GetScanInterval", "Missing");
        await NativeAssert.That((TimeSpan)configured!)
            .IsEqualTo(TimeSpan.FromMilliseconds(FastScanIntervalMilliseconds));
        await NativeAssert.That((TimeSpan)fallback!).IsEqualTo(TimeSpan.FromSeconds(1));
    }

    /// <summary>Exercises request null responses and explicit null register data guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task WriteRequests_RejectNullResponsesAndRegisterDataAsync()
    {
        var registers = new WriteMultipleRegistersRequest(1, 0, new RegisterCollection(1));
        var coils = new WriteMultipleCoilsRequest(1, 0, new DiscreteCollection(true));
        var singleRegister = new WriteSingleRegisterRequestResponse(1, 0, 1);
        var singleCoil = new WriteSingleCoilRequestResponse(1, 0, true);
        var read = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 0, 1);

        await NativeAssert.That(() => new WriteMultipleRegistersRequest(1, 0, null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => registers.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => coils.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => singleRegister.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => singleCoil.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => read.ValidateResponse(null!)).Throws<IOException>();
    }

    /// <summary>Exercises array equality null, length, and element branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ArrayEqualityComparer_HandlesEveryArrayShapeAsync()
    {
        var comparer = new ArrayEqualityComparer<int>();
        await NativeAssert.That(comparer.Equals(null, [1])).IsFalse();
        await NativeAssert.That(comparer.Equals([1], null)).IsFalse();
        await NativeAssert.That(comparer.Equals([1], [1, Two])).IsFalse();
        await NativeAssert.That(comparer.Equals([1], [Two])).IsFalse();
        await NativeAssert.That(comparer.Equals([1], [1])).IsTrue();
    }

    /// <summary>Exercises unsupported element types and null list validation in one-based Modbus collections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusDataCollection_RejectsUnsupportedTypesAndNullListsAsync()
    {
        await NativeAssert.That(() => new ModbusDataCollection<int>()).Throws<NotSupportedException>();
        await NativeAssert.That(() => new ModbusDataCollection<bool>((IList<bool>)null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises tag-codec whole-value and byte-order validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TagCodec_RejectsPartialValuesAndUnknownByteOrderAsync()
    {
        await NativeAssert.That(
                () => new ModbusLogicalTag(
                    new ModbusTagConfiguration(
                        "Partial",
                        1,
                        ModbusDataArea.HoldingRegister,
                        0,
                        Three,
                        typeof(int[]))))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => InvokeCodecTransform((ModbusByteOrder)int.MaxValue))
            .Throws<TargetInvocationException>();
    }

    /// <summary>Exercises transport null, disposal, non-request, and response-factory branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Transport_ValidatesFactoriesRequestsAndDisposalAsync()
    {
        var transport = new EmptyTransport();
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 0, 1);
        var response = new ReadCoilsInputsResponse(
            Modbus.ReadCoils,
            1,
            1,
            new DiscreteCollection(true));
        await NativeAssert.That(
                () => transport.UnicastMessage<ReadCoilsInputsResponse>(request, null!))
            .Throws<ArgumentNullException>();
        transport.ValidateResponse(response, response);
        await NativeAssert.That(
                async () => await transport.CreateResponseMessageAsync<ReadCoilsInputsResponse>(
                    Task.FromResult<byte[]>([1, Modbus.ReadCoils, 1, 1]),
                    static () => null!))
            .Throws<InvalidOperationException>();

        InvokeTransportDispose(transport, false);
        transport.Dispose();
        await NativeAssert.That(() => transport.StreamResource).Throws<ObjectDisposedException>();
    }

    /// <summary>Exercises aggregate retry and counted slave-busy terminal branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Transport_HandlesAggregateAndCountedBusyFailuresAsync()
    {
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 0, 1);
        var aggregate = new Mock<ModbusTransport> { CallBase = true };
        aggregate.Object.Retries = 0;
        _ = aggregate.Setup(transport => transport.Write(It.IsAny<IModbusMessage>()));
        _ = aggregate.Setup(transport => transport.ReadResponseAsync(
                It.IsAny<Func<ReadCoilsInputsResponse>>()))
            .Throws(new AggregateException(new TimeoutException()));
        await NativeAssert.That(
                () => aggregate.Object.UnicastMessage(
                    request,
                    static () => new ReadCoilsInputsResponse()))
            .Throws<AggregateException>();

        var busy = new Mock<ModbusTransport> { CallBase = true };
        busy.Object.Retries = 0;
        busy.Object.SlaveBusyUsesRetryCount = true;
        _ = busy.Setup(transport => transport.Write(It.IsAny<IModbusMessage>()));
        _ = busy.Setup(transport => transport.ReadResponseAsync(
                It.IsAny<Func<ReadCoilsInputsResponse>>()).Result)
            .Returns(new SlaveExceptionResponse(
                1,
                Modbus.ReadCoils + Modbus.ExceptionOffset,
                Modbus.SlaveDeviceBusy));
        await NativeAssert.That(
                () => busy.Object.UnicastMessage(
                    request,
                    static () => new ReadCoilsInputsResponse()))
            .Throws<SlaveException>();
    }

    /// <summary>Exercises every EmptyTransport null guard.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EmptyTransport_RejectsNullMessagesAsync()
    {
        var transport = new EmptyTransport();
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 0, 1);
        var response = new ReadCoilsInputsResponse(
            Modbus.ReadCoils,
            1,
            1,
            new DiscreteCollection(true));
        await NativeAssert.That(() => transport.BuildMessageFrame(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => transport.Write(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => transport.OnValidateResponse(null!, response)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => transport.OnValidateResponse(request, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Sets a property through its non-public setter.</summary>
    /// <param name="target">The object that owns the property.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateProperty(object target, string propertyName, object? value) =>
        target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(target, value);

    /// <summary>Invokes a private enhanced-observation helper.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="methodName">The helper name.</param>
    /// <param name="arguments">The helper arguments.</param>
    /// <returns>The helper result.</returns>
    private static TResult Invoke<TResult>(string methodName, params object[] arguments) =>
        (TResult)typeof(EnhancedModbusServerExtensions)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!;

    /// <summary>Invokes a private generic enhanced-observation helper.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="methodName">The helper name.</param>
    /// <param name="arguments">The helper arguments.</param>
    /// <returns>The helper result.</returns>
    private static TResult InvokeGeneric<TResult>(string methodName, params object[] arguments) =>
        (TResult)typeof(EnhancedModbusServerExtensions)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(arguments[0].GetType().GetElementType()!)
            .Invoke(null, arguments)!;

    /// <summary>Invokes a private logical-client method.</summary>
    /// <param name="target">The logical client.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The invocation result.</returns>
    private static object? InvokePrivate(
        ModbusLogicalTagClient target,
        string methodName,
        params object[] arguments) =>
        typeof(ModbusLogicalTagClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method =>
                method.Name == methodName &&
                method.GetParameters().Length == arguments.Length &&
                method.GetParameters()
                    .Select(static parameter => parameter.ParameterType)
                    .Zip(arguments, static (parameterType, argument) => parameterType.IsInstanceOfType(argument))
                    .All(static matches => matches))
            .Invoke(target, arguments);

    /// <summary>Invokes a private asynchronous logical-client method.</summary>
    /// <typeparam name="TResult">The asynchronous result type.</typeparam>
    /// <param name="target">The logical client.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The invocation task.</returns>
    private static Task<TResult> InvokePrivateTask<TResult>(
        ModbusLogicalTagClient target,
        string methodName,
        params object[] arguments) =>
        (Task<TResult>)InvokePrivate(target, methodName, arguments)!;

    /// <summary>Invokes the tag codec's private byte-order transform.</summary>
    /// <param name="order">The byte order.</param>
    private static void InvokeCodecTransform(ModbusByteOrder order) =>
        typeof(ModbusTagCodec)
            .GetMethod("Transform", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new byte[Four], order]);

    /// <summary>Invokes the protected transport disposal overload.</summary>
    /// <param name="transport">The transport to inspect.</param>
    /// <param name="disposing">Whether managed state should be disposed.</param>
    private static void InvokeTransportDispose(ModbusTransport transport, bool disposing) =>
        typeof(ModbusTransport)
            .GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(transport, [disposing]);
}
