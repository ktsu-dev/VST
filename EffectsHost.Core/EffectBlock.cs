// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Core;

/// <summary>
/// One block of stereo audio handed to <see cref="IAudioEffect.ProcessBlock"/>, together with a
/// snapshot of the current parameter values.
/// </summary>
/// <remarks>
/// This is a <see langword="ref"/> struct wrapping spans over host-owned buffers, so it can be
/// constructed and passed on the audio thread without any allocation. Input and output spans may
/// alias the same memory (in-place processing); effects must read each input sample before writing
/// the corresponding output sample. <see cref="ParameterValues"/> holds plain (real-unit) values
/// indexed by the parameter's position in <see cref="IAudioEffect.Parameters"/>.
/// </remarks>
public readonly ref struct EffectBlock
{
	/// <summary>Gets the number of valid samples in each channel span.</summary>
	public required int SampleCount { get; init; }

	/// <summary>Gets the sample rate, in hertz, the block was produced at.</summary>
	public required double SampleRate { get; init; }

	/// <summary>Gets the plain parameter values, indexed by position in <see cref="IAudioEffect.Parameters"/>.</summary>
	public required ReadOnlySpan<double> ParameterValues { get; init; }

	/// <summary>Gets the left input channel.</summary>
	public required ReadOnlySpan<float> InputLeft { get; init; }

	/// <summary>Gets the right input channel.</summary>
	public required ReadOnlySpan<float> InputRight { get; init; }

	/// <summary>Gets the left output channel.</summary>
	public required Span<float> OutputLeft { get; init; }

	/// <summary>Gets the right output channel.</summary>
	public required Span<float> OutputRight { get; init; }
}
