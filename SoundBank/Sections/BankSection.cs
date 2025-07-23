using System.IO;
using System.Text;

namespace PD2SoundBankEditor {
	public class BankSection {
		public static BankSection Read(SoundBank soundBank, BinaryReader reader) {
			var name = Encoding.ASCII.GetString(reader.ReadBytes(4));
			return name switch {
				"BKHD" => new BkhdSection(soundBank, name, reader),
				"DIDX" => new DidxSection(soundBank, name, reader),
				"DATA" => new DataSection(soundBank, name, reader),
				"HIRC" => new HircSection(soundBank, name, reader),
				"STID" => new StidSection(soundBank, name, reader),
				_ => new BankSection(soundBank, name, reader)
			};
		}

		public SoundBank SoundBank { get; protected set; }
		public string Name { get; protected set; }
		public byte[] Data { get; set; }
		public long DataOffset { get; protected set; }

		public BankSection(SoundBank soundBank, string name, BinaryReader reader) {
			SoundBank = soundBank;
			Name = name;
			var length = reader.ReadInt32();
			DataOffset = reader.BaseStream.Position;
			Read(reader, length);
		}

		protected virtual void Read(BinaryReader reader, int amount) {
			Data = reader.ReadBytes(amount);
			if (reader.BaseStream.Position != DataOffset + amount) {
				throw new FileFormatException("Soundbank data is malformed.");
			}
		}

		public virtual void Write(BinaryWriter writer) {
			writer.Write(Encoding.ASCII.GetBytes(Name));
			writer.Write(Data.Length);
			DataOffset = writer.BaseStream.Position;
			writer.Write(Data);
		}
	}
}