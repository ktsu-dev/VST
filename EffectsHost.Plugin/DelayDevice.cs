// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using ktsu.EffectsHost.Effects;

/// <summary>
/// The parameter model for the <see cref="DelayEffect"/> device.
/// </summary>
public sealed class DelayModel : EffectsHostModel
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DelayModel"/> class.
	/// </summary>
	public DelayModel() : base(new DelayEffect())
	{
	}
}

/// <summary>
/// The audio processor for the <see cref="DelayEffect"/> device.
/// </summary>
public sealed class DelayProcessor : EffectsHostProcessor<DelayModel>
{
	/// <summary>The stable VST3 component class id of the Delay device.</summary>
	public static readonly Guid ClassId = new("be26f34b-fa62-49a1-bc1c-6eb7293442cf");

	/// <inheritdoc/>
	public override Guid ControllerClassId => DelayController.ClassId;
}

/// <summary>
/// The edit controller for the <see cref="DelayEffect"/> device.
/// </summary>
public sealed class DelayController : EffectsHostController<DelayModel>
{
	/// <summary>The stable VST3 controller class id of the Delay device.</summary>
	public static readonly Guid ClassId = new("0ddf2d67-c9b2-4745-aa4c-5b3ba23cffdd");

	/// <inheritdoc/>
	public override Guid ProcessorClassId => DelayProcessor.ClassId;
}
