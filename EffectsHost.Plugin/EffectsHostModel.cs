// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.EffectsHost.Core;

using NPlug;

/// <summary>
/// The NPlug parameter model auto-built from an <see cref="IAudioEffect"/> definition: a bypass
/// parameter plus one <see cref="EffectAudioParameter"/> per effect parameter, in declaration
/// order.
/// </summary>
/// <remarks>
/// NPlug requires the processor and controller to construct their own model instances via a
/// parameterless constructor, so each concrete effect gets a trivial subclass (for example
/// <c>GainModel</c>) that supplies its effect instance.
/// </remarks>
public abstract class EffectsHostModel : AudioProcessorModel
{
	private readonly EffectAudioParameter[] effectParameters;

	/// <summary>
	/// Initializes a new instance of the <see cref="EffectsHostModel"/> class from an effect
	/// definition.
	/// </summary>
	/// <param name="effect">The effect whose parameters define this model.</param>
	protected EffectsHostModel(IAudioEffect effect) : base(effect?.Name ?? string.Empty)
	{
		_ = Ensure.NotNull(effect);

		Effect = effect;
		AddByPassParameter();

		effectParameters = new EffectAudioParameter[effect.Parameters.Count];
		for (int i = 0; i < effectParameters.Length; i++)
		{
			effectParameters[i] = AddParameter(new EffectAudioParameter(effect.Parameters[i]));
		}
	}

	/// <summary>Gets the effect definition this model was built from.</summary>
	public IAudioEffect Effect { get; }

	/// <summary>Gets the effect parameters, indexed like <see cref="IAudioEffect.Parameters"/>.</summary>
	public IReadOnlyList<EffectAudioParameter> EffectParameters => effectParameters;
}
