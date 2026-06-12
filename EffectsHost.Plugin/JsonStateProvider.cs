// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Text.Json;

using ktsu.SerializationProvider;

/// <summary>
/// A System.Text.Json <see cref="ISerializationProvider"/> for the plugin's logical preset state,
/// used as the inner payload of the <c>.vstpreset</c> container.
/// </summary>
public sealed class JsonStateProvider : ISerializationProvider
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
	};

	/// <inheritdoc/>
	public string ProviderName => "System.Text.Json";

	/// <inheritdoc/>
	public string ContentType => "application/json";

	/// <inheritdoc/>
	public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, Options);

	/// <inheritdoc/>
	public string Serialize(object obj, Type type) => JsonSerializer.Serialize(obj, type, Options);

	/// <inheritdoc/>
	public T Deserialize<T>(string data) =>
		JsonSerializer.Deserialize<T>(data, Options) ?? throw new JsonException("Deserialized payload was null.");

	/// <inheritdoc/>
	public object Deserialize(string data, Type type) =>
		JsonSerializer.Deserialize(data, type, Options) ?? throw new JsonException("Deserialized payload was null.");

	/// <inheritdoc/>
	public Task<string> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Serialize(obj));
	}

	/// <inheritdoc/>
	public Task<string> SerializeAsync(object obj, Type type, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Serialize(obj, type));
	}

	/// <inheritdoc/>
	public Task<T> DeserializeAsync<T>(string data, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Deserialize<T>(data));
	}

	/// <inheritdoc/>
	public Task<object> DeserializeAsync(string data, Type type, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Deserialize(data, type));
	}
}
