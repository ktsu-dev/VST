// Copyright (c) 2026 ktsu-dev contributors

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
	/// Saves a preset state to a new <c>.vstpreset</c> file, never replacing an existing one.
	/// </summary>
	/// <param name="state">The preset state to save.</param>
	/// <param name="directory">The directory to save into.</param>
	/// <param name="baseName">The file name to use, without the extension.</param>
	/// <returns>The path of the file that was written.</returns>
	/// <remarks>
	/// The file is <paramref name="baseName"/><c>.vstpreset</c>, or, when that name is taken,
	/// <paramref name="baseName"/><c>-2.vstpreset</c>, <c>-3</c> and so on. Each file is created
	/// with <see cref="FileMode.CreateNew"/>, so a name taken between the check and the write is
	/// skipped rather than overwritten. A failure after the file is created, such as a full disk,
	/// is not a taken name: the partial file is deleted and the exception reaches the caller.
	/// </remarks>
	public string SaveWithoutOverwriting(EffectsHostPresetState state, string directory, string baseName) =>
		SaveWithoutOverwriting(state, directory, baseName, static path => new FileStream(path, FileMode.CreateNew, FileAccess.Write));

	/// <summary>
	/// Saves a preset state to a new <c>.vstpreset</c> file, opening each candidate through
	/// <paramref name="createNew"/>.
	/// </summary>
	/// <param name="state">The preset state to save.</param>
	/// <param name="directory">The directory to save into.</param>
	/// <param name="baseName">The file name to use, without the extension.</param>
	/// <param name="createNew">Creates the file at a path, failing if it already exists.</param>
	/// <returns>The path of the file that was written.</returns>
	internal string SaveWithoutOverwriting(EffectsHostPresetState state, string directory, string baseName, Func<string, Stream> createNew)
	{
		byte[] bytes = Encode(state);
		for (int attempt = 1; ; attempt++)
		{
			string fileName = attempt == 1
				? $"{baseName}.vstpreset"
				: string.Create(CultureInfo.InvariantCulture, $"{baseName}-{attempt}.vstpreset");
			string path = Path.Combine(directory, fileName);
			if (File.Exists(path))
			{
				continue;
			}

			Stream stream;
			try
			{
				stream = createNew(path);
			}
			catch (IOException) when (File.Exists(path))
			{
				// Another writer took the name after the check; try the next one.
				continue;
			}

			try
			{
				using (stream)
				{
					stream.Write(bytes, 0, bytes.Length);
				}

				return path;
			}
			catch
			{
				TryDelete(path);
				throw;
			}
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{
			// Best effort: the original failure is the one the caller needs to see.
		}
		catch (UnauthorizedAccessException)
		{
			// Best effort, as above.
		}
	}

	/// <summary>
	/// Loads a preset state from a <c>.vstpreset</c> file.
	/// </summary>
	/// <param name="path">The source file path.</param>
	/// <returns>The loaded preset state.</returns>
	public EffectsHostPresetState Load(string path) => Decode(File.ReadAllBytes(path));
}
