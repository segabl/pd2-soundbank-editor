using System;
using System.IO;

namespace PD2SoundBankEditor {
	public class DataSection : BankSection {
		public DataSection(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

		protected override void Read(BinaryReader reader, int amount) {
			foreach (var info in SoundBank.StreamInfos) {
				reader.BaseStream.Seek(DataOffset + info.Offset, SeekOrigin.Begin);
				var data = reader.ReadBytes(info.Data.Length);
				Array.Copy(data, info.Data, data.Length);
			}
			if (reader.BaseStream.Position != DataOffset + amount) {
				throw new FileFormatException("Soundbank data is malformed.");
			}
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());
			foreach (var info in SoundBank.StreamInfos) {
				var padding = info.Offset - dataWriter.BaseStream.Position;
				if (padding > 0) {
					dataWriter.Write(new byte[padding]);
				}
				dataWriter.Write(info.Data);
			}
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}