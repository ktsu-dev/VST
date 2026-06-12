// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Globalization;

using ktsu.EffectsHost.Core;

using NPlug;

/// <summary>
/// An NPlug <see cref="AudioParameter"/> built from an <see cref="EffectParameter"/> descriptor.
/// </summary>
/// <remarks>
/// The descriptor's <c>NormalizedParameter</c> taper supplies the plain-value mapping, so the
/// host's normalized <c>[0, 1]</c> automation domain and the parameter's real units (decibels,
/// hertz, percent, ...) stay consistent everywhere: host generic editors, automation lanes, and
/// the plugin's own UI all go through <see cref="ToPlain"/>/<see cref="ToNormalized"/>.
/// </remarks>
public sealed class EffectAudioParameter : AudioParameter
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EffectAudioParameter"/> class from a descriptor.
	/// </summary>
	/// <param name="descriptor">The effect parameter descriptor to expose to the host.</param>
	public EffectAudioParameter(EffectParameter descriptor)
		: base(
			Validated(descriptor).Name,
			units: descriptor.Units,
			stepCount: descriptor.StepCount,
			defaultNormalizedValue: descriptor.Range.Normalize(descriptor.DefaultValue))
	{
		Descriptor = descriptor;
		Precision = descriptor.DisplayPrecision;
	}

	private static EffectParameter Validated(EffectParameter descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);
		return descriptor;
	}

	/// <summary>Gets the descriptor this parameter was built from.</summary>
	public EffectParameter Descriptor { get; }

	/// <inheritdoc/>
	public override double ToPlain(double normalizedValue) => Descriptor.Range.Denormalize(normalizedValue);

	/// <inheritdoc/>
	public override double ToNormalized(double plainValue) => Descriptor.Range.Normalize(plainValue);

	/// <inheritdoc/>
	public override string ToString(double valueNormalized) =>
		StepCount == 1
			? base.ToString(valueNormalized)
			: ToPlain(valueNormalized).ToString($"F{Precision}", CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public override double FromString(string plainValueAsString) =>
		StepCount == 1
			? base.FromString(plainValueAsString)
			: double.TryParse(plainValueAsString, NumberStyles.Float, CultureInfo.InvariantCulture, out double plainValue)
				? ToNormalized(plainValue)
				: DefaultNormalizedValue;
}
