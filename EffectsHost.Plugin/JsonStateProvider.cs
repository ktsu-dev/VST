// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Text.Json;

/// <summary>
/// System.Text.Json serialization for the plugin's logical preset state, used as the inner payload
/// of the <c>.vstpreset</c> container.
/// </summary>
/// <remarks>
/// This used to implement <c>ktsu.SerializationProvider.ISerializationProvider</c>, an interface it
/// was the only implementation of, and which existed here only because the archived package's
/// container codec took one as a constructor argument. With that codec replaced by
/// <see cref="VstPresetFile"/>, nothing asks for the interface and the indirection has no second
/// implementation to justify it, so what is left is the two calls <see cref="PresetCodec"/> makes.
/// </remarks>
public static class JsonStateProvider
{
	/// <summary>
	/// The options every preset payload is written with.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Indented, because the indentation is inside the component chunk and so is part of the bytes a
	/// <c>.vstpreset</c> carries, and because a preset opened in a text editor should be readable.
	/// </para>
	/// <para>
	/// <see cref="JsonSerializerOptions.NewLine"/> is pinned to <c>"\n"</c> rather than left to
	/// default, and that is load-bearing. The default is <see cref="Environment.NewLine"/>, so the
	/// same preset written on Windows and on Linux came out as two different files - six bytes apart
	/// here, and the chunk offsets in the container header differ with it. That was true of the
	/// archived codec too, so it is a defect this inherited rather than introduced, and it made a
	/// preset's bytes depend on the machine that saved it: hashing, byte-comparing or deduplicating
	/// presets across a team gave different answers for identical state.
	/// </para>
	/// <para>
	/// Pinning it makes the output identical everywhere and equal to what the archived codec wrote on
	/// Linux and macOS. On Windows the newlines inside the JSON payload change from CRLF to LF, which
	/// is whitespace JSON does not ascribe meaning to: presets written by the old code still load
	/// unchanged on every platform, which is what
	/// <c>PresetCodecTests.APresetWrittenByTheArchivedCodecStillLoads</c> holds.
	/// </para>
	/// </remarks>
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		NewLine = "\n",
	};

	/// <summary>Serializes a value to JSON.</summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The JSON representation of <paramref name="value"/>.</returns>
	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

	/// <summary>Deserializes a value from JSON.</summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="json">The JSON to read.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="JsonException">The payload deserialized to null.</exception>
	public static T Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Deserialized payload was null.");
}
