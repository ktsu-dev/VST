// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

/// <summary>
/// The logical contents of an EffectsHost preset: the effect it belongs to and its plain-value
/// parameter snapshot.
/// </summary>
/// <remarks>
/// Values are keyed by parameter name in plain (real-unit) values rather than by id in normalized
/// values, so presets stay readable and survive reordering of the parameter list.
/// </remarks>
public sealed record EffectsHostPresetState
{
	/// <summary>Gets the name of the effect this preset belongs to.</summary>
	public required string EffectName { get; init; }

	/// <summary>Gets the plain parameter values keyed by parameter name.</summary>
	public required Dictionary<string, double> ParameterValues { get; init; }

	/// <summary>Gets a value indicating whether the effect is bypassed.</summary>
	public bool Bypass { get; init; }
}
