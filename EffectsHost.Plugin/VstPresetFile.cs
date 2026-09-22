// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.EffectsHost.Plugin;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// The contents of a VST3 <c>.vstpreset</c> container.
/// </summary>
/// <param name="ClassId">
/// The 32-character ASCII processor class id the preset is tagged with, which ties a preset to the
/// device that wrote it.
/// </param>
/// <param name="ComponentState">The processor's state, carried in the <c>Comp</c> chunk.</param>
/// <param name="ControllerState">
/// The controller's state, carried in the <c>Cont</c> chunk. <see langword="null"/> when the
/// container has no such chunk; an empty array is a present but empty chunk, which is a different
/// thing.
/// </param>
/// <param name="MetaInfo">
/// The XML meta chunk, carried in <c>Info</c>, under the same present-versus-empty distinction as
/// <paramref name="ControllerState"/>.
/// </param>
/// <param name="Version">The container format version.</param>
#pragma warning disable CA1819 // Properties should not return arrays - a preset chunk is a
// byte buffer, which is what the container stores and what every caller wants back.
public sealed record VstPreset(
	string ClassId,
	byte[] ComponentState,
	byte[]? ControllerState,
	string? MetaInfo,
	int Version);
#pragma warning restore CA1819

/// <summary>
/// Reads and writes the VST3 <c>.vstpreset</c> container.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the container codec the archived <c>ktsu.SerializationProvider</c> package used to
/// supply. The layout below was taken from the bytes that package actually wrote rather than from
/// its source, and <c>PresetCodecTests</c> holds a preset it wrote as a fixture so that what is
/// produced here stays byte-for-byte what it produced.
/// </para>
/// <para>
/// The layout, little-endian throughout:
/// </para>
/// <code>
/// 0x00  "VST3"                      4 bytes, ASCII
/// 0x04  version                     int32
/// 0x08  class id                    32 bytes, ASCII
/// 0x28  offset of the chunk list    int64
/// 0x30  chunk payloads, in the order the list names them
///       "List"                      4 bytes, ASCII
///       chunk count                 int32
///       per chunk:  id              4 bytes, ASCII ("Comp", "Cont", "Info")
///                   offset          int64
///                   size            int64
/// </code>
/// <para>
/// <c>Comp</c> is always written. <c>Cont</c> and <c>Info</c> are written when their state is
/// present, empty or not, and omitted when it is absent.
/// </para>
/// </remarks>
public static class VstPresetFile
{
	/// <summary>The container format version this writes.</summary>
	public const int FormatVersion = 1;

	private const string HeaderTag = "VST3";
	private const string ListTag = "List";
	private const string ComponentChunk = "Comp";
	private const string ControllerChunk = "Cont";
	private const string MetaChunk = "Info";

	private const int ClassIdLength = 32;
	private const int HeaderLength = 48;
	private const int ChunkEntryLength = 20;

	/// <summary>
	/// Writes a preset to a stream.
	/// </summary>
	/// <param name="stream">The stream to write to.</param>
	/// <param name="preset">The preset to write.</param>
	public static void Write(Stream stream, VstPreset preset)
	{
		Ensure.NotNull(stream);

		byte[] bytes = ToBytes(preset);
		stream.Write(bytes, 0, bytes.Length);
	}

	/// <summary>
	/// Encodes a preset as the bytes of a <c>.vstpreset</c> file.
	/// </summary>
	/// <param name="preset">The preset to encode.</param>
	/// <returns>The file's bytes.</returns>
	/// <exception cref="ArgumentException">
	/// The class id is not <see cref="ClassIdLength"/> ASCII characters.
	/// </exception>
	public static byte[] ToBytes(VstPreset preset)
	{
		Ensure.NotNull(preset);
		Ensure.NotNull(preset.ComponentState);

		if (preset.ClassId is null || preset.ClassId.Length != ClassIdLength)
		{
			throw new ArgumentException(
				$"A VST3 preset class id must be exactly {ClassIdLength} characters.",
				nameof(preset));
		}

		List<(string Id, byte[] Payload)> chunks = [(ComponentChunk, preset.ComponentState)];

		if (preset.ControllerState is not null)
		{
			chunks.Add((ControllerChunk, preset.ControllerState));
		}

		if (preset.MetaInfo is not null)
		{
			chunks.Add((MetaChunk, Encoding.UTF8.GetBytes(preset.MetaInfo)));
		}

		int payloadLength = chunks.Sum(chunk => chunk.Payload.Length);
		long listOffset = HeaderLength + payloadLength;
		int total = (int)listOffset + 8 + (chunks.Count * ChunkEntryLength);

		byte[] bytes = new byte[total];
		Span<byte> span = bytes;

		Encoding.ASCII.GetBytes(HeaderTag).CopyTo(span);
		BinaryPrimitives.WriteInt32LittleEndian(span[4..], preset.Version);
		Encoding.ASCII.GetBytes(preset.ClassId).CopyTo(span[8..]);
		BinaryPrimitives.WriteInt64LittleEndian(span[40..], listOffset);

		int cursor = HeaderLength;
		List<(string Id, long Offset, long Size)> entries = [];

		foreach ((string id, byte[] payload) in chunks)
		{
			payload.CopyTo(span[cursor..]);
			entries.Add((id, cursor, payload.Length));
			cursor += payload.Length;
		}

		Encoding.ASCII.GetBytes(ListTag).CopyTo(span[cursor..]);
		BinaryPrimitives.WriteInt32LittleEndian(span[(cursor + 4)..], chunks.Count);
		cursor += 8;

		foreach ((string id, long offset, long size) in entries)
		{
			Encoding.ASCII.GetBytes(id).CopyTo(span[cursor..]);
			BinaryPrimitives.WriteInt64LittleEndian(span[(cursor + 4)..], offset);
			BinaryPrimitives.WriteInt64LittleEndian(span[(cursor + 12)..], size);
			cursor += ChunkEntryLength;
		}

		return bytes;
	}

