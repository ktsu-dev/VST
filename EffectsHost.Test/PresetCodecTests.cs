// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.EffectsHost.Test;

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

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
	[DataRow("""{"EffectName":"Gain","ParameterValues":null}""")]
	[DataRow("""{"EffectName":null,"ParameterValues":{"Gain":-6}}""")]
	public void APayloadWithANullRequiredFieldIsRejectedOnDecode(string payload)
	{
		PresetCodec codec = new(GainProcessor.ClassId);
		byte[] presetBytes = VstPresetFile.ToBytes(new VstPreset(
			codec.ClassId,
			Encoding.UTF8.GetBytes(payload),
			ControllerState: null,
			MetaInfo: null,
			VstPresetFile.FormatVersion));

		// A JsonException is what the editor's LoadPreset reports as "Load failed". Before, the
		// payload decoded with a null field, and ApplyPresetState then threw a
		// NullReferenceException out of the ImGui render callback.
		_ = Assert.ThrowsExactly<JsonException>(() => codec.Decode(presetBytes));
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
	public void SavingTwiceUnderTheSameNameKeepsBothPresets()
	{
		PresetCodec codec = new(GainProcessor.ClassId);
		string directory = Path.Combine(Path.GetTempPath(), $"effectshost-test-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		try
		{
			// The editor names presets by a one-second timestamp, so two saves in the same second
			// arrive with the same base name.
			EffectsHostPresetState second = MakeState();
			second.ParameterValues["Gain"] = 3.0;

			string firstPath = codec.SaveWithoutOverwriting(MakeState(), directory, "Gain-20261101-091257");
			string secondPath = codec.SaveWithoutOverwriting(second, directory, "Gain-20261101-091257");

			Assert.AreEqual("Gain-20261101-091257.vstpreset", Path.GetFileName(firstPath));
			Assert.AreEqual("Gain-20261101-091257-2.vstpreset", Path.GetFileName(secondPath));
			Assert.AreEqual(-6.5, codec.Load(firstPath).ParameterValues["Gain"], 1e-12);
			Assert.AreEqual(3.0, codec.Load(secondPath).ParameterValues["Gain"], 1e-12);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public void AFailedWriteReachesTheCallerAndLeavesNoPresetBehind()
	{
		PresetCodec codec = new(GainProcessor.ClassId);
		string directory = Path.Combine(Path.GetTempPath(), "EffectsHost-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			int opens = 0;

			// Creating the file succeeds and the write fails, the way it does on a full disk.
			IOException thrown = Assert.ThrowsExactly<IOException>(() => codec.SaveWithoutOverwriting(MakeState(), directory, "Gain-20261112-120000", path =>
			{
				if (++opens > 1)
				{
					// The bug: the failed write was taken for a collision and the next name tried, forever.
					throw new InvalidOperationException("A second name was tried after a failed write.");
				}

				return new FullDiskStream(new FileStream(path, FileMode.CreateNew, FileAccess.Write));
			}));

			Assert.AreEqual(FullDiskStream.Message, thrown.Message);
			Assert.AreEqual(0, Directory.GetFiles(directory).Length, "The partial preset must be deleted.");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	/// <summary>A stream whose file was created but which has no room for any bytes.</summary>
	private sealed class FullDiskStream(FileStream inner) : Stream
	{
		public const string Message = "No space left on device";

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => inner.Length;
		public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
		public override void Flush() => inner.Flush();
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new IOException(Message);

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				inner.Dispose();
			}

			base.Dispose(disposing);
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
	/// A field whose value would overflow a bounds check is refused as corrupt data, rather than
	/// escaping as an <see cref="ArgumentOutOfRangeException"/> or <see cref="OverflowException"/>
	/// the editor's load handler does not catch.
	/// </summary>
	/// <param name="field">Which field of a valid preset to corrupt.</param>
	[TestMethod]
	[DataRow("listOffset")]
	[DataRow("count")]
	[DataRow("size")]
	public void OverflowingFieldsAreRefusedAsCorrupt(string field)
	{
		byte[] bytes = VstPresetFile.ToBytes(new VstPreset(new string('A', 32), [1, 2, 3], ControllerState: null, MetaInfo: null, VstPresetFile.FormatVersion));
		int listOffset = (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(40));
		int firstEntry = listOffset + 8;

		switch (field)
		{
			case "listOffset":
				BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(40), long.MaxValue);
				break;
			case "count":
				BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(listOffset + 4), 0x70000000);
				break;
			default:
				BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(firstEntry + 4), 1);
				BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(firstEntry + 12), long.MaxValue);
				break;
		}

		Assert.ThrowsExactly<InvalidDataException>(() => VstPresetFile.FromBytes(bytes));
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
		byte[] bareBytes = VstPresetFile.ToBytes(bare);
		VstPreset bareRestored = VstPresetFile.FromBytes(bareBytes);

		Assert.IsNull(bareRestored.ControllerState);
		Assert.IsNull(bareRestored.MetaInfo);

		// Re-saving a preset that was loaded must not add chunks the original did not have.
		CollectionAssert.AreEqual(bareBytes, VstPresetFile.ToBytes(bareRestored));
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
