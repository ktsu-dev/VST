// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using System.Text;

using ktsu.EffectsHost.Plugin;
using ktsu.SerializationProvider;

/// <summary>
/// Tests for <c>.vstpreset</c> load/save through the SerializationProvider codec.
/// </summary>
[TestClass]
public sealed class PresetCodecTests
{
	private static EffectsHostPresetState MakeState() => new()
	{
		EffectName = "Gain",
		ParameterValues = new Dictionary<string, double> { ["Gain"] = -6.5 },
		Bypass = false,
	};

	[TestMethod]
	public void PresetStateRoundTripsThroughVstPresetBytes()
	{
		PresetCodec codec = new(GainProcessor.ClassId);

		byte[] presetBytes = codec.Encode(MakeState());
		EffectsHostPresetState restored = codec.Decode(presetBytes);

		Assert.AreEqual("Gain", restored.EffectName);
		Assert.AreEqual(-6.5, restored.ParameterValues["Gain"], 1e-12);
		Assert.IsFalse(restored.Bypass);
	}

	[TestMethod]
	public void EncodedBytesAreAWellFormedVstPresetContainer()
	{
		PresetCodec codec = new(GainProcessor.ClassId);

		byte[] presetBytes = codec.Encode(MakeState());
		VstPreset preset = VstPresetFile.FromBytes(presetBytes);

		Assert.AreEqual(codec.ClassId, preset.ClassId);
		Assert.AreEqual(32, preset.ClassId.Length);

		// The component chunk is the JSON payload of the logical state.
		string json = Encoding.UTF8.GetString(preset.ComponentState);
		StringAssert.Contains(json, "\"EffectName\"");
		StringAssert.Contains(json, "\"Gain\"");
	}

	[TestMethod]
	public void PresetFilesRoundTripOnDisk()
	{
		PresetCodec codec = new(GainProcessor.ClassId);
		string path = Path.Combine(Path.GetTempPath(), $"effectshost-test-{Guid.NewGuid():N}.vstpreset");

		try
		{
			codec.Save(MakeState(), path);
			EffectsHostPresetState restored = codec.Load(path);
			Assert.AreEqual(-6.5, restored.ParameterValues["Gain"], 1e-12);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public void DecodingAPresetForADifferentDeviceStillYieldsItsState()
	{
		PresetCodec gainCodec = new(GainProcessor.ClassId);
		PresetCodec delayCodec = new(DelayProcessor.ClassId);

		Assert.AreNotEqual(gainCodec.ClassId, delayCodec.ClassId);

		// The container carries the class id; the logical payload is still decodable, leaving
		// mismatch policy to the caller.
		byte[] presetBytes = gainCodec.Encode(MakeState());
		EffectsHostPresetState restored = delayCodec.Decode(presetBytes);
		Assert.AreEqual("Gain", restored.EffectName);
	}
}
