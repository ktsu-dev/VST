// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.EffectsHost.Core;

using NPlug;

/// <summary>
/// The NPlug audio processor auto-built from an effect definition. Concrete effects subclass this
/// with their model type and class ids; all processing behaviour lives here.
/// </summary>
/// <typeparam name="TModel">The model type describing the effect's parameters.</typeparam>
/// <remarks>
/// <see cref="ProcessMain"/> is the real-time sanctuary: it snapshots the model's normalized
/// parameter values into a preallocated buffer and hands the block to the <see cref="AudioEngine"/>.
/// Nothing on that path allocates, locks, or blocks.
/// </remarks>
public abstract class EffectsHostProcessor<TModel> : AudioProcessor<TModel>
	where TModel : EffectsHostModel, new()
{
	private double[] hostNormalizedValues = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="EffectsHostProcessor{TModel}"/> class.
	/// </summary>
	protected EffectsHostProcessor() : base(AudioSampleSizeSupport.Float32)
	{
	}

	/// <summary>Gets the real-time engine, available once the processor has been initialized.</summary>
	public AudioEngine? Engine { get; private set; }

	/// <inheritdoc/>
	protected override bool Initialize(AudioHostApplication host)
	{
		AddAudioInput("Stereo Input", SpeakerArrangement.SpeakerStereo);
		AddAudioOutput("Stereo Output", SpeakerArrangement.SpeakerStereo);

		Engine = new AudioEngine(Model.Effect);
		hostNormalizedValues = new double[Model.EffectParameters.Count];
		return true;
	}

	/// <inheritdoc/>
	protected override void OnActivate(bool isActive)
	{
		if (isActive)
		{
			Engine?.Prepare(new AudioEffectSetup(ProcessSetupData.SampleRate, ProcessSetupData.MaxSamplesPerBlock));
		}
		else
		{
			Engine?.Reset();
		}
	}

	/// <inheritdoc/>
	protected override void ProcessMain(in AudioProcessData data)
	{
		AudioEngine? currentEngine = Engine;
		if (currentEngine is null || data.Input.BusCount == 0 || data.Output.BusCount == 0 || data.SampleCount == 0)
		{
			return;
		}

		// Snapshot the host-synchronized normalized values. Reads are plain loads from the
		// model's shared parameter buffer; NPlug has already applied this block's inbound
		// parameter changes by the time ProcessMain runs.
		IReadOnlyList<EffectAudioParameter> parameters = Model.EffectParameters;
		for (int i = 0; i < hostNormalizedValues.Length; i++)
		{
			hostNormalizedValues[i] = parameters[i].NormalizedValue;
		}

		bool bypass = Model.ByPassParameter is { } byPassParameter && byPassParameter.NormalizedValue > 0.5;

		currentEngine.Process(
			hostNormalizedValues,
			bypass,
			ProcessSetupData.SampleRate,
			data.Input[0].GetChannelSpanAsFloat32(ProcessSetupData, data, 0),
			data.Input[0].GetChannelSpanAsFloat32(ProcessSetupData, data, 1),
			data.Output[0].GetChannelSpanAsFloat32(ProcessSetupData, data, 0),
			data.Output[0].GetChannelSpanAsFloat32(ProcessSetupData, data, 1));
	}
}
