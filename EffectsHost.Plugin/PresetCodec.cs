// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Globalization;

using ktsu.SerializationProvider;

/// <summary>
/// Loads and saves EffectsHost preset state as VST3 <c>.vstpreset</c> files through the
/// <see cref="VstPresetSerializationProvider"/> codec.
/// </summary>
/// <remarks>
/// The logical state is JSON (via <see cref="JsonStateProvider"/>), packaged as the component
/// chunk of a <c>.vstpreset</c> container tagged with the device's processor class id, so each
/// preset file is tied to the device that wrote it.
/// </remarks>
public sealed class PresetCodec
{
	private readonly VstPresetSerializationProvider provider;

	/// <summary>
	/// Initializes a new instance of the <see cref="PresetCodec"/> class for a device.
	/// </summary>
	/// <param name="processorClassId">The device's VST3 processor class id.</param>
	public PresetCodec(Guid processorClassId)
	{
		ClassId = processorClassId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
		provider = new VstPresetSerializationProvider(new JsonStateProvider(), ClassId);
	}

	/// <summary>Gets the 32-character ASCII class id presets are tagged with.</summary>
	public string ClassId { get; }

	/// <summary>
	/// Encodes a preset state into <c>.vstpreset</c> file bytes.
	/// </summary>
	/// <param name="state">The preset state to encode.</param>
	/// <returns>The bytes of a <c>.vstpreset</c> file.</returns>
	public byte[] Encode(EffectsHostPresetState state) => Convert.FromBase64String(provider.Serialize(state));

	/// <summary>
	/// Decodes a preset state from <c>.vstpreset</c> file bytes.
	/// </summary>
	/// <param name="presetBytes">The bytes of a <c>.vstpreset</c> file.</param>
	/// <returns>The decoded preset state.</returns>
	public EffectsHostPresetState Decode(byte[] presetBytes) =>
		provider.Deserialize<EffectsHostPresetState>(Convert.ToBase64String(presetBytes));

	/// <summary>
	/// Saves a preset state to a <c>.vstpreset</c> file.
	/// </summary>
	/// <param name="state">The preset state to save.</param>
	/// <param name="path">The destination file path.</param>
	public void Save(EffectsHostPresetState state, string path) => File.WriteAllBytes(path, Encode(state));

	/// <summary>
	/// Loads a preset state from a <c>.vstpreset</c> file.
	/// </summary>
	/// <param name="path">The source file path.</param>
	/// <returns>The loaded preset state.</returns>
	public EffectsHostPresetState Load(string path) => Decode(File.ReadAllBytes(path));
}
