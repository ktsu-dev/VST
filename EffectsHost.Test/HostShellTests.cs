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
/// <remarks>
/// These tests deliberately do not call <c>AudioProcessorModel.Initialize()</c>. The parameter
/// set, bypass parameter, and taper mapping are all built in the model constructor; Initialize only
/// allocates NPlug's internal native shared-parameter buffer, which is exercised inside a real host.
/// NPlug 0.4.0.377 allocates that buffer with <c>NativeMemory.AlignedAlloc</c> but frees it with the
/// mismatched <c>NativeMemory.Free</c> in <c>Dispose</c>; on Windows that pairing corrupts the heap
/// (exit code 0xC0000374), so initializing and disposing a model in-process would crash the test
/// host. (On Linux the two are compatible, which is why it only surfaces on the Windows CI runner.)
/// </remarks>
[TestClass]
public sealed class HostShellTests
{
	[TestMethod]
	public void ModelIsBuiltFromEffectDefinition()
	{
		using GainModel model = new();

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
