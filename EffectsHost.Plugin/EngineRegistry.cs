// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Collections.Concurrent;

/// <summary>
/// A process-wide registry pairing each processor's <see cref="AudioEngine"/> with its controller.
/// </summary>
/// <remarks>
/// VST3 deliberately separates the processor and controller, so the controller cannot reach the
/// processor instance directly. The processor registers its engine here and sends the resulting id
/// over the host's connection-point message channel; the controller looks the engine up from the
/// id. This direct reference is only valid because NPlug runs both components in one process.
/// </remarks>
internal static class EngineRegistry
{
	/// <summary>The connection message id carrying the engine registration id to the controller.</summary>
	internal const string BridgeMessageId = "ktsu.EffectsHost.EngineBridge";

	/// <summary>The attribute id of the engine registration id within the bridge message.</summary>
	internal const string EngineIdAttribute = "EngineId";

	private static readonly ConcurrentDictionary<long, AudioEngine> Engines = new();
	private static long nextId;

	/// <summary>
	/// Registers an engine and returns the id to send to the paired controller.
	/// </summary>
	/// <param name="engine">The engine to register.</param>
	/// <returns>The registration id.</returns>
	internal static long Register(AudioEngine engine)
	{
		long id = Interlocked.Increment(ref nextId);
		Engines[id] = engine;
		return id;
	}

	/// <summary>
	/// Resolves a previously registered engine.
	/// </summary>
	/// <param name="id">The registration id received from the processor.</param>
	/// <param name="engine">The engine, when found.</param>
	/// <returns><see langword="true"/> when the id is registered.</returns>
	internal static bool TryGet(long id, out AudioEngine? engine) => Engines.TryGetValue(id, out engine);

	/// <summary>
	/// Removes an engine registration.
	/// </summary>
	/// <param name="id">The registration id to remove.</param>
	internal static void Unregister(long id) => Engines.TryRemove(id, out _);
}
