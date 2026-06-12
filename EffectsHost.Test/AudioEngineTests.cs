// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Core;
using ktsu.EffectsHost.Effects;
using ktsu.EffectsHost.Plugin;

/// <summary>
/// Tests for the real-time <see cref="AudioEngine"/>: parameter folding, the UI → audio mailbox,
/// and telemetry.
/// </summary>
[TestClass]
public sealed class AudioEngineTests
{
	private const double SampleRate = 48000.0;
	private const int BlockSize = 256;

	private static void RunBlock(AudioEngine engine, double[] hostNormalized, float[] input, float[] output, bool bypass = false) =>
		engine.Process(hostNormalized, bypass, SampleRate, input, input, output, output);

	[TestMethod]
	public void HostNormalizedChangesAreFoldedThroughTheTaper()
	{
		GainEffect effect = new();
		AudioEngine engine = new(effect);
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		// Normalized 0 maps to -60 dB on the gain taper; run long enough to settle.
		double[] hostNormalized = [0.0];
		for (int i = 0; i < 100; i++)
		{
			RunBlock(engine, hostNormalized, input, output);
		}

		float expected = MathF.Pow(10.0f, -60.0f / 20.0f);
		Assert.AreEqual(expected, output[^1], 1e-4f);
	}

	[TestMethod]
	public void BypassCopiesInputToOutput()
	{
		AudioEngine engine = new(new GainEffect());
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		for (int i = 0; i < input.Length; i++)
		{
			input[i] = i / (float)BlockSize;
		}

		float[] output = new float[BlockSize];
		double[] hostNormalized = [0.0];
		RunBlock(engine, hostNormalized, input, output, bypass: true);

		CollectionAssert.AreEqual(input, output);
	}

	[TestMethod]
	public void PostedParameterChangesApplyOnTheNextBlock()
	{
		GainEffect effect = new();
		AudioEngine engine = new(effect);
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		Array.Fill(input, 1.0f);
		float[] output = new float[BlockSize];

		// Normalized default (0 dB); first block also creates the audio-side mailbox.
		double[] hostNormalized = [engine.Effect.Parameters[GainEffect.GainParameterIndex].Range.Normalize(0.0)];
		for (int i = 0; i < 50; i++)
		{
			RunBlock(engine, hostNormalized, input, output);
		}

		// Post -60 dB from a different (UI) thread; the host normalized value stays unchanged.
		bool posted = false;
		Thread uiThread = new(() => posted = engine.TryPostParameterChange(GainEffect.GainParameterIndex, -60.0));
		uiThread.Start();
		uiThread.Join();
		Assert.IsTrue(posted, "Mailbox rejected the parameter post");

		for (int i = 0; i < 100; i++)
		{
			RunBlock(engine, hostNormalized, input, output);
		}

		float expected = MathF.Pow(10.0f, -60.0f / 20.0f);
		Assert.AreEqual(expected, output[^1], 1e-4f);
	}

	[TestMethod]
	public void PostBeforeAudioStartsIsRejectedNotBlocked()
	{
		AudioEngine engine = new(new GainEffect());
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		Assert.IsFalse(engine.TryPostParameterChange(GainEffect.GainParameterIndex, -6.0));
	}

	[TestMethod]
	public void TelemetryCarriesOutputPeaks()
	{
		AudioEngine engine = new(new GainEffect());
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] input = new float[BlockSize];
		input[BlockSize / 2] = 0.5f;
		float[] output = new float[BlockSize];

		double[] hostNormalized = [engine.Effect.Parameters[GainEffect.GainParameterIndex].Range.Normalize(0.0)];
		for (int i = 0; i < 10; i++)
		{
			RunBlock(engine, hostNormalized, input, output);
		}

		MeterFrame last = default;
		bool any = false;
		while (engine.TryReadTelemetry(out MeterFrame frame))
		{
			last = frame;
			any = true;
		}

		Assert.IsTrue(any, "No telemetry was published");
		Assert.AreEqual(0.5f, last.PeakLeft, 0.05f);
		Assert.AreEqual(0.5f, last.PeakRight, 0.05f);
	}
}
