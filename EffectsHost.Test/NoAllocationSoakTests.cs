// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Core;
using ktsu.EffectsHost.Effects;
using ktsu.EffectsHost.Plugin;

/// <summary>
/// Soak tests proving the steady-state audio callback path performs zero GC allocations, with a
/// live UI thread hammering the parameter mailbox and draining telemetry while audio runs.
/// </summary>
[TestClass]
public sealed class NoAllocationSoakTests
{
	private const double SampleRate = 48000.0;
	private const int BlockSize = 128;
	private const int WarmupBlocks = 200;
	private const int SoakBlocks = 20_000;

	[TestMethod]
	public void GainCallbackPathDoesNotAllocate() => SoakEffect(new GainEffect(), GainEffect.GainParameterIndex, -12.0, 6.0);

	[TestMethod]
	public void DelayCallbackPathDoesNotAllocate() => SoakEffect(new DelayEffect(), DelayEffect.MixParameterIndex, 10.0, 90.0);

	private static void SoakEffect(IAudioEffect effect, int uiParameterIndex, double uiValueA, double uiValueB)
	{
		AudioEngine engine = new(effect);
		engine.Prepare(new AudioEffectSetup(SampleRate, BlockSize));

		float[] inputLeft = new float[BlockSize];
		float[] inputRight = new float[BlockSize];
		float[] outputLeft = new float[BlockSize];
		float[] outputRight = new float[BlockSize];
		for (int i = 0; i < BlockSize; i++)
		{
			inputLeft[i] = MathF.Sin(2.0f * MathF.PI * 330.0f * i / (float)SampleRate);
			inputRight[i] = inputLeft[i];
		}

		double[] hostNormalized = new double[effect.Parameters.Count];
		for (int i = 0; i < hostNormalized.Length; i++)
		{
			hostNormalized[i] = effect.Parameters[i].Range.Normalize(effect.Parameters[i].DefaultValue);
		}

		// Warm up: tier-up, the one-time audio-side mailbox creation, and smoother settling all
		// happen here, outside the measured window.
		for (int i = 0; i < WarmupBlocks; i++)
		{
			engine.Process(hostNormalized, bypass: false, SampleRate, inputLeft, inputRight, outputLeft, outputRight);
		}

		// A live UI thread posts parameter flips through the mailbox and drains telemetry for
		// the whole measured window, so the cross-thread paths are exercised, not idle. Its own
		// allocations land on the UI thread, which is allowed; the audio thread is what must not
		// allocate.
		using CancellationTokenSource cancellation = new();
		Thread uiThread = new(() =>
		{
			bool flip = false;
			while (!cancellation.Token.IsCancellationRequested)
			{
				flip = !flip;
				_ = engine.TryPostParameterChange(uiParameterIndex, flip ? uiValueA : uiValueB);
				while (engine.TryReadTelemetry(out _))
				{
					// Drain.
				}

				Thread.Yield();
			}
		})
		{
			IsBackground = true,
		};
		uiThread.Start();

		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

		for (int i = 0; i < SoakBlocks; i++)
		{
			engine.Process(hostNormalized, bypass: false, SampleRate, inputLeft, inputRight, outputLeft, outputRight);
		}

		long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

		cancellation.Cancel();
		uiThread.Join();

		long allocated = allocatedAfter - allocatedBefore;
		Assert.AreEqual(0L, allocated, $"Audio callback path allocated {allocated} bytes over {SoakBlocks} blocks");
	}
}
