// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.EffectsHost.Effects;

using ktsu.EffectsHost.Core;
using ktsu.Semantics;

/// <summary>
/// A stereo gain/trim effect: the simplest possible <see cref="IAudioEffect"/>, used to validate
/// the host shell end to end.
/// </summary>
/// <remarks>
/// The single parameter is a level in decibels. The linear gain is smoothed with a one-pole ramp
/// towards its target so that parameter jumps (automation steps, coarse host quantisation) do not
/// produce zipper noise. The processing path performs no allocation, locking, or I/O.
/// </remarks>
public sealed class GainEffect : IAudioEffect
{
	/// <summary>The index of the gain parameter in <see cref="Parameters"/>.</summary>
	public const int GainParameterIndex = 0;

	/// <summary>The time, in seconds, the gain smoother takes to cover ~63% of a step.</summary>
	private const double SmoothingTimeSeconds = 0.010;

	private static readonly EffectParameter[] ParameterDescriptors =
	[
		EffectParameter.FromDecibels(
			"Gain",
			Decibels<double>.Create(-60.0),
			Decibels<double>.Create(12.0),
			Decibels<double>.Unity),
	];

	private float currentGain = 1.0f;
	private float smoothingCoefficient;
	private bool snapGainToTarget = true;

	/// <inheritdoc/>
	public string Name => "Gain";

	/// <inheritdoc/>
	public IReadOnlyList<EffectParameter> Parameters => ParameterDescriptors;

	/// <inheritdoc/>
	public void Prepare(in AudioEffectSetup setup)
	{
		// One-pole coefficient for the configured time constant at this sample rate.
		smoothingCoefficient = (float)(1.0 - Math.Exp(-1.0 / (SmoothingTimeSeconds * setup.SampleRate)));
		Reset();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Gliding from a stale or hard-coded level after a reset would play the signal at that level
	/// until the smoother caught up, so the next block starts at its target gain instead.
	/// </remarks>
	public void Reset() => snapGainToTarget = true;

	/// <inheritdoc/>
	public void ProcessBlock(in EffectBlock block)
	{
		float targetGain = (float)Decibels<double>.Create(block.ParameterValues[GainParameterIndex]).ToAmplitude().Value;
		if (snapGainToTarget)
		{
			currentGain = targetGain;
			snapGainToTarget = false;
		}

		float gain = currentGain;
		float coefficient = smoothingCoefficient;

		for (int sample = 0; sample < block.SampleCount; sample++)
		{
			gain += (targetGain - gain) * coefficient;
			block.OutputLeft[sample] = block.InputLeft[sample] * gain;
			block.OutputRight[sample] = block.InputRight[sample] * gain;
		}

		currentGain = gain;
	}
}
