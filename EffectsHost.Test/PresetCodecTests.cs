// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using System.Text;

using ktsu.EffectsHost.Plugin;

/// <summary>
/// Tests for <c>.vstpreset</c> load/save through the in-repo container codec.
/// </summary>
[TestClass]
public sealed class PresetCodecTests
{
	private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "gain-preset-v1.vstpreset");

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

	/// <summary>
	/// The bytes this writes are the bytes the archived <c>ktsu.SerializationProvider</c> codec
	/// wrote, to the byte.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>Fixtures/gain-preset-v1.vstpreset</c> was produced by the archived codec, on this same
	/// state and class id, and committed <em>before</em> the dependency was removed - once it is
	/// gone there is nothing left to compare against. Every <c>.vstpreset</c> a user has already
	/// saved was written by that code, so this is what says those files did not quietly change
	/// meaning.
	/// </para>
	/// <para>
	/// It pins the container layout and the JSON payload together: the indentation of the JSON is
	/// inside the component chunk, so a change to either the chunk framing or the serializer options
	/// fails here.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void EncodedBytesMatchThePresetTheArchivedCodecWrote()
	{
		PresetCodec codec = new(GainProcessor.ClassId);

		byte[] written = codec.Encode(MakeState());
		byte[] archived = File.ReadAllBytes(FixturePath);

		CollectionAssert.AreEqual(archived, written, "The preset bytes differ from the ones the archived codec produced.");
	}

	/// <summary>
	/// And a preset the archived codec wrote still loads.
	/// </summary>
	[TestMethod]
	public void APresetWrittenByTheArchivedCodecStillLoads()
	{
		PresetCodec codec = new(GainProcessor.ClassId);

		EffectsHostPresetState restored = codec.Decode(File.ReadAllBytes(FixturePath));

		Assert.AreEqual("Gain", restored.EffectName);
		Assert.AreEqual(-6.5, restored.ParameterValues["Gain"], 1e-12);
		Assert.IsFalse(restored.Bypass);
	}

	/// <summary>
	/// Bytes that are not a preset are refused rather than half-read.
	/// </summary>
	[TestMethod]
	public void BytesThatAreNotAPresetAreRefused()
	{
		Assert.ThrowsExactly<InvalidDataException>(() => VstPresetFile.FromBytes(new byte[8]));
		Assert.ThrowsExactly<InvalidDataException>(() => VstPresetFile.FromBytes(Encoding.ASCII.GetBytes(new string('x', 64))));
	}

	/// <summary>
	/// A controller chunk and a meta chunk survive a round trip, and a preset without them reports
	/// their absence rather than an empty chunk.
	/// </summary>
	[TestMethod]
	public void OptionalChunksRoundTrip()
	{
		string classId = new('A', 32);

		VstPreset full = new(classId, [1, 2, 3], [4, 5], "<meta/>", VstPresetFile.FormatVersion);
		VstPreset restored = VstPresetFile.FromBytes(VstPresetFile.ToBytes(full));

		Assert.AreEqual(classId, restored.ClassId);
		CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, restored.ComponentState);
		CollectionAssert.AreEqual(new byte[] { 4, 5 }, restored.ControllerState);
		Assert.AreEqual("<meta/>", restored.MetaInfo);

		VstPreset bare = new(classId, [1], ControllerState: null, MetaInfo: null, VstPresetFile.FormatVersion);
		VstPreset bareRestored = VstPresetFile.FromBytes(VstPresetFile.ToBytes(bare));

		Assert.IsNull(bareRestored.ControllerState);
	}

	/// <summary>
	/// The bytes a preset is written as do not depend on the line endings of the machine that wrote
	/// it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>WriteIndented</c> defaults to <see cref="Environment.NewLine"/>, so before the newline was
	/// pinned this same state came out six bytes longer on Windows than on Linux, with every chunk
	/// offset in the container header shifted to match. A preset's bytes depended on who saved it.
	/// </para>
	/// <para>
	/// Asserted as "no CR anywhere in the payload" rather than by comparing two platforms, so the
	/// check means the same thing on whichever one runs it - this is precisely the test that has to
	/// keep working on a Linux-only run to be worth anything.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void PresetBytesDoNotDependOnTheHostsLineEndings()
	{
		PresetCodec codec = new(GainProcessor.ClassId);

		byte[] payload = VstPresetFile.FromBytes(codec.Encode(MakeState())).ComponentState;
		string json = Encoding.UTF8.GetString(payload);

		Assert.IsTrue(json.Contains('\n', StringComparison.Ordinal), "The payload is not indented at all.");
		Assert.IsFalse(
			json.Contains('\r', StringComparison.Ordinal),
			"The payload carries CR, so its bytes follow the host's line endings rather than being fixed.");
	}
}
