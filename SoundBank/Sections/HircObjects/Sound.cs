using System.Diagnostics;
using System.IO;

namespace PD2SoundBankEditor {
	public class Sound : HircObject {
		public uint ObjectId { get; protected set; }
		public uint PluginId { get; protected set; }
		public uint StreamType { get; protected set; }
		public uint SourceId { get; protected set; }
		public uint FileId { get; protected set; }
		public uint FileOffset { get; protected set; }
		public uint FileSize { get; protected set; }
		public byte SourceBits { get; protected set; }
		public uint UnknownSize { get; protected set; }
		public NodeBaseParams NodeBaseParams { get; protected set; }

		public Sound(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) {
			section.SoundObjects.Add(this);
		}

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			ObjectId = reader.ReadUInt32();
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
				//Trace.WriteLine($"Plugin 0x{PluginId & 0xF:X1} 0x{(PluginId & 0xFFF0) >> 4:X3}");
				UnknownSize = reader.ReadUInt32(); // Unknown size field
			}

			//Trace.WriteLine($"{ObjectId} {PluginId} {StreamType}");

			NodeBaseParams = new NodeBaseParams(reader, amount + dataOffset);
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(ObjectId);
			dataWriter.Write(PluginId);
			dataWriter.Write(StreamType);
			dataWriter.Write(SourceId);
			dataWriter.Write(FileId);
			if (StreamType != 1) {
				if (StreamType == 0) {
					var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
					if (streamInfo != null) {
						FileOffset = (uint)(Section.SoundBank.Sections.Find(x => x.Name == "DATA").DataOffset + streamInfo.Offset);
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
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}