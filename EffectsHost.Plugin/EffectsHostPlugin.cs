// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Runtime.CompilerServices;

using NPlug;

/// <summary>
/// The VST3 plugin entry point: registers every effect device with the host factory.
/// </summary>
public static class EffectsHostPlugin
{
	/// <summary>
	/// Builds the plugin factory containing all EffectsHost devices.
	/// </summary>
	/// <returns>The configured <see cref="AudioPluginFactory"/>.</returns>
	public static AudioPluginFactory GetFactory()
	{
		AudioPluginFactory factory = new(new("ktsu.dev", "https://github.com/ktsu-dev/vst", "support@ktsu.dev"));
		factory.RegisterPlugin<GainProcessor>(new(GainProcessor.ClassId, "EffectsHost Gain", AudioProcessorCategory.Effect));
		factory.RegisterPlugin<GainController>(new(GainController.ClassId, "EffectsHost Gain Controller"));
		return factory;
	}

	[ModuleInitializer]
	internal static void ExportThisPlugin() => AudioPluginFactoryExporter.Instance = GetFactory();
}
