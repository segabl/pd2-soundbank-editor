using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PD2SoundBankEditor {
	public class StidSection : BankSection {
		public uint Type;
		public SortedDictionary<uint, string> FileNames { get; protected set; } = new();

		public StidSection(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

		protected override void Read(BinaryReader reader, int amount) {
			base.Read(reader, amount);

			var size = BitConverter.ToUInt32(Data, 4);
			var offset = 8;
			for (int i = 0; i < size; i++) {
				var bankId = BitConverter.ToUInt32(Data, offset);
				var stringLength = Data[offset + 4];
				var fileName = Encoding.ASCII.GetString(Data, offset + 5, stringLength);
				FileNames.Add(bankId, fileName);
				offset += 5 + stringLength;
			}
		}

		public override void Write(BinaryWriter writer) {
			base.Write(writer);
		}
	}
}