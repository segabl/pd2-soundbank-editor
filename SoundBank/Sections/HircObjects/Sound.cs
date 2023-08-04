using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics;

namespace PD2SoundBankEditor {
	public class Sound : HircObject {
		public uint ObjectId;
		public ushort PluginType;
		public ushort PluginCompany;
		public uint StreamType;
		public uint SourceId;
		public uint FileId;
		public uint FileOffset;
		public uint FileSize;
		public byte SourceBits;
		public uint UnknownSize;
		public NodeBaseParams NodeBaseParams;

		public Sound(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			ObjectId = reader.ReadUInt32();
			PluginType = reader.ReadUInt16();
			PluginCompany = reader.ReadUInt16();
			StreamType = reader.ReadUInt32(); // 0 = embedded, 1 = streamed, 2 = prefetch
			SourceId = reader.ReadUInt32();
			FileId = reader.ReadUInt32();

			if (StreamType == 0) {
				FileOffset = reader.ReadUInt32();
				FileSize = reader.ReadUInt32();
				var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
				if (streamInfo != null) {
					streamInfo.HasReferences = true;
				}
			}
			SourceBits = reader.ReadByte(); // 0 = sfx, 1 = voice

			if (PluginType != 1) {
				UnknownSize = reader.ReadUInt32(); // Unknown size field
			}

			NodeBaseParams = new NodeBaseParams(reader, amount + dataOffset);
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(ObjectId);
			dataWriter.Write(PluginType);
			dataWriter.Write(PluginCompany);
			dataWriter.Write(StreamType);
			dataWriter.Write(SourceId);
			dataWriter.Write(FileId);
			if (StreamType == 0) {
				var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
				if (streamInfo != null) {
					FileOffset = (uint)(Section.SoundBank.Sections.Find(x => x.Name == "DATA").DataOffset + streamInfo.Offset);
					FileSize = (uint)streamInfo.Data.Length;
				}
				dataWriter.Write(FileOffset);
				dataWriter.Write(FileSize);
			}
			dataWriter.Write(SourceBits);
			if (PluginType != 1) {
				dataWriter.Write(UnknownSize);
			}
			NodeBaseParams.Write(dataWriter);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}