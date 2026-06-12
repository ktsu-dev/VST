// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Effects;
using ktsu.EffectsHost.Plugin;

using NPlug;

/// <summary>
/// Tests for the NPlug host shell that is auto-built from an effect definition.
/// </summary>
[TestClass]
public sealed class HostShellTests
{
	[TestMethod]
	public void ModelIsBuiltFromEffectDefinition()
	{
		using GainModel model = new();
		model.Initialize();

		Assert.AreEqual("Gain", model.Effect.Name);
		Assert.IsNotNull(model.ByPassParameter);
		Assert.AreEqual(model.Effect.Parameters.Count, model.EffectParameters.Count);

		EffectAudioParameter gain = model.EffectParameters[GainEffect.GainParameterIndex];
		Assert.AreEqual("Gain", gain.Title);
		Assert.AreEqual("dB", gain.Units);
	}

	[TestMethod]
	public void EffectParameterMapsThroughItsTaper()
	{
		using GainModel model = new();
		model.Initialize();

		EffectAudioParameter gain = model.EffectParameters[GainEffect.GainParameterIndex];

		// Default plain value is 0 dB on a [-60, +12] range.
		Assert.AreEqual(0.0, gain.ToPlain(gain.DefaultNormalizedValue), 1e-9);
		Assert.AreEqual(-60.0, gain.ToPlain(0.0), 1e-9);
		Assert.AreEqual(12.0, gain.ToPlain(1.0), 1e-9);

		double normalized = gain.ToNormalized(-6.0);
		Assert.AreEqual(-6.0, gain.ToPlain(normalized), 1e-9);
	}

	[TestMethod]
	public void EffectParameterFormatsPlainValuesForDisplay()
	{
		using GainModel model = new();
		model.Initialize();

		EffectAudioParameter gain = model.EffectParameters[GainEffect.GainParameterIndex];
		Assert.AreEqual("0.0", gain.ToString(gain.DefaultNormalizedValue));
		Assert.AreEqual("-60.0", gain.ToString(0.0));
	}

	[TestMethod]
	public void FactoryRegistersGainAndDelayDevices()
	{
		AudioPluginFactory factory = EffectsHostPlugin.GetFactory();

		Assert.AreEqual(4, factory.PluginClassInfos.Count);
		Assert.AreEqual(GainProcessor.ClassId, factory.PluginClassInfos[0].ClassId);
		Assert.AreEqual(GainController.ClassId, factory.PluginClassInfos[1].ClassId);
		Assert.AreEqual(DelayProcessor.ClassId, factory.PluginClassInfos[2].ClassId);
		Assert.AreEqual(DelayController.ClassId, factory.PluginClassInfos[3].ClassId);
	}
}
