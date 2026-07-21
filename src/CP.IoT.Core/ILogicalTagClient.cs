// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Combines read, write, and observation capabilities for logical PLC tags.</summary>
public interface ILogicalTagClient : ILogicalTagReader, ILogicalTagWriter, ILogicalTagObserver;
