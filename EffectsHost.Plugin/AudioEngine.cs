// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.Containers;
using ktsu.EffectsHost.Core;
using ktsu.Invoker;

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
///
/// <para>
/// <b>Cross-thread channels.</b> UI → audio parameter changes are posted through an
/// <see cref="Invoker"/> owned by the audio thread via the non-blocking, allocation-free
/// <see cref="Invoker.TryBeginInvoke"/>, and pumped at the top of each block; the audio thread
/// never blocks on the UI. Audio → UI telemetry travels the other way through a lock-free
/// single-producer/single-consumer <see cref="SpscRingBuffer{T}"/>, never through the invoker.
/// </para>
/// </remarks>
public sealed class AudioEngine
{
	private const int MailboxCapacity = 256;
	private const int TelemetryCapacity = 256;

	private readonly EffectParameter[] descriptors;
	private readonly double[] parameterValues;
	private readonly double[] lastHostNormalized;
	private readonly SpscRingBuffer<MeterFrame> telemetry = new(TelemetryCapacity);
	private Invoker? audioMailbox;
	private int audioThreadId = -1;

	/// <summary>
	/// Initializes a new instance of the <see cref="AudioEngine"/> class around an effect.
	/// </summary>
	/// <param name="effect">The effect instance this engine drives.</param>
	public AudioEngine(IAudioEffect effect)
	{
		_ = Ensure.NotNull(effect);

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
	/// Posts a plain-value parameter change from the UI thread to the audio thread without
	/// blocking. The value is applied at the start of the next audio block.
	/// </summary>
	/// <param name="parameterIndex">The parameter's index in <see cref="IAudioEffect.Parameters"/>.</param>
	/// <param name="plainValue">The new plain (real-unit) value.</param>
	/// <returns>
	/// <see langword="true"/> when the change was queued (or applied inline on the audio thread);
	/// <see langword="false"/> when audio is not running yet or the mailbox was full — callers can
	/// simply drop the change, because the host echo of the same edit will still arrive through
	/// the normal parameter sync.
	/// </returns>
	public bool TryPostParameterChange(int parameterIndex, double plainValue)
	{
		Invoker? mailbox = Volatile.Read(ref audioMailbox);
		return mailbox is not null
			&& mailbox.TryBeginInvoke(() => ApplyPostedParameterChange(parameterIndex, plainValue));
	}

	/// <summary>
	/// Drains one telemetry frame published by the audio thread. Call from a single UI thread.
	/// </summary>
	/// <param name="frame">The dequeued frame, when available.</param>
	/// <returns><see langword="true"/> when a frame was available.</returns>
	public bool TryReadTelemetry(out MeterFrame frame) => telemetry.TryDequeue(out frame);

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
		// Own (or re-own, if the host migrated processing) the UI→audio mailbox on this thread,
		// then pump it. Creation is the only allocation and happens outside steady state; the
		// pump itself is allocation-free and never blocks.
		if (audioThreadId != Environment.CurrentManagedThreadId)
		{
			audioThreadId = Environment.CurrentManagedThreadId;
			Volatile.Write(ref audioMailbox, new Invoker(MailboxCapacity));
		}

		audioMailbox?.DoInvokes();

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
		}
		else
		{
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

		PublishTelemetry(outputLeft, outputRight);
	}

	private void PublishTelemetry(ReadOnlySpan<float> outputLeft, ReadOnlySpan<float> outputRight)
	{
		float peakLeft = 0.0f;
		float peakRight = 0.0f;
		for (int i = 0; i < outputLeft.Length; i++)
		{
			peakLeft = MathF.Max(peakLeft, MathF.Abs(outputLeft[i]));
			peakRight = MathF.Max(peakRight, MathF.Abs(outputRight[i]));
		}

		// Dropping frames when the UI is not draining is the correct behaviour: the audio
		// thread must never wait for a consumer.
		_ = telemetry.TryEnqueue(new MeterFrame(peakLeft, peakRight));
	}

	private void ApplyPostedParameterChange(int parameterIndex, double plainValue)
	{
		if ((uint)parameterIndex >= (uint)parameterValues.Length)
		{
			return;
		}

		parameterValues[parameterIndex] = descriptors[parameterIndex].Range.Clamp(plainValue);
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
