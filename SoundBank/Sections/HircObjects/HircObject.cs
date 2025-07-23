using System;
using System.IO;

namespace PD2SoundBankEditor {
	public class HircObject {
		public static HircObject Read(HircSection section, BinaryReader reader) {
			var type = reader.ReadByte();
			return type switch {
				0x02 => new Sound(section, type, reader),
				0x07 => new ActorMixer(section, type, reader),
				_ => new HircObject(section, type, reader)
			};
		}

		public HircSection Section { get; protected set; }
		public long Offset { get; protected set; }
		public byte Type { get; protected set; }
		public uint Size { get; protected set; }
		public uint Id { get; protected set; }
		public byte[] Data { get; protected set; }
		public string TypeName {
			get => Type switch {
				0x02 => "Sound",
				0x03 => "Action",
				0x04 => "Event",
				0x05 => "Random/Sequence Container",
				0x06 => "Switch Container",
				0x07 => "Actor Mixer",
				0x0E => "Attenuation",
				0x13 => "FxCustom",
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
	}
}