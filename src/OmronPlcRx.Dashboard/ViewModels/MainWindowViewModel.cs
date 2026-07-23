// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace IoT.DriverCore.OmronPlcRx.Dashboard.ViewModels;

/// <summary>Main window view model coordinating connection and tags.</summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    /// <summary>Stores the connection stabilization delay.</summary>
    private const int ConnectionStabilizationDelayMilliseconds = 1_000;

    /// <summary>Stores the editable tag definitions.</summary>
    private readonly ObservableCollection<TagDefinition> _tags = [];

    /// <summary>Tracks active subscriptions for disposal.</summary>
    private readonly MultipleDisposable _disposables = new();

    /// <summary>Stores the current PLC connection.</summary>
    private OmronPlcRx? _plc;

    /// <summary>Initializes a new instance of the <see cref="MainWindowViewModel"/> class.</summary>
    public MainWindowViewModel()
    {
        Tags = new(_tags);
        ConnectionMethods = Enum.GetValues<ConnectionMethod>();
        SerialProtocols = Enum.GetValues<OmronSerialProtocol>();
        SerialParities = Enum.GetValues<Parity>();
        SerialStopBits = Enum.GetValues<StopBits>();
        SerialHandshakes = Enum.GetValues<Handshake>();
        SerialFrameModes = Enum.GetValues<OmronHostLinkFinsFrameMode>();

        var canConnect = this.WhenAnyValue(v => v.IsConnected).Select(static isConnected => !isConnected);
        var canDisconnect = this.WhenAnyValue(v => v.IsConnected);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnect);
        AddTagCommand = ReactiveCommand.CreateFromTask(
            AddTagAsync,
            this.WhenAnyValue(v => v.IsConnected));
        WriteTagCommand = ReactiveCommand.CreateFromTask<TagDefinition>(
            WriteTagAsync,
            this.WhenAnyValue(v => v.IsConnected));
    }

    /// <summary>Gets the connection methods value.</summary>
    public Array ConnectionMethods { get; }

    /// <summary>Gets the serial protocols value.</summary>
    public Array SerialProtocols { get; }

    /// <summary>Gets the serial parities value.</summary>
    public Array SerialParities { get; }

    /// <summary>Gets the serial stop bits value.</summary>
    public Array SerialStopBits { get; }

    /// <summary>Gets the serial handshakes value.</summary>
    public Array SerialHandshakes { get; }

    /// <summary>Gets the serial frame modes value.</summary>
    public Array SerialFrameModes { get; }

    /// <summary>Gets connection settings.</summary>
    public ConnectionSettings Settings { get; } = new();

    /// <summary>Gets the tags value.</summary>
    public ReadOnlyObservableCollection<TagDefinition> Tags { get; }

    /// <summary>Gets the is connected value.</summary>
    public bool IsConnected
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the status value.</summary>
    public string Status
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Idle";

    /// <summary>Gets the plc type value.</summary>
    public PlcType? PlcType
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the controller model value.</summary>
    public string? ControllerModel
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the controller version value.</summary>
    public string? ControllerVersion
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the connect command value.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ConnectCommand { get; }

    /// <summary>Gets the disconnect command value.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DisconnectCommand { get; }

    /// <summary>Gets the add tag command value.</summary>
    public ReactiveCommand<RxVoid, RxVoid> AddTagCommand { get; }

    /// <summary>Gets the write tag command value.</summary>
    public ReactiveCommand<TagDefinition, RxVoid> WriteTagCommand { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Disconnect();
        _disposables.Dispose();
    }

    /// <summary>Connects to the configured PLC.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ConnectAsync()
    {
        Disconnect();
        try
        {
            _plc = Settings.Method == ConnectionMethod.Serial
                ? new OmronPlcRx(
                    Settings.LocalNodeId,
                    Settings.RemoteNodeId,
                    Settings.ToSerialOptions(),
                    Settings.Timeout,
                    Settings.Retries,
                    TimeSpan.FromMilliseconds(Settings.PollMs))
                : new OmronPlcRx(
                    new OmronConnectionOptions(
                        Settings.LocalNodeId,
                        Settings.RemoteNodeId,
                        Settings.Method,
                        Settings.Host)
                    {
                        Port = Settings.Port,
                        Timeout = Settings.Timeout,
                        Retries = Settings.Retries,
                    },
                    TimeSpan.FromMilliseconds(Settings.PollMs));
            _ = _plc.Errors
                .SubscribeSafe(
                    error => Status = error?.Message ?? string.Empty,
                    error => Status = error.Message)
                .DisposeWith(_disposables);
            IsConnected = true;
            await Task
                .Delay(ConnectionStabilizationDelayMilliseconds)
                .ConfigureAwait(true);
            PlcType = _plc.PlcType;
            ControllerModel = _plc.ControllerModel;
            ControllerVersion = _plc.ControllerVersion;
            Status = Settings.Method == ConnectionMethod.Serial
                ? $"Connected via serial {Settings.SerialPortName} ({Settings.SerialProtocol})"
                : "Connected";
        }
        catch (Exception ex)
        {
            Status = $"Connect failed: {ex.Message}";
        }
    }

    /// <summary>Disconnects the current PLC connection and clears state.</summary>
    private void Disconnect()
    {
        _plc?.Dispose();
        _plc = null;
        IsConnected = false;
        PlcType = null;
        ControllerModel = null;
        ControllerVersion = null;
        _tags.Clear();
        Status = "Disconnected";
    }

    /// <summary>Adds a tag definition through the dashboard dialog.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task AddTagAsync()
    {
        if (_plc is null)
        {
            return;
        }

        Type[] allowed =
        [
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(float),
            typeof(double),
            typeof(string),
        ];
        var vm = new AddTagViewModel(allowed);
        var dlg = new Views.AddTagDialog { DataContext = vm };
        if (dlg.ShowDialog() == true)
        {
            var tagDef = new TagDefinition(vm.Name.Trim(), vm.Address.Trim(), vm.SelectedType);
            _tags.Add(tagDef);
            RegisterTag(tagDef);
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    /// <summary>Registers a tag with the PLC wrapper and subscribes for updates.</summary>
    /// <param name="tag">The tag definition.</param>
    private void RegisterTag(TagDefinition tag)
    {
        if (_plc is null)
        {
            return;
        }

        try
        {
            var valueType = tag.ValueType;
            var plcTagType = typeof(PlcTag<>).MakeGenericType(valueType);
            var plcTag = Activator.CreateInstance(plcTagType, tag.Name, tag.Address);
            _ = GetGenericPlcMethod(
                    nameof(IOmronPlcRx.AddUpdateTagItem),
                    valueType)
                .Invoke(_plc, [plcTag]);

            var tagKey = TagKeyFactory.Create(valueType, tag.Name);
            var observable = GetGenericPlcMethod(
                    nameof(IOmronPlcRx.Observe),
                    valueType)
                .Invoke(_plc, [tagKey]);
            if (observable is not null)
            {
                SubscribeGeneric(tag, valueType, observable);
            }
        }
        catch (Exception ex)
        {
            Status = $"Add tag failed: {ex.Message}";
        }
    }

    /// <summary>Subscribes a tag to the reflected observable returned by the PLC wrapper.</summary>
    /// <param name="tag">The tag definition to update.</param>
    /// <param name="valueType">The tag value type.</param>
    /// <param name="observable">The reflected observable instance.</param>
    private void SubscribeGeneric(TagDefinition tag, Type valueType, object observable)
    {
        switch (Type.GetTypeCode(valueType))
        {
            case TypeCode.Boolean:
            {
                SubscribeCore<bool>(tag, observable);
                break;
            }

            case TypeCode.Byte:
            {
                SubscribeCore<byte>(tag, observable);
                break;
            }

            case TypeCode.Int16:
            {
                SubscribeCore<short>(tag, observable);
                break;
            }

            case TypeCode.UInt16:
            {
                SubscribeCore<ushort>(tag, observable);
                break;
            }

            case TypeCode.Int32:
            {
                SubscribeCore<int>(tag, observable);
                break;
            }

            case TypeCode.UInt32:
            {
                SubscribeCore<uint>(tag, observable);
                break;
            }

            case TypeCode.Single:
            {
                SubscribeCore<float>(tag, observable);
                break;
            }

            case TypeCode.Double:
            {
                SubscribeCore<double>(tag, observable);
                break;
            }

            case TypeCode.String:
            {
                SubscribeCore<string>(tag, observable);
                break;
            }

            default:
                return;
        }
    }

    /// <summary>Gets a closed generic PLC method.</summary>
    /// <param name="methodName">Method name.</param>
    /// <param name="valueType">Generic tag value type.</param>
    /// <returns>The closed generic method.</returns>
    private System.Reflection.MethodInfo GetGenericPlcMethod(
        string methodName,
        Type valueType)
    {
        var plc = _plc
            ?? throw new InvalidOperationException("The PLC is not connected.");
        return plc
            .GetType()
            .GetMethods()
            .Single(method =>
                method.Name == methodName
                && method.IsGenericMethodDefinition)
            .MakeGenericMethod(valueType);
    }

    /// <summary>Subscribes a strongly typed observable to a dashboard tag.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="tag">The tag definition to update.</param>
    /// <param name="observable">The reflected observable instance.</param>
    private void SubscribeCore<T>(TagDefinition tag, object observable)
    {
        if (observable is not IObservable<T?> source)
        {
            return;
        }

        _ = source
            .SubscribeSafe(
                value => tag.Value = value is null ? null : (object)value,
                error => Status = error.Message)
            .DisposeWith(_disposables);
    }

    /// <summary>Writes a tag value to the PLC wrapper.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task WriteTagAsync(TagDefinition? tag)
    {
        if (tag is null || _plc is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var valueType = tag.ValueType;
            var method = GetGenericPlcMethod(
                nameof(IOmronPlcRx.SetValue),
                valueType);
            var converted = TypeDescriptor
                .GetConverter(valueType)
                .ConvertFromString(tag.Value?.ToString() ?? string.Empty);
            _ = method.Invoke(
                _plc,
                [TagKeyFactory.Create(valueType, tag.Name), converted]);
        }
        catch (Exception ex)
        {
            Status = $"Write failed: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    /// <summary>Creates typed logical tag keys for reflected operations.</summary>
    private static class TagKeyFactory
    {
        /// <summary>Creates a typed logical tag key.</summary>
        /// <param name="valueType">Tag value type.</param>
        /// <param name="tagName">Logical tag name.</param>
        /// <returns>The typed logical tag key.</returns>
        internal static object? Create(Type valueType, string tagName)
        {
            var tagKeyType = typeof(LogicalTagKey<>).MakeGenericType(valueType);
            return Activator.CreateInstance(tagKeyType, tagName);
        }
    }
}
