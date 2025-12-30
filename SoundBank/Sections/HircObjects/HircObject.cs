using System;
using System.Collections.Generic;
using System.IO;

namespace PD2SoundBankEditor {
	public class HircObject {
		public static HircObject Read(HircSection section, BinaryReader reader) {
			var type = reader.ReadByte();
			return type switch {
				0x02 => new Sound(section, type, reader),
				0x03 => new Action(section, type, reader),
				0x04 => new Event(section, type, reader),
				0x05 => new RandomSequenceContainer(section, type, reader),
				0x07 => new ActorMixer(section, type, reader),
				_ => new HircObject(section, type, reader)
			};
		}

		public HircSection Section { get; protected set; }
		public long Offset { get; protected set; }
		public byte Type { get; protected set; }
		public uint Size { get; protected set; }
		public uint Id { get; protected set; }
		public string StringId { get; protected set; }
		public byte[] Data { get; protected set; }

		public string TypeName {
			get => Type switch {
				0x02 => "Sound",
				0x03 => "Action",
				0x04 => "Event",
				0x05 => "Random/Sequence Container",
				0x06 => "Switch Container",
				0x07 => "Actor Mixer",
				0x08 => "Audio Bus",
				0x09 => "Blend Container",
				0x0A => "Music Segment",
				0x0B => "Music Track",
				0x0C => "Music Switch Container",
				0x0D => "Music Playlist Container",
				0x0E => "Attenuation",
				0x0F => "Dialogue Event",
				0x10 => "Motion Bus",
				0x11 => "Motion FX",
				0x12 => "Effect",
				0x13 => "FxCustom",
				0x14 => "Auxiliary Bus",
				_ => $"Unknown (0x{Type:x2})"
			};
		}

		public NodeBaseParams NodeBaseParams { get; protected set; }

		public HircObject(HircSection section, byte type, BinaryReader reader) {
			Section = section;
			Offset = reader.BaseStream.Position;
			Type = type;
			Size = reader.ReadUInt32();
			Id = reader.ReadUInt32();
			StringId = type switch // Only try to dehash names for reversable types
			{
				0x04 => HashList.DehashId(Id),
				0x08 => HashList.DehashId(Id),
				_ => null
			};
			Read(reader, (int)Size - sizeof(UInt32));
		}

		public virtual void Read(BinaryReader reader, int amount) {
			Data = reader.ReadBytes(amount);
		}

		public virtual void Write(BinaryWriter writer) {
			writer.Write(Type);
			writer.Write(Data.Length + sizeof(UInt32));
			writer.Write(Id);
			writer.Write(Data);
		}

		public virtual Dictionary<string, string> DisplayProperties() {
			return new Dictionary<string, string>() {
				{ "ID", Id.ToString() },
				{ "Type", TypeName }
			};
		}
	}
}