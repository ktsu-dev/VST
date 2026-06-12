// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.EffectsHost.Effects;

/// <summary>
/// The parameter model for the <see cref="GainEffect"/> device.
/// </summary>
/// <remarks>
/// These three tiny subclasses are the entire per-effect cost of exposing an
/// <see cref="Core.IAudioEffect"/> as a VST3 device: a model, a processor with its class ids, and
/// a controller. Everything else is inherited from the host shell.
/// </remarks>
public sealed class GainModel : EffectsHostModel
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GainModel"/> class.
	/// </summary>
	public GainModel() : base(new GainEffect())
	{
	}
}

/// <summary>
/// The audio processor for the <see cref="GainEffect"/> device.
/// </summary>
public sealed class GainProcessor : EffectsHostProcessor<GainModel>
{
	/// <summary>The stable VST3 component class id of the Gain device.</summary>
	public static readonly Guid ClassId = new("c416d72d-ef52-4022-9bf2-2a307223661a");

	/// <inheritdoc/>
	public override Guid ControllerClassId => GainController.ClassId;
}

/// <summary>
/// The edit controller for the <see cref="GainEffect"/> device.
/// </summary>
public sealed class GainController : EffectsHostController<GainModel>
{
	/// <summary>The stable VST3 controller class id of the Gain device.</summary>
	public static readonly Guid ClassId = new("05ded0dc-50dc-4b43-b9d3-b1955ad20e09");

	/// <inheritdoc/>
	public override Guid ProcessorClassId => GainProcessor.ClassId;
}
