// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Combines the common read, write, observe, and logical-tag setup surfaces.</summary>
public interface IManagedLogicalTagClient : ILogicalTagClient, ILogicalTagSetup;
