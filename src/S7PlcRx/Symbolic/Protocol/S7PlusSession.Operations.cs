// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Coordinates S7 plus session.</summary>
internal sealed partial class S7PlusSession
{
    /// <summary>Handles Explore async.</summary>
    /// <param name = "id">The id.</param>
    /// <param name = "recursive">The recursive.</param>
    /// <param name = "attributes">The attributes.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task<PlusObject[]> ExploreAsync(uint id, bool recursive, uint[] attributes, CancellationToken cancellationToken)
    {
        try
        {
            RequireNotNull(attributes, nameof(attributes));
            var response = await ExchangeAsync(
                OrdinaryVersion,
                0x04bb,
                0x34,
                false,
                true,
                integrity =>
            {
                var writer = new PlusWriter();
                writer.UInt32(id);
                writer.VarUInt32(0);
                writer.Byte(recursive ? (byte)1 : (byte)0);
                writer.Byte(1);
                writer.Byte(0);
                writer.Byte(0);
                writer.VarUInt32((uint)attributes.Length);
                foreach (var attribute in attributes)
                {
                    writer.VarUInt32(attribute);
                }

                writer.VarUInt32(integrity);
                writer.Bytes(ExplorationPadding);
                return writer.ToArray();
            },
                cancellationToken).ConfigureAwait(false);
            CheckReturn(response.Body);

            // The controller returns its canonical exploration scope.
            _ = response.Body.UInt32();

            CheckIntegrity(response);
            var objects = new List<PlusObject>();
            while (response.Body.Remaining > PaddingLength)
            {
                objects.Add(PlusObject.ReadFrom(response.Body));
            }

            return objects.ToArray();
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Read async.</summary>
    /// <param name = "addresses">The addresses.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task<S7SymbolicReadResult[]> ReadAsync(S7SymbolicAddress[] addresses, CancellationToken cancellationToken)
    {
        try
        {
            RequireNotNull(addresses, nameof(addresses));
            var results = new S7SymbolicReadResult[addresses.Length];
            for (var offset = 0; offset < addresses.Length; offset += ReadLimit)
            {
                var count = Math.Min(ReadLimit, addresses.Length - offset);
                var response = await ExchangeAsync(
                    OrdinaryVersion,
                    0x054c,
                    0x34,
                    false,
                    true,
                    integrity =>
                {
                    var writer = new PlusWriter();
                    writer.UInt32(0);
                    WriteAddresses(writer, addresses, offset, count);
                    Qualifier(writer);
                    writer.VarUInt32(integrity);
                    writer.UInt32(0);
                    return writer.ToArray();
                },
                    cancellationToken).ConfigureAwait(false);
                CheckReturn(response.Body);
                var seen = new bool[count];
                uint index;
                while ((index = response.Body.VarUInt32()) != 0)
                {
                    ValidateIndex(index, count, seen);
                    results[offset + (int)index - 1] = new(addresses[offset + (int)index - 1], S7SymbolicValue.ReadFrom(response.Body), 0);
                }

                while ((index = response.Body.VarUInt32()) != 0)
                {
                    ValidateIndex(index, count, seen);
                    results[offset + (int)index - 1] = new(addresses[offset + (int)index - 1], null, response.Body.VarUInt64());
                }

                CheckIntegrity(response);
                if (Array.Exists(
                    seen,
                    static item => !item))
                {
                    throw new InvalidDataException("Read response omitted a requested value.");
                }
            }

            return results;
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Write async.</summary>
    /// <param name = "items">The items.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task<S7SymbolicWriteResult[]> WriteAsync(S7SymbolicWriteItem[] items, CancellationToken cancellationToken)
    {
        try
        {
            RequireNotNull(items, nameof(items));
            var addresses = new S7SymbolicAddress[items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                addresses[index] = items[index].Address;
            }

            var results = new S7SymbolicWriteResult[items.Length];
            for (var offset = 0; offset < items.Length; offset += WriteLimit)
            {
                var count = Math.Min(WriteLimit, items.Length - offset);
                var response = await ExchangeAsync(
                    OrdinaryVersion,
                    0x0542,
                    0x34,
                    true,
                    true,
                    integrity =>
                {
                    var writer = new PlusWriter();
                    writer.UInt32(0);
                    WriteAddresses(writer, addresses, offset, count);
                    for (var item = 0; item < count; item++)
                    {
                        writer.VarUInt32((uint)item + 1);
                        items[offset + item].Value.WriteTo(writer);
                    }

                    writer.Byte(0);
                    Qualifier(writer);
                    writer.VarUInt32(integrity);
                    writer.UInt32(0);
                    return writer.ToArray();
                },
                    cancellationToken).ConfigureAwait(false);
                CheckReturn(response.Body);
                for (var item = 0; item < count; item++)
                {
                    results[offset + item] = new(addresses[offset + item], 0);
                }

                var seen = new bool[count];
                uint index;
                while ((index = response.Body.VarUInt32()) != 0)
                {
                    ValidateIndex(index, count, seen);
                    results[offset + (int)index - 1] = new(addresses[offset + (int)index - 1], response.Body.VarUInt64());
                }

                CheckIntegrity(response);
            }

            return results;
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Get attribute async.</summary>
    /// <param name = "objectId">The objectId.</param>
    /// <param name = "attributeId">The attributeId.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal Task<S7SymbolicValue> GetAttributeAsync(uint objectId, uint attributeId, CancellationToken cancellationToken) => GetAttributeCoreAsync(objectId, attributeId, true, cancellationToken);

    /// <summary>Handles Set attribute async.</summary>
    /// <param name = "objectId">The objectId.</param>
    /// <param name = "attributeId">The attributeId.</param>
    /// <param name = "value">The value.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal Task SetAttributeAsync(uint objectId, uint attributeId, S7SymbolicValue value, CancellationToken cancellationToken) =>
        SetAttributeCoreAsync(objectId, attributeId, value, true, cancellationToken);

    /// <summary>Handles Create object async.</summary>
    /// <param name = "parentId">The parentId.</param>
    /// <param name = "obj">The obj.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task<uint[]> CreateObjectAsync(uint parentId, PlusObject obj, CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExchangeAsync(
                OrdinaryVersion,
                0x04ca,
                0x34,
                true,
                true,
                integrity => CreateBody(parentId, obj, integrity, true),
                cancellationToken).ConfigureAwait(false);
            var errorCode = response.Body.VarUInt64();
            var ids = ReadObjectIds(response.Body);
            CheckIntegrity(response);
            if (errorCode != 0)
            {
                var failure = new S7SymbolicException($"Controller rejected object creation with error 0x{errorCode:X}.", errorCode);
                try
                {
                    foreach (var id in ids)
                    {
                        if (id != 0)
                        {
                            await DeleteObjectAsync(id, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Object creation and allocation cleanup both failed.", failure, cleanupFailure);
                }

                throw failure;
            }

            while (response.Body.Remaining > PaddingLength)
            {
                _ = PlusObject.ReadFrom(response.Body);
            }

            if (response.Body.Remaining != PaddingLength || response.Body.UInt32() != 0)
            {
                throw new InvalidDataException("Invalid object creation response padding.");
            }

            return ids;
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Delete object async.</summary>
    /// <param name = "objectId">The objectId.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task DeleteObjectAsync(uint objectId, CancellationToken cancellationToken)
    {
        try
        {
            var deletingSession = objectId == SessionId;
            var response = await ExchangeAsync(
                OrdinaryVersion,
                0x04d4,
                0x34,
                true,
                true,
                integrity =>
            {
                var writer = new PlusWriter();
                writer.UInt32(objectId);
                writer.Byte(0);
                Qualifier(writer);
                writer.VarUInt32(integrity);
                writer.UInt32(0);
                return writer.ToArray();
            },
                cancellationToken).ConfigureAwait(false);
            if (deletingSession)
            {
                // Deleting the current session can report its already-terminated state.
                _ = response.Body.VarUInt64();
            }
            else
            {
                CheckReturn(response.Body);
            }

            if (response.Body.UInt32() != objectId)
            {
                throw new InvalidDataException("Object deletion response identifies a different object.");
            }

            if (deletingSession)
            {
                return;
            }

            CheckIntegrity(response);
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Get attribute core async.</summary>
    /// <param name = "objectId">The objectId.</param>
    /// <param name = "attributeId">The attributeId.</param>
    /// <param name = "withIntegrity">The withIntegrity.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task<S7SymbolicValue> GetAttributeCoreAsync(uint objectId, uint attributeId, bool withIntegrity, CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExchangeAsync(
                OrdinaryVersion,
                0x0586,
                0x34,
                false,
                withIntegrity,
                integrity =>
            {
                var writer = new PlusWriter();
                writer.UInt32(objectId);
                writer.Bytes(AttributeDescriptor);
                writer.VarUInt32(attributeId);
                Qualifier(writer);
                writer.UInt16(1);
                if (withIntegrity)
                {
                    writer.VarUInt32(integrity);
                }

                writer.UInt32(0);
                return writer.ToArray();
            },
                cancellationToken).ConfigureAwait(false);
            CheckReturn(response.Body);
            _ = response.Body.Byte();
            var value = S7SymbolicValue.ReadFrom(response.Body);
            CheckIntegrity(response);
            return value;
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }

    /// <summary>Handles Set attribute core async.</summary>
    /// <param name = "objectId">The objectId.</param>
    /// <param name = "attributeId">The attributeId.</param>
    /// <param name = "value">The value.</param>
    /// <param name = "withIntegrity">The withIntegrity.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task SetAttributeCoreAsync(uint objectId, uint attributeId, S7SymbolicValue value, bool withIntegrity, CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExchangeAsync(
                OrdinaryVersion,
                0x04f2,
                0x34,
                true,
                withIntegrity,
                integrity =>
            {
                var writer = new PlusWriter();
                writer.UInt32(objectId);
                writer.VarUInt32(1);
                writer.VarUInt32(attributeId);
                value.WriteTo(writer);
                Qualifier(writer);
                writer.Byte(0);
                if (withIntegrity)
                {
                    writer.VarUInt32(integrity);
                }

                writer.UInt32(0);
                return writer.ToArray();
            },
                cancellationToken).ConfigureAwait(false);
            CheckReturn(response.Body);
            CheckIntegrity(response);
        }
        catch (InvalidDataException exception)
        {
            FailConnection(exception);
            throw;
        }
    }
}
