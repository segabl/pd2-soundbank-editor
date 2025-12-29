using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PD2SoundBankEditor {
	public class Sound : HircObject {
		public uint PluginId { get; protected set; }
		public uint StreamType { get; protected set; }
		public uint SourceId { get; protected set; }
		public uint FileId { get; protected set; }
		public uint FileOffset { get; protected set; }
		public uint FileSize { get; protected set; }
		public byte SourceBits { get; protected set; }
		public uint UnknownSize { get; protected set; }
		public byte[] Unhandled { get; protected set; }

		public string StreamTypeName {
			get => StreamType switch {
				0 => "Embedded",
				1 => "Streamed",
				2 => "Prefetch",
				_ => $"Unknown (0x{StreamType:x2})"
			};
		}

		public Sound(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			PluginId = reader.ReadUInt32();
			StreamType = reader.ReadUInt32(); // 0 = embedded, 1 = streamed, 2 = prefetch
			SourceId = reader.ReadUInt32();
			FileId = reader.ReadUInt32();

			if (StreamType != 1) {
				FileOffset = reader.ReadUInt32();
				FileSize = reader.ReadUInt32();
				if (StreamType == 0) {
					var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
					if (streamInfo != null) {
						streamInfo.HasReferences = true;
					}
				}
			}

			SourceBits = reader.ReadByte(); // 0 = sfx, 1 = voice

			if ((PluginId & 0xF) > 1) {
				UnknownSize = reader.ReadUInt32(); // Unknown size field
			}

			NodeBaseParams = new(reader);

			Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(PluginId);
			dataWriter.Write(StreamType);
			dataWriter.Write(SourceId);
			dataWriter.Write(FileId);
			if (StreamType != 1) {
				if (StreamType == 0) {
					var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
					if (streamInfo != null) {
						FileOffset = (uint)(Section.SoundBank.GetSection<DataSection>().DataOffset + streamInfo.Offset);
						FileSize = (uint)streamInfo.Data.Length;
					}
				}

				dataWriter.Write(FileOffset);
				dataWriter.Write(FileSize);
			}

			dataWriter.Write(SourceBits);
			if ((PluginId & 0xF) > 1) {
				dataWriter.Write(UnknownSize);
			}

			NodeBaseParams.Write(dataWriter);
			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}

		public override Dictionary<string, string> DisplayProperties() {
			var properties = base.DisplayProperties();

			properties.Add("Sound Type", StreamTypeName);
			properties.Add("Sound ID", SourceId.ToString());

			foreach (var prop in NodeBaseParams.DisplayProperties()) {
				properties.Add(prop.Key, prop.Value);
			}

			return properties;
		}
	}
}