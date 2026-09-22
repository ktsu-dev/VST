// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

/// <summary>
/// Loads and saves EffectsHost preset state as VST3 <c>.vstpreset</c> files.
/// </summary>
/// <remarks>
/// The logical state is JSON (via <see cref="JsonStateProvider"/>), packaged as the component
/// chunk of a <c>.vstpreset</c> container tagged with the device's processor class id, so each
/// preset file is tied to the device that wrote it.
/// </remarks>
/// <param name="processorClassId">The device's VST3 processor class id.</param>
public sealed class PresetCodec(Guid processorClassId)
{
	/// <summary>Gets the 32-character ASCII class id presets are tagged with.</summary>
	public string ClassId { get; } = processorClassId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();

	/// <summary>
	/// Encodes a preset state into <c>.vstpreset</c> file bytes.
	/// </summary>
	/// <param name="state">The preset state to encode.</param>
	/// <returns>The bytes of a <c>.vstpreset</c> file.</returns>
	public byte[] Encode(EffectsHostPresetState state) => VstPresetFile.ToBytes(new VstPreset(
		ClassId,
		Encoding.UTF8.GetBytes(JsonStateProvider.Serialize(state)),
		ControllerState: null,
		MetaInfo: null,
		VstPresetFile.FormatVersion));

	/// <summary>
	/// Decodes a preset state from <c>.vstpreset</c> file bytes.
	/// </summary>
	/// <param name="presetBytes">The bytes of a <c>.vstpreset</c> file.</param>
	/// <returns>The decoded preset state.</returns>
	/// <remarks>
	/// Deliberately not static, though it reads nothing off the instance. It is the other half of
	/// the <see cref="Encode"/> pair on a device's codec, and a preset's class id is read from the
	/// container rather than checked against this one - see
	/// <c>DecodingAPresetForADifferentDeviceStillYieldsItsState</c>, which pins that mismatch policy
	/// as the caller's to set.
	/// </remarks>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Half of the instance Encode/Decode pair; making it static would break every caller.")]
	public EffectsHostPresetState Decode(byte[] presetBytes) =>
		JsonStateProvider.Deserialize<EffectsHostPresetState>(
			Encoding.UTF8.GetString(VstPresetFile.FromBytes(presetBytes).ComponentState));

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
