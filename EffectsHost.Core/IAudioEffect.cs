// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Core;

/// <summary>
/// A single audio effect: the one interface a new DSP idea has to implement to become a plugin.
/// </summary>
/// <remarks>
/// The host shell turns <see cref="Parameters"/> into VST3 parameters, automation, state
/// persistence, and an editor panel, so an implementation only contains DSP code.
///
/// <para>
/// <b>Real-time contract.</b> <see cref="ProcessBlock"/> runs on the audio thread and must never
/// allocate, lock, block, or perform I/O. Anything whose size depends on the sample rate or block
/// size is allocated in <see cref="Prepare"/>, which runs before processing starts (and again on
/// configuration changes), never concurrently with <see cref="ProcessBlock"/>.
/// </para>
/// </remarks>
public interface IAudioEffect
{
	/// <summary>Gets the human-readable effect name shown by hosts.</summary>
	public string Name { get; }

	/// <summary>
	/// Gets the parameters this effect exposes. The list must be fixed for the lifetime of the
	/// instance; values are delivered per block via <see cref="EffectBlock.ParameterValues"/> in
	/// the same order.
	/// </summary>
	public IReadOnlyList<EffectParameter> Parameters { get; }

	/// <summary>
	/// Prepares the effect for processing: allocate buffers and derive coefficients here.
	/// </summary>
	/// <param name="setup">The processing environment.</param>
	public void Prepare(in AudioEffectSetup setup);

	/// <summary>
	/// Clears all internal signal history (delay lines, filter state, smoothers) without
	/// reallocating, returning the effect to silence.
	/// </summary>
	public void Reset();

	/// <summary>
	/// Processes one block of audio. Runs on the audio thread; must not allocate, lock, or block.
	/// </summary>
	/// <param name="block">The audio block and parameter snapshot to process.</param>
	public void ProcessBlock(in EffectBlock block);
}
