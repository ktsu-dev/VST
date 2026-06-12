// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.EffectsHost.Core;

/// <summary>
/// The host-independent real-time core of the plugin: it owns the <see cref="IAudioEffect"/>
/// instance and turns host-normalized parameter values into the plain-value snapshot the effect
/// processes with.
/// </summary>
/// <remarks>
/// Everything reachable from <see cref="Process"/> obeys the audio-thread contract: no
/// allocation, no locks, no blocking, no I/O. All state is allocated in the constructor and
/// <see cref="Prepare"/>. Keeping this class free of NPlug types lets the soak test drive the
/// exact production audio path without a plugin host.
/// </remarks>
public sealed class AudioEngine
{
	private readonly EffectParameter[] descriptors;
	private readonly double[] parameterValues;
	private readonly double[] lastHostNormalized;

	/// <summary>
	/// Initializes a new instance of the <see cref="AudioEngine"/> class around an effect.
	/// </summary>
	/// <param name="effect">The effect instance this engine drives.</param>
	public AudioEngine(IAudioEffect effect)
	{
		ArgumentNullException.ThrowIfNull(effect);

		Effect = effect;
		descriptors = [.. effect.Parameters];
		parameterValues = new double[descriptors.Length];
		lastHostNormalized = new double[descriptors.Length];
		SeedParameterDefaults();
	}

	/// <summary>Gets the effect this engine drives.</summary>
	public IAudioEffect Effect { get; }

	/// <summary>
	/// Prepares the effect for processing. Must be called from a non-real-time context before
	/// <see cref="Process"/>, and again whenever the sample rate or maximum block size changes.
	/// </summary>
	/// <param name="setup">The processing environment.</param>
	public void Prepare(in AudioEffectSetup setup)
	{
		Effect.Prepare(setup);
		SeedParameterDefaults();
	}

	/// <summary>
	/// Clears the effect's signal history without reallocating.
	/// </summary>
	public void Reset() => Effect.Reset();

	/// <summary>
	/// Processes one block of stereo audio on the audio thread.
	/// </summary>
	/// <param name="hostNormalizedValues">The host's current normalized parameter values, indexed like <see cref="IAudioEffect.Parameters"/>.</param>
	/// <param name="bypass">Whether the effect is bypassed; when <see langword="true"/> the input is copied to the output unchanged.</param>
	/// <param name="sampleRate">The sample rate, in hertz, of this block.</param>
	/// <param name="inputLeft">The left input channel.</param>
	/// <param name="inputRight">The right input channel.</param>
	/// <param name="outputLeft">The left output channel.</param>
	/// <param name="outputRight">The right output channel.</param>
	public void Process(
		ReadOnlySpan<double> hostNormalizedValues,
		bool bypass,
		double sampleRate,
		ReadOnlySpan<float> inputLeft,
		ReadOnlySpan<float> inputRight,
		Span<float> outputLeft,
		Span<float> outputRight)
	{
		// Fold host automation into the plain-value snapshot. Denormalizing only on change keeps
		// the steady-state cost at one comparison per parameter per block.
		for (int i = 0; i < descriptors.Length; i++)
		{
			double normalized = hostNormalizedValues[i];
			if (normalized != lastHostNormalized[i])
			{
				lastHostNormalized[i] = normalized;
				parameterValues[i] = descriptors[i].Range.Denormalize(normalized);
			}
		}

		if (bypass)
		{
			inputLeft.CopyTo(outputLeft);
			inputRight.CopyTo(outputRight);
			return;
		}

		Effect.ProcessBlock(new EffectBlock
		{
			SampleCount = inputLeft.Length,
			SampleRate = sampleRate,
			ParameterValues = parameterValues,
			InputLeft = inputLeft,
			InputRight = inputRight,
			OutputLeft = outputLeft,
			OutputRight = outputRight,
		});
	}

	private void SeedParameterDefaults()
	{
		for (int i = 0; i < descriptors.Length; i++)
		{
			parameterValues[i] = descriptors[i].DefaultValue;

			// NaN never compares equal, so the first block re-derives every plain value from the
			// host's actual normalized state.
			lastHostNormalized[i] = double.NaN;
		}
	}
}
