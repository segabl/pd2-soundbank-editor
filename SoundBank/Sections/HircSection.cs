using System.Collections.Generic;
using System.IO;

namespace PD2SoundBankEditor {
	public class HircSection : BankSection {
		public List<Sound> SoundObjects { get; private set; } = new();

		public List<HircObject> Objects { get; protected set; } = new List<HircObject>();

		public HircSection(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

		protected override void Read(BinaryReader reader, int amount) {
			var numObjects = reader.ReadUInt32();
			for (var i = 0; i < numObjects; i++) {
				var obj = HircObject.Read(this, reader);
				Objects.Add(obj);
			}
			if (reader.BaseStream.Position != DataOffset + amount) {
				throw new FileFormatException("Soundbank data is malformed.");
			}
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());
			dataWriter.Write(Objects.Count);
			foreach (var obj in Objects) {
				obj.Write(dataWriter);
			}
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}