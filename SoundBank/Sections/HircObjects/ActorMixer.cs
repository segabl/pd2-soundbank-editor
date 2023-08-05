using System.IO;

namespace PD2SoundBankEditor {
	public class ActorMixer : HircObject {
		public uint ObjectId;
		public NodeBaseParams NodeBaseParams;

		public ActorMixer(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			ObjectId = reader.ReadUInt32();

			NodeBaseParams = new NodeBaseParams(reader, amount + dataOffset);
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(ObjectId);
			NodeBaseParams.Write(dataWriter);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}