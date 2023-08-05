using System.IO;

namespace PD2SoundBankEditor {
	public class HircObject {
		public static HircObject Read(HircSection section, BinaryReader reader) {
			var type = reader.ReadByte();
			return type switch {
				2 => new Sound(section, type, reader),
				7 => new ActorMixer(section, type, reader),
				_ => new HircObject(section, type, reader)
			};
		}

		public HircSection Section { get; protected set; }
		public byte Type { get; protected set; }
		public byte[] Data { get; protected set; }

		public HircObject(HircSection section, byte type, BinaryReader reader) {
			Section = section;
			Type = type;
			var length = reader.ReadInt32();
			Read(reader, length);
		}

		public virtual void Read(BinaryReader reader, int amount) {
			Data = reader.ReadBytes(amount);
		}

		public virtual void Write(BinaryWriter writer) {
			writer.Write(Type);
			writer.Write(Data.Length);
			writer.Write(Data);
		}
	}
}