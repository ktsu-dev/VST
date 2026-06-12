// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using NPlug;

/// <summary>
/// The NPlug edit controller auto-built from an effect definition. Concrete effects subclass this
/// with their model type and class id; parameter handling and editor creation live here.
/// </summary>
/// <typeparam name="TModel">The model type describing the effect's parameters.</typeparam>
public abstract class EffectsHostController<TModel> : AudioController<TModel>
	where TModel : EffectsHostModel, new()
{
	/// <inheritdoc/>
	protected override IAudioPluginView? CreateView() => new EffectsHostEditorView<TModel>(this);
}
