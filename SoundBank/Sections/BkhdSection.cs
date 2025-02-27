using System;
using System.IO;

namespace PD2SoundBankEditor {
	public class BkhdSection : BankSection {
		public BkhdSection(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

		protected override void Read(BinaryReader reader, int amount) {
			base.Read(reader, amount);

			SoundBank.GeneratorVersion = BitConverter.ToUInt32(Data, 0);
			SoundBank.Id = BitConverter.ToUInt32(Data, 4);
		}

		public override void Write(BinaryWriter writer) {
			base.Write(writer);
		}
	}
}