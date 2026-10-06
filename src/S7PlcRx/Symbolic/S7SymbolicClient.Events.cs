// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Handles the s7symbolic client.</summary>
public sealed partial class S7SymbolicClient
{
    /// <summary>The PLC object display name attribute.</summary>
    private const uint SubscriptionNameAttribute = 233;

    /// <summary>The native PLC program object.</summary>
    private const uint PlcProgramObject = 3;

    /// <summary>The alarm subscription function class.</summary>
    private const byte AlarmFunctionClass = 2;

    /// <summary>The alarm subscription route mode.</summary>
    private const byte AlarmRouteMode = 2;

    /// <summary>The alarm reference trigger and transmit mode.</summary>
    private const byte TriggerAndTransmitMode = 3;

    /// <summary>The MaximumSubscriptionItems protocol constant.</summary>
    private const int MaximumSubscriptionItems = 1_024;

    /// <summary>The MinimumCycleMilliseconds protocol constant.</summary>
    private const int MinimumCycleMilliseconds = 100;

    /// <summary>The ReferenceList protocol constant.</summary>
    private const int ReferenceList = 1_048;

    /// <summary>The CpuExecutionUnit protocol constant.</summary>
    private const int CpuExecutionUnit = 52;

    /// <summary>The OperatingStateRequest protocol constant.</summary>
    private const int OperatingStateRequest = 2_167;

    /// <summary>The ObjectComment protocol constant.</summary>
    private const int ObjectComment = 4_288;

    /// <summary>The LineComments protocol constant.</summary>
    private const int LineComments = 2_546;

    /// <summary>The StaticAlarmDefinitions protocol constant.</summary>
    private const int StaticAlarmDefinitions = 7_859;

    /// <summary>The AlarmTexts protocol constant.</summary>
    private const int AlarmTexts = 2_715;

    /// <summary>The CpuAlarmIdAttribute protocol constant.</summary>
    private const int CpuAlarmIdAttribute = 2_670;

    /// <summary>The AssociatedValuesAttribute protocol constant.</summary>
    private const int AssociatedValuesAttribute = 3_476;

    /// <summary>The AlarmReferenceClass protocol constant.</summary>
    private const int AlarmReferenceClass = 2_662;

    /// <summary>The TriggerTransmitMode protocol constant.</summary>
    private const int TriggerTransmitMode = 1_005;

    /// <summary>The AlarmDomainFilter protocol constant.</summary>
    private const int AlarmDomainFilter = 2_659;

    /// <summary>The InitialCreditLimit protocol constant.</summary>
    private const int InitialCreditLimit = 10;

    /// <summary>The AdditionalAlarmDomainFilter protocol constant.</summary>
    private const int AdditionalAlarmDomainFilter = 7_731;

    /// <summary>The AlarmLanguages protocol constant.</summary>
    private const int AlarmLanguages = 8_181;

    /// <summary>The SendAlarmTexts protocol constant.</summary>
    private const int SendAlarmTexts = 8_173;

    /// <summary>The AlarmSubsystemRelation protocol constant.</summary>
    private const int AlarmSubsystemRelation = 2_660;

    /// <summary>The AlarmSubsystem protocol constant.</summary>
    private const int AlarmSubsystem = 8;

    /// <summary>The SubscriptionClass protocol constant.</summary>
    private const int SubscriptionClass = 1_001;

    /// <summary>The SubscriptionFunctionClass protocol constant.</summary>
    private const int SubscriptionFunctionClass = 1_082;

    /// <summary>The MissedSendings protocol constant.</summary>
    private const int MissedSendings = 1_002;

    /// <summary>The SubsystemError protocol constant.</summary>
    private const int SubsystemError = 1_003;

    /// <summary>The RouteMode protocol constant.</summary>
    private const int RouteMode = 1_040;

    /// <summary>The SubscriptionActive protocol constant.</summary>
    private const int SubscriptionActive = 1_041;

    /// <summary>The CycleTime protocol constant.</summary>
    private const int CycleTime = 1_049;

    /// <summary>The DelayTime protocol constant.</summary>
    private const int DelayTime = 1_050;

    /// <summary>The SubscriptionDisabled protocol constant.</summary>
    private const int SubscriptionDisabled = 1_051;

    /// <summary>The SubscriptionCount protocol constant.</summary>
    private const int SubscriptionCount = 1_052;

    /// <summary>The CreditLimitAttribute protocol constant.</summary>
    private const int CreditLimitAttribute = 1_053;

    /// <summary>The SubscriptionTicks protocol constant.</summary>
    private const int SubscriptionTicks = 1_054;

    /// <summary>The requested comment metadata attributes.</summary>
    private static readonly uint[] _commentAttributes = [ObjectComment, LineComments];

    /// <summary>The requested alarm metadata attributes.</summary>
    private static readonly uint[] _alarmAttributes = [StaticAlarmDefinitions, AlarmTexts, CpuAlarmIdAttribute, AssociatedValuesAttribute];

    /// <summary>The initial empty alarm variable reference list.</summary>
    private static readonly uint[] _initialAlarmReferences = [0x80010000, 0, 0];

    /// <summary>The all-domain alarm filter.</summary>
    private static readonly ushort[] _allAlarmDomains = [ushort.MaxValue];

    /// <summary>The subscription relation.</summary>
    private int _subscriptionRelation;

    /// <summary>Creates a credit controlled PLC variable subscription.</summary>
    /// <param name="paths">The paths.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The operation result.</returns>
    public Task<S7SymbolicSubscription> SubscribeAsync(string[] paths, TimeSpan cycle) => SubscribeAsync(paths, cycle, CancellationToken.None);

    /// <summary>Creates a credit controlled PLC variable subscription.</summary>
    /// <param name="paths">The paths.</param>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7SymbolicSubscription> SubscribeAsync(string[] paths, TimeSpan cycle, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        if (paths is null)
        {
            throw new ArgumentNullException(nameof(paths));
        }
#else
        ArgumentNullException.ThrowIfNull(paths);
#endif
        if (paths.Length == 0 || paths.Length > MaximumSubscriptionItems)
        {
            throw new ArgumentOutOfRangeException(nameof(paths));
        }

        if (cycle.TotalMilliseconds < MinimumCycleMilliseconds || cycle.TotalMilliseconds > ushort.MaxValue || cycle.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle));
        }

        var symbols = await ResolveSubscriptionSymbolsAsync(paths, cancellationToken).ConfigureAwait(false);
        var addresses = BuildReferenceList(symbols);
        var stream = new S7SymbolicSubscriptionStream<S7SymbolicChange>(Session, notification => ProjectChanges(notification, symbols), cancellationToken);
        try
        {
            var obj = SubscriptionObject((uint)cycle.TotalMilliseconds, false);
            obj.Attributes[ReferenceList] = new(S7SymbolicDataType.UDInt, addresses.ToArray(), 0x20);
            var result = await Session.CreateObjectAsync(Session.SubscriptionSessionId, obj, cancellationToken).ConfigureAwait(false);
            if (result.Length == 0 || result[0] == 0)
            {
                throw new InvalidDataException("PLC returned an invalid subscription object identifier.");
            }

            stream.Activate(result[0]);
            return new(stream);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Requests RUN or STOP from the PLC execution unit.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The operation result.</returns>
    public Task SetOperatingStateAsync(S7OperatingState state) => SetOperatingStateAsync(state, CancellationToken.None);

    /// <summary>Requests RUN or STOP from the PLC execution unit.</summary>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public Task SetOperatingStateAsync(S7OperatingState state, CancellationToken cancellationToken)
    {
        if (state is not S7OperatingState.Stop and not S7OperatingState.Run)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        return Session.SetAttributeAsync(CpuExecutionUnit, OperatingStateRequest, new(S7SymbolicDataType.DInt, (int)state), cancellationToken);
    }

    /// <summary>Reads the execution unit operating state request attribute.</summary>
    /// <returns>The operation result.</returns>
    public Task<S7OperatingState> GetRequestedOperatingStateAsync() => GetRequestedOperatingStateAsync(CancellationToken.None);

    /// <summary>Reads the execution unit operating state request attribute.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7OperatingState> GetRequestedOperatingStateAsync(CancellationToken cancellationToken)
    {
        var value = await Session.GetAttributeAsync(CpuExecutionUnit, OperatingStateRequest, cancellationToken).ConfigureAwait(false);
        return (S7OperatingState)Convert.ToInt32(value.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Reads PLC comment metadata including localized strings and compressed comment blobs.</summary>
    /// <param name="objectId">The object id.</param>
    /// <returns>The operation result.</returns>
    public Task<IReadOnlyList<S7SymbolicAlarmMetadata>> GetCommentsAsync(uint objectId) => GetCommentsAsync(objectId, CancellationToken.None);

    /// <summary>Reads PLC comment metadata including localized strings and compressed comment blobs.</summary>
    /// <param name="objectId">The object id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<IReadOnlyList<S7SymbolicAlarmMetadata>> GetCommentsAsync(uint objectId, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        if (objectId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectId));
        }
#else
        ArgumentOutOfRangeException.ThrowIfZero(objectId);
#endif
        var objects = await Session.ExploreAsync(objectId, true, _commentAttributes, cancellationToken).ConfigureAwait(false);
        return Metadata(objects);
    }

    /// <summary>Browses alarm definitions and their localized text and associated value metadata.</summary>
    /// <param name="languageId">The language id.</param>
    /// <returns>The operation result.</returns>
    public Task<IReadOnlyList<S7SymbolicAlarmMetadata>> BrowseAlarmsAsync(uint languageId) => BrowseAlarmsAsync(languageId, CancellationToken.None);

    /// <summary>Browses alarm definitions and their localized text and associated value metadata.</summary>
    /// <param name="languageId">The language id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<IReadOnlyList<S7SymbolicAlarmMetadata>> BrowseAlarmsAsync(uint languageId, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        if (languageId > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(languageId));
        }
