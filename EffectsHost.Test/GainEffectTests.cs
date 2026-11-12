// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Core;
using ktsu.EffectsHost.Effects;

/// <summary>
/// DSP correctness tests for <see cref="GainEffect"/>.
/// </summary>
[TestClass]
public sealed class GainEffectTests
{
	private const double SampleRate = 48000.0;
	private const int BlockSize = 512;

	private static void ProcessBlocks(GainEffect effect, double gainDb, float[] input, float[] output, int blockCount)
	{
		double[] parameterValues = [gainDb];
		for (int i = 0; i < blockCount; i++)
		{
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
		}
	}

	[TestMethod]
	public void UnityGainPassesSignalThrough()
	{
		GainEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		float[] output = new float[BlockSize];
		for (int i = 0; i < input.Length; i++)
		{
			input[i] = MathF.Sin(2.0f * MathF.PI * 440.0f * i / (float)SampleRate);
		}

		// Run a few blocks so the smoother has fully settled at unity.
		ProcessBlocks(effect, gainDb: 0.0, input, output, blockCount: 10);

		for (int i = 0; i < input.Length; i++)
		{
			Assert.AreEqual(input[i], output[i], 1e-5f, $"Sample {i} differs at unity gain");
		}
	}

	[TestMethod]
	public void MinusSixDecibelsRoughlyHalvesAmplitude()
	{
		GainEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		// Enough blocks for the 10 ms smoother to converge to the target.
		ProcessBlocks(effect, gainDb: -6.0, input, output, blockCount: 50);

		float expected = MathF.Pow(10.0f, -6.0f / 20.0f);
		Assert.AreEqual(expected, output[^1], 1e-4f);
	}

	[TestMethod]
	public void GainChangesAreSmoothedNotStepped()
	{
		GainEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		// Settle at unity, then jump the target down to -60 dB.
		ProcessBlocks(effect, gainDb: 0.0, input, output, blockCount: 50);
		ProcessBlocks(effect, gainDb: -60.0, input, output, blockCount: 1);

		// The first sample after the jump must still be near unity (no zipper step) and the block
		// must move monotonically towards the new target.
		Assert.IsGreaterThan(0.9f, output[0], "Gain stepped instead of ramping");
		for (int i = 1; i < output.Length; i++)
		{
			Assert.IsLessThanOrEqualTo(output[i - 1], output[i], $"Ramp not monotonic at sample {i}");
		}
	}

	[TestMethod]
	public void ResetSnapsGainToTarget()
	{
		GainEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		ProcessBlocks(effect, gainDb: -60.0, input, output, blockCount: 50);
		effect.Reset();
		ProcessBlocks(effect, gainDb: 0.0, input, output, blockCount: 1);

		Assert.AreEqual(1.0f, output[0], 1e-5f);
	}

	[TestMethod]
	public void FirstBlockAfterPrepareStartsAtTheTargetGain()
	{
		GainEffect effect = new();
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		ProcessBlocks(effect, gainDb: -60.0, input, output, blockCount: 1);

		// -60 dB is 0.001. Gliding down from unity would start near 1.0.
		for (int i = 0; i < output.Length; i++)
		{
			Assert.AreEqual(0.001f, output[i], 1e-6f, $"Sample {i} is not at the target gain");
		}
	}

	[TestMethod]
	public void ReactivatingAtANonUnityTargetPlaysNoBurst()
	{
		GainEffect effect = new();
		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		// Settle at +12 dB, then re-activate (as a host does on transport start or a sample-rate
		// change) with the gain at -60 dB.
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));
		ProcessBlocks(effect, gainDb: 12.0, input, output, blockCount: 50);
		effect.Prepare(new AudioEffectSetup(SampleRate, BlockSize));
		ProcessBlocks(effect, gainDb: -60.0, input, output, blockCount: 1);

		Assert.AreEqual(0.001f, output[0], 1e-6f);

		// Settle at -60 dB, then reset with the gain at -20 dB.
		ProcessBlocks(effect, gainDb: -60.0, input, output, blockCount: 50);
		effect.Reset();
		ProcessBlocks(effect, gainDb: -20.0, input, output, blockCount: 1);

		Assert.AreEqual(0.1f, output[0], 1e-6f);
	}
}
