using System.IO;

namespace PD2SoundBankEditor {
	public class ActorMixer : HircObject {
		public byte[] Unhandled;

		public ActorMixer(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			NodeBaseParams = new(reader);

			Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			NodeBaseParams.Write(dataWriter);
			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}