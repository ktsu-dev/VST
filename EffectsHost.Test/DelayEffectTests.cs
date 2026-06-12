// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Core;
using ktsu.EffectsHost.Effects;

/// <summary>
/// DSP correctness tests for <see cref="DelayEffect"/>.
/// </summary>
[TestClass]
public sealed class DelayEffectTests
{
	private const double SampleRate = 48000.0;

	private static void Process(DelayEffect effect, double[] parameterValues, float[] input, float[] output) =>
		effect.ProcessBlock(new EffectBlock
		{
			SampleCount = input.Length,
			SampleRate = SampleRate,
			ParameterValues = parameterValues,
			InputLeft = input,
			InputRight = input,
			OutputLeft = output,
			OutputRight = output,
		});

	[TestMethod]
	public void ImpulseComesBackAfterTheConfiguredDelay()
	{
		DelayEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, 4096));

		// 10 ms at 48 kHz = 480 samples; full wet, no feedback, so the output is exactly the
		// delayed impulse.
		const double delayMs = 10.0;
		const int delaySamples = 480;
		double[] parameters = [delayMs, 0.0, 100.0];

		float[] input = new float[4096];
		input[0] = 1.0f;
		float[] output = new float[4096];
		Process(effect, parameters, input, output);

		int peakIndex = 0;
		for (int i = 1; i < output.Length; i++)
		{
			if (output[i] > output[peakIndex])
			{
				peakIndex = i;
			}
		}

		Assert.AreEqual(delaySamples, peakIndex, 1.0, "Echo did not arrive at the configured delay");
		Assert.AreEqual(1.0f, output[peakIndex], 0.05f);
	}

	[TestMethod]
	public void FeedbackProducesADecayingEchoTrain()
	{
		DelayEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, 4096));

		// 5 ms = 240 samples, 50% feedback, full wet.
		double[] parameters = [5.0, 50.0, 100.0];

		float[] input = new float[4096];
		input[0] = 1.0f;
		float[] output = new float[4096];
		Process(effect, parameters, input, output);

		float firstEcho = output[240];
		float secondEcho = output[480];
		float thirdEcho = output[720];

		Assert.AreEqual(1.0f, firstEcho, 0.05f);
		Assert.AreEqual(0.5f, secondEcho, 0.05f);
		Assert.AreEqual(0.25f, thirdEcho, 0.05f);
	}

	[TestMethod]
	public void FullyDrySettingPassesInputThrough()
	{
		DelayEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, 512));

		double[] parameters = [100.0, 0.0, 0.0];

		float[] input = new float[512];
		for (int i = 0; i < input.Length; i++)
		{
			input[i] = MathF.Sin(2.0f * MathF.PI * 220.0f * i / (float)SampleRate);
		}

		float[] output = new float[512];
		Process(effect, parameters, input, output);

		for (int i = 0; i < input.Length; i++)
		{
			Assert.AreEqual(input[i], output[i], 1e-5f);
		}
	}

	[TestMethod]
	public void ResetSilencesTheTail()
	{
		DelayEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, 512));

		double[] parameters = [5.0, 50.0, 100.0];

		float[] input = new float[512];
		input[0] = 1.0f;
		float[] output = new float[512];
		Process(effect, parameters, input, output);

		effect.Reset();
		Array.Clear(input);
		Process(effect, parameters, input, output);

		for (int i = 0; i < output.Length; i++)
		{
			Assert.AreEqual(0.0f, output[i], 1e-6f, $"Tail not silenced at sample {i}");
		}
	}
}
