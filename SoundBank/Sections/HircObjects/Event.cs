using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class Event : HircObject {
		public uint ActionNumber;
		public List<uint> ActionIDs = new();
        
		public byte[] Unhandled;

		public Event(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

            ActionNumber = reader.ReadUInt32();
			for (var i = 0; i < ActionNumber; i++)
			{
                ActionIDs.Add(reader.ReadUInt32());
			}

            Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(ActionNumber);
			for (var i = 0; i < ActionNumber; i++)
			{
				dataWriter.Write(ActionIDs[i]);
			}

			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}