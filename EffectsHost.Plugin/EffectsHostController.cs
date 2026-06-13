// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using NPlug;

/// <summary>
/// The NPlug edit controller auto-built from an effect definition. Concrete effects subclass this
/// with their model type and class id; parameter handling, the engine bridge, preset
/// capture/apply, and editor creation live here.
/// </summary>
/// <typeparam name="TModel">The model type describing the effect's parameters.</typeparam>
public abstract class EffectsHostController<TModel> : AudioController<TModel>
	where TModel : EffectsHostModel, new()
{
	/// <summary>
	/// Gets the paired processor's VST3 class id, used to tag <c>.vstpreset</c> files written by
	/// the editor so each preset stays bound to the device that created it.
	/// </summary>
	public abstract Guid ProcessorClassId { get; }

	/// <summary>
	/// Gets the paired processor's real-time engine, once the host has connected the components.
	/// Used by the editor for the direct UI → audio parameter mailbox and telemetry.
	/// </summary>
	public AudioEngine? Engine { get; private set; }

	/// <inheritdoc/>
	protected override void OnMessage(AudioMessage message)
	{
		if (message.Id == EngineRegistry.BridgeMessageId
			&& message.Attributes.TryGetInt64(EngineRegistry.EngineIdAttribute, out long engineId)
			&& EngineRegistry.TryGet(engineId, out AudioEngine? engine))
		{
			Engine = engine;
		}
	}

	/// <summary>
	/// Captures the current parameter values as a preset state.
	/// </summary>
	/// <returns>The captured state, with plain values keyed by parameter name.</returns>
	public EffectsHostPresetState CapturePresetState()
	{
		Dictionary<string, double> values = [];
		foreach (EffectAudioParameter parameter in Model.EffectParameters)
		{
			values[parameter.Title] = parameter.ToPlain(parameter.NormalizedValue);
		}

		return new EffectsHostPresetState
		{
			EffectName = Model.Effect.Name,
			ParameterValues = values,
			Bypass = Model.ByPassParameter is { } byPass && byPass.NormalizedValue > 0.5,
		};
	}

	/// <summary>
	/// Applies a preset state: each value is set through the begin/perform/end edit protocol so
	/// the host records the changes, and forwarded to the audio engine's mailbox.
	/// </summary>
	/// <param name="state">The preset state to apply.</param>
	public void ApplyPresetState(EffectsHostPresetState state)
	{
		_ = Ensure.NotNull(state);

		IReadOnlyList<EffectAudioParameter> parameters = Model.EffectParameters;
		for (int i = 0; i < parameters.Count; i++)
		{
			EffectAudioParameter parameter = parameters[i];
			if (!state.ParameterValues.TryGetValue(parameter.Title, out double plainValue))
			{
				continue;
			}

			SetParameterAsEdit(parameter, parameter.ToNormalized(plainValue));
			_ = Engine?.TryPostParameterChange(i, plainValue);
		}

		if (Model.ByPassParameter is { } byPass)
		{
			SetParameterAsEdit(byPass, state.Bypass ? 1.0 : 0.0);
		}
	}

	private void SetParameterAsEdit(AudioParameter parameter, double normalizedValue)
	{
		BeginEditParameter(parameter);
		parameter.NormalizedValue = normalizedValue;
		EndEditParameter();
	}
}