	/// <summary>
	/// Reads a preset from a stream.
	/// </summary>
	/// <param name="stream">The stream to read from.</param>
	/// <returns>The preset the stream holds.</returns>
	public static VstPreset Read(Stream stream)
	{
		Ensure.NotNull(stream);

		using MemoryStream buffer = new();
		stream.CopyTo(buffer);
		return FromBytes(buffer.ToArray());
	}

	/// <summary>
	/// Decodes the bytes of a <c>.vstpreset</c> file.
	/// </summary>
	/// <param name="bytes">The file's bytes.</param>
	/// <returns>The preset the bytes hold.</returns>
	/// <exception cref="InvalidDataException">
	/// The bytes are not a VST3 preset, or are truncated or otherwise inconsistent.
	/// </exception>
	public static VstPreset FromBytes(byte[] bytes)
	{
		Ensure.NotNull(bytes);

		if (bytes.Length < HeaderLength)
		{
			throw new InvalidDataException(
				bytes.Length < 4
					? "Corrupt VST3 preset: unexpected end of stream."
					: "Not a VST3 preset: missing 'VST3' header tag.");
		}

		if (Encoding.ASCII.GetString(bytes, 0, 4) != HeaderTag)
		{
			throw new InvalidDataException("Not a VST3 preset: missing 'VST3' header tag.");
		}

		int version = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
		string classId = Encoding.ASCII.GetString(bytes, 8, ClassIdLength);
		long listOffset = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(40));

		if (listOffset < HeaderLength || listOffset + 8 > bytes.Length)
		{
			throw new InvalidDataException("Corrupt VST3 preset: the chunk list is outside the file.");
		}

		int cursor = (int)listOffset;

		if (Encoding.ASCII.GetString(bytes, cursor, 4) != ListTag)
		{
			throw new InvalidDataException("Corrupt VST3 preset: missing 'List' chunk index.");
		}

		int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor + 4));
		cursor += 8;

		if (count < 0 || cursor + (count * ChunkEntryLength) > bytes.Length)
		{
			throw new InvalidDataException("Corrupt VST3 preset: the chunk index is truncated.");
		}

		byte[]? component = null;
		byte[]? controller = null;
		string? metaInfo = null;

		for (int entry = 0; entry < count; entry++)
		{
			string id = Encoding.ASCII.GetString(bytes, cursor, 4);
			long offset = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(cursor + 4));
			long size = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(cursor + 12));
			cursor += ChunkEntryLength;

			if (offset < 0 || size < 0 || offset + size > bytes.Length)
			{
				throw new InvalidDataException($"Corrupt VST3 preset: the '{id}' chunk is outside the file.");
			}

			byte[] payload = new byte[size];
			Array.Copy(bytes, offset, payload, 0, size);

			switch (id)
			{
				case ComponentChunk:
					component = payload;
					break;
				case ControllerChunk:
					controller = payload;
					break;
				case MetaChunk:
					metaInfo = Encoding.UTF8.GetString(payload);
					break;
				default:
					// An unknown chunk is not this reader's to interpret, and not a reason to
					// refuse a preset a host wrote with more in it than the plugin puts there.
					break;
			}
		}

		if (component is null)
		{
			throw new InvalidDataException("Corrupt VST3 preset: no 'Comp' chunk.");
		}

		return new VstPreset(classId, component, controller, metaInfo ?? string.Empty, version);
	}
}
