using System.IO;

namespace PD2SoundBankEditor {
	public class DidxSection : BankSection {
		public DidxSection(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

		protected override void Read(BinaryReader reader, int amount) {
			for (var i = 0; i < amount; i += 12) {
				var id = reader.ReadUInt32();
				var offset = reader.ReadUInt32();
				var length = reader.ReadUInt32();
				SoundBank.StreamInfos.Add(new StreamInfo(SoundBank, id, (int)offset, (int)length));
			}

			if (reader.BaseStream.Position != DataOffset + amount) {
				throw new FileFormatException("Soundbank data is malformed.");
			}
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());
			var totalDataSize = 0;
			foreach (var info in SoundBank.StreamInfos) {
				var align = 16 - (totalDataSize % 16); // pad to nearest 16
				if (align < 16) {
					totalDataSize += align;
				}

				info.Offset = totalDataSize;

				dataWriter.Write(info.Id);
				dataWriter.Write(info.Offset);
				dataWriter.Write(info.Data.Length);

				totalDataSize += info.Data.Length;
			}

			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}