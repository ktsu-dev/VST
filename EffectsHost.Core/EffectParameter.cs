// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Core;

using ktsu.Semantics;

/// <summary>
/// A declarative description of one automatable parameter exposed by an <see cref="IAudioEffect"/>.
/// </summary>
/// <remarks>
/// Effects declare their parameters as a list of these descriptors and the host shell does the rest:
/// it builds the VST3 parameter set, the automation mapping, and the editor UI from them. The
/// real-unit range and its response curve are modelled by a <see cref="NormalizedParameter{T}"/>
/// taper, which converts between the host's normalized <c>[0, 1]</c> domain and the parameter's
/// plain value (decibels, hertz, percent, ...) in both directions.
/// </remarks>
public sealed record EffectParameter
{
	/// <summary>Gets the human-readable name shown by hosts and the editor.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the unit suffix shown after the value (for example <c>dB</c> or <c>ms</c>), if any.</summary>
	public string? Units { get; init; }

	/// <summary>Gets the mapping between the host-normalized <c>[0, 1]</c> domain and the plain value range.</summary>
	public required NormalizedParameter<double> Range { get; init; }

	/// <summary>Gets the default plain value the parameter starts at.</summary>
	public required double DefaultValue { get; init; }

	/// <summary>
	/// Gets the number of discrete steps, or <c>0</c> for a continuous parameter. A value of <c>1</c>
	/// makes the parameter a two-state toggle.
	/// </summary>
	public int StepCount { get; init; }

	/// <summary>Gets the number of fractional digits the editor displays for the plain value.</summary>
	public int DisplayPrecision { get; init; } = 1;

	/// <summary>
	/// Creates a continuous parameter with a linear response between two plain values.
	/// </summary>
	/// <param name="name">The human-readable parameter name.</param>
	/// <param name="min">The plain value at normalized position <c>0</c>.</param>
	/// <param name="max">The plain value at normalized position <c>1</c>.</param>
	/// <param name="defaultValue">The default plain value.</param>
	/// <param name="units">The unit suffix to display, if any.</param>
	/// <returns>A new <see cref="EffectParameter"/>.</returns>
	public static EffectParameter Linear(string name, double min, double max, double defaultValue, string? units = null) => new()
	{
		Name = name,
		Units = units,
		Range = NormalizedParameter<double>.Linear(min, max),
		DefaultValue = defaultValue,
	};

	/// <summary>
	/// Creates a level parameter expressed in decibels.
	/// </summary>
	/// <remarks>
	/// The taper is linear in the decibel domain, which is already logarithmic in amplitude — equal
	/// control movements produce equal gain ratios, which is how a level control should feel.
	/// </remarks>
	/// <param name="name">The human-readable parameter name.</param>
	/// <param name="min">The level, in decibels, at normalized position <c>0</c>.</param>
	/// <param name="max">The level, in decibels, at normalized position <c>1</c>.</param>
	/// <param name="defaultValue">The default level in decibels.</param>
	/// <returns>A new <see cref="EffectParameter"/>.</returns>
	public static EffectParameter FromDecibels(string name, Decibels<double> min, Decibels<double> max, Decibels<double> defaultValue) => new()
	{
		Name = name,
		Units = "dB",
		Range = NormalizedParameter<double>.Linear(min.Value, max.Value),
		DefaultValue = defaultValue.Value,
	};

	/// <summary>
	/// Creates a continuous parameter with a logarithmic (constant-ratio) response, the natural
	/// taper for frequency and time controls.
	/// </summary>
	/// <param name="name">The human-readable parameter name.</param>
	/// <param name="min">The plain value at normalized position <c>0</c>; must be non-zero and share its sign with <paramref name="max"/>.</param>
	/// <param name="max">The plain value at normalized position <c>1</c>.</param>
	/// <param name="defaultValue">The default plain value.</param>
	/// <param name="units">The unit suffix to display, if any.</param>
	/// <returns>A new <see cref="EffectParameter"/>.</returns>
	public static EffectParameter Logarithmic(string name, double min, double max, double defaultValue, string? units = null) => new()
	{
		Name = name,
		Units = units,
		Range = NormalizedParameter<double>.Logarithmic(min, max),
		DefaultValue = defaultValue,
	};

	/// <summary>
	/// Creates a percentage parameter spanning <paramref name="min"/> to <paramref name="max"/> percent.
	/// </summary>
	/// <param name="name">The human-readable parameter name.</param>
	/// <param name="min">The percentage at normalized position <c>0</c>.</param>
	/// <param name="max">The percentage at normalized position <c>1</c>.</param>
	/// <param name="defaultValue">The default percentage.</param>
	/// <returns>A new <see cref="EffectParameter"/>.</returns>
	public static EffectParameter FromPercent(string name, Percent<double> min, Percent<double> max, Percent<double> defaultValue) => new()
	{
		Name = name,
		Units = "%",
		Range = NormalizedParameter<double>.Linear(min.Value, max.Value),
		DefaultValue = defaultValue.Value,
		DisplayPrecision = 0,
	};
}