#else
        ArgumentOutOfRangeException.ThrowIfGreaterThan(languageId, ushort.MaxValue);
#endif
        var result = new List<S7SymbolicAlarmMetadata>(Metadata(await Session.ExploreAsync(0x8a7e0000, true, [], cancellationToken).ConfigureAwait(false), languageId));
        result.AddRange(Metadata(await Session.ExploreAsync(PlcProgramObject, true, _alarmAttributes, cancellationToken).ConfigureAwait(false), languageId));
        return result;
    }

    /// <summary>Subscribes to PLC alarms and requests alarm texts for the specified LCID.</summary>
    /// <param name="languageId">The language id.</param>
    /// <returns>The operation result.</returns>
    public Task<S7SymbolicAlarmSubscription> SubscribeAlarmsAsync(uint languageId) => SubscribeAlarmsAsync(languageId, CancellationToken.None);

    /// <summary>Subscribes to PLC alarms and requests alarm texts for the specified LCID.</summary>
    /// <param name="languageId">The language id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7SymbolicAlarmSubscription> SubscribeAlarmsAsync(uint languageId, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        if (languageId > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(languageId));
        }
#else
        ArgumentOutOfRangeException.ThrowIfGreaterThan(languageId, ushort.MaxValue);
#endif
        var stream = new S7SymbolicSubscriptionStream<S7SymbolicAlarm>(Session, notification => ProjectAlarms(notification, languageId), cancellationToken);
        try
        {
            var obj = SubscriptionObject(0, true);
            obj.Attributes[ReferenceList] = new(S7SymbolicDataType.UDInt, _initialAlarmReferences, 0x20);
            var reference = new PlusObject(0x51010001, AlarmReferenceClass);
            reference.Attributes[SubscriptionNameAttribute] = new(S7SymbolicDataType.WString, "IoTDriverAlarmReference");
            reference.Attributes[TriggerTransmitMode] = new(S7SymbolicDataType.USInt, TriggerAndTransmitMode);
            reference.Attributes[AlarmDomainFilter] = new(S7SymbolicDataType.UInt, new ushort[InitialCreditLimit], 0x10);
            reference.Attributes[AdditionalAlarmDomainFilter] = new(S7SymbolicDataType.UInt, _allAlarmDomains, 0x20);
            reference.Attributes[AlarmLanguages] = new(S7SymbolicDataType.UDInt, languageId == 0 ? Array.Empty<uint>() : new[] { languageId }, 0x20);
            reference.Attributes[SendAlarmTexts] = new(S7SymbolicDataType.Bool, true);
            reference.Relations[AlarmSubsystemRelation] = AlarmSubsystem;
            obj.Children.Add(reference);
            var result = await Session.CreateObjectAsync(Session.SubscriptionSessionId, obj, cancellationToken).ConfigureAwait(false);
            if (result.Length == 0 || result[0] == 0)
            {
                throw new InvalidDataException("Invalid alarm subscription identifier.");
            }

            stream.Activate(result[0]);
            return new(stream);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Handles the metadata.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="languageId">The language id.</param>
    /// <returns>The operation result.</returns>
    private static List<S7SymbolicAlarmMetadata> Metadata(IEnumerable<PlusObject> objects, uint languageId = 0)
    {
        var result = new List<S7SymbolicAlarmMetadata>();
        foreach (var obj in objects)
        {
            if (obj.Attributes.Count > 0)
            {
                result.Add(new(obj.Id, obj.ClassId, obj.Attributes, languageId));
            }

            result.AddRange(Metadata(obj.Children, languageId));
        }

        return result;
    }

    /// <summary>Handles the project alarms.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="languageId">The language id.</param>
    /// <returns>The operation result.</returns>
    private static IEnumerable<S7SymbolicAlarm> ProjectAlarms(PlusNotification notification, uint languageId)
    {
        foreach (var obj in notification.AlarmObjects)
        {
            yield return new(notification.Timestamp, notification.Sequence, new(obj.Id, obj.ClassId, obj.Attributes, languageId));
        }
    }

    /// <summary>Maps PLC item references to subscribed symbols.</summary>
    /// <param name="notification">The PLC notification.</param>
    /// <param name="symbols">The subscribed symbols.</param>
    /// <returns>The changes and access errors.</returns>
    private static IEnumerable<S7SymbolicChange> ProjectChanges(PlusNotification notification, S7Symbol[] symbols)
    {
        foreach (var value in notification.Values)
        {
            if (value.Key == 0 || value.Key > symbols.Length)
            {
                throw new InvalidDataException("Notification references an unknown item.");
            }

            var symbol = symbols[value.Key - 1];
            yield return new(symbol.Path, symbol.Address, value.Value, 0, notification.Timestamp, notification.Sequence);
        }

        foreach (var error in notification.Errors)
        {
            if (error.Key == 0 || error.Key > symbols.Length)
            {
                throw new InvalidDataException("Notification references an unknown item.");
            }

            var symbol = symbols[error.Key - 1];
            yield return new(symbol.Path, symbol.Address, null, error.Value, notification.Timestamp, notification.Sequence);
        }
    }

    /// <summary>Builds the PLC subscription reference array.</summary>
    /// <param name="symbols">The resolved symbols.</param>
    /// <returns>The subscription addresses.</returns>
    private static List<uint> BuildReferenceList(S7Symbol[] symbols)
    {
        var addresses = new List<uint>
        {
            0x80010000,
            0,
            (uint)symbols.Length
        };
        for (var i = 0; i < symbols.Length; i++)
        {
            var address = symbols[i].Address;
            var ids = address.LocalIds;
            addresses.Add(0x80040000 | (uint)(ids.Length + 1));
            addresses.Add((uint)i + 1);
            addresses.Add(0);
            addresses.Add(address.AccessArea);
            addresses.Add(address.SymbolCrc);
            addresses.Add(address.SubArea);
            addresses.AddRange(ids);
        }

        return addresses;
    }

    /// <summary>Handles the subscription object.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="alarm">The alarm.</param>
    /// <returns>The operation result.</returns>
    private PlusObject SubscriptionObject(uint cycle, bool alarm)
    {
        var relation = Interlocked.Increment(ref _subscriptionRelation);
        if (relation > 0x3ffe)
        {
            throw new InvalidOperationException("Subscription relation identifiers are exhausted for this client.");
        }

        var obj = new PlusObject(0x7fffc000 + (uint)relation, SubscriptionClass);
        obj.Attributes[SubscriptionNameAttribute] = new(S7SymbolicDataType.WString, $"IoTDriverSubscription_{relation}");
        obj.Attributes[SubscriptionFunctionClass] = new(S7SymbolicDataType.USInt, (byte)(alarm ? AlarmFunctionClass : 0));
        obj.Attributes[MissedSendings] = new(S7SymbolicDataType.UInt, (ushort)0);
        obj.Attributes[SubsystemError] = new(S7SymbolicDataType.LInt, 0L);
        obj.Attributes[RouteMode] = new(S7SymbolicDataType.USInt, (byte)(alarm ? AlarmRouteMode : 0x14));
        obj.Attributes[SubscriptionActive] = new(S7SymbolicDataType.Bool, true);
        obj.Attributes[CycleTime] = new(S7SymbolicDataType.UDInt, cycle);
        obj.Attributes[DelayTime] = new(S7SymbolicDataType.UDInt, 0U);
        obj.Attributes[SubscriptionDisabled] = new(S7SymbolicDataType.USInt, (byte)0);
        obj.Attributes[SubscriptionCount] = new(S7SymbolicDataType.USInt, (byte)0);
        obj.Attributes[CreditLimitAttribute] = new(S7SymbolicDataType.Int, (short)InitialCreditLimit);
        obj.Attributes[SubscriptionTicks] = new(S7SymbolicDataType.UInt, ushort.MaxValue);
        return obj;
    }

    /// <summary>Resolves every requested subscription path.</summary>
    /// <param name="paths">The requested paths.</param>
    /// <param name="cancellationToken">The request cancellation.</param>
    /// <returns>The resolved symbols.</returns>
    private async Task<S7Symbol[]> ResolveSubscriptionSymbolsAsync(string[] paths, CancellationToken cancellationToken)
    {
        var symbols = new S7Symbol[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
#if NETFRAMEWORK
            if (paths[i] is null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            if (string.IsNullOrWhiteSpace(paths[i]))
            {
                throw new ArgumentException("Subscription paths must contain nonempty names.", nameof(paths));
            }
#else
            ArgumentException.ThrowIfNullOrWhiteSpace(paths[i]);
#endif
            symbols[i] = await ResolveSymbolAsync(paths[i], cancellationToken).ConfigureAwait(false);
        }

        return symbols;
    }
}
