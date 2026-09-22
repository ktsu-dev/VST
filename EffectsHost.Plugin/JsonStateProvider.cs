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
	/// Indented, because the indentation is part of the bytes a <c>.vstpreset</c> carries: presets
	/// written before the archived codec was replaced have to go on comparing equal to ones written
	/// after it, and a preset opened in a text editor stays readable.
	/// </summary>
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
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
