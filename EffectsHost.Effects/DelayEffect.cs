// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Effects;

using ktsu.Containers;
using ktsu.EffectsHost.Core;
using ktsu.Semantics;

/// <summary>
/// A stereo feedback delay built on the denormal-aware <see cref="DelayLine"/> from
/// <c>ktsu.Containers</c>.
/// </summary>
/// <remarks>
/// The delay time rides a one-pole smoother and is read with linear interpolation, so time changes
/// glide instead of clicking. Feedback decay tails cannot cause denormal CPU spikes because the
/// delay line flushes denormal magnitudes to zero on write. The processing path performs no
/// allocation, locking, or I/O; both delay lines are sized for <see cref="MaxDelayMilliseconds"/>
/// in <see cref="Prepare"/>.
/// </remarks>
public sealed class DelayEffect : IAudioEffect
{
	/// <summary>The index of the delay-time parameter in <see cref="Parameters"/>.</summary>
	public const int TimeParameterIndex = 0;

	/// <summary>The index of the feedback parameter in <see cref="Parameters"/>.</summary>
	public const int FeedbackParameterIndex = 1;

	/// <summary>The index of the dry/wet mix parameter in <see cref="Parameters"/>.</summary>
	public const int MixParameterIndex = 2;

	/// <summary>The longest supported delay time, in milliseconds.</summary>
	public const double MaxDelayMilliseconds = 2000.0;

	/// <summary>The time, in seconds, the delay-time smoother takes to cover ~63% of a step.</summary>
	private const double SmoothingTimeSeconds = 0.050;

	private static readonly EffectParameter[] ParameterDescriptors =
	[
		EffectParameter.Logarithmic("Time", 1.0, MaxDelayMilliseconds, 250.0, "ms"),
		EffectParameter.FromPercent("Feedback", Percent<double>.Create(0.0), Percent<double>.Create(95.0), Percent<double>.Create(35.0)),
		EffectParameter.FromPercent("Mix", Percent<double>.Create(0.0), Percent<double>.Create(100.0), Percent<double>.Create(50.0)),
	];

	private DelayLine? delayLeft;
	private DelayLine? delayRight;
	private double sampleRate = 48000.0;
	private float smoothingCoefficient;
	private float currentDelaySamples;
	private bool snapDelayToTarget;

	/// <inheritdoc/>
	public string Name => "Delay";

	/// <inheritdoc/>
	public IReadOnlyList<EffectParameter> Parameters => ParameterDescriptors;

	/// <inheritdoc/>
	public void Prepare(in AudioEffectSetup setup)
	{
		sampleRate = setup.SampleRate;
		int maxDelaySamples = (int)Math.Ceiling(MaxDelayMilliseconds / 1000.0 * sampleRate) + 1;
		delayLeft = new DelayLine(maxDelaySamples);
		delayRight = new DelayLine(maxDelaySamples);
		smoothingCoefficient = (float)(1.0 - Math.Exp(-1.0 / (SmoothingTimeSeconds * sampleRate)));
		currentDelaySamples = ToDelaySamples(ParameterDescriptors[TimeParameterIndex].DefaultValue);
		Reset();
	}

	/// <inheritdoc/>
	public void Reset()
	{
		delayLeft?.Clear();
		delayRight?.Clear();

		// After a reset there is no audible tail to protect, so the next block jumps straight to
		// its target time instead of gliding from stale state.
		snapDelayToTarget = true;
	}

	/// <inheritdoc/>
	public void ProcessBlock(in EffectBlock block)
	{
		if (delayLeft is not DelayLine left || delayRight is not DelayLine right)
		{
			return;
		}

		float targetDelaySamples = ToDelaySamples(block.ParameterValues[TimeParameterIndex]);
		if (snapDelayToTarget)
		{
			currentDelaySamples = targetDelaySamples;
			snapDelayToTarget = false;
		}

		float feedback = (float)(block.ParameterValues[FeedbackParameterIndex] / 100.0);
		float mix = (float)(block.ParameterValues[MixParameterIndex] / 100.0);
		float dryLevel = 1.0f - mix;

		float delaySamples = currentDelaySamples;
		float coefficient = smoothingCoefficient;
		int capacity = left.Capacity;

		for (int sample = 0; sample < block.SampleCount; sample++)
		{
			delaySamples += (targetDelaySamples - delaySamples) * coefficient;

			// The most recently written sample has age 0, so an effective delay of D samples
			// between input and output reads back at age D - 1 before writing.
			float readAge = Math.Clamp(delaySamples - 1.0f, 0.0f, capacity);

			float dryLeft = block.InputLeft[sample];
			float dryRight = block.InputRight[sample];

			float wetLeft = left.ReadInterpolated(readAge);
			float wetRight = right.ReadInterpolated(readAge);

			left.Write(dryLeft + (wetLeft * feedback));
			right.Write(dryRight + (wetRight * feedback));

			block.OutputLeft[sample] = (dryLeft * dryLevel) + (wetLeft * mix);
			block.OutputRight[sample] = (dryRight * dryLevel) + (wetRight * mix);
		}

		currentDelaySamples = delaySamples;
	}

	private float ToDelaySamples(double delayMilliseconds) =>
		(float)Math.Clamp(delayMilliseconds / 1000.0 * sampleRate, 1.0, delayLeft?.Capacity ?? 1.0);
}
