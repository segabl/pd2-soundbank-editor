using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class Event : HircObject {
		public List<uint> ActionIDs = new();
		public byte[] Unhandled;

		public Event(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			var numActions = reader.ReadUInt32();
			for (var i = 0; i < numActions; i++) {
				ActionIDs.Add(reader.ReadUInt32());
			}

			Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write((uint)ActionIDs.Count);
			foreach (var actionId in ActionIDs) {
				dataWriter.Write(actionId);
			}

			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}

		public override Dictionary<string, string> DisplayProperties() {
			var properties = base.DisplayProperties();

			if (ActionIDs.Count > 0) {
				foreach (var actionId in ActionIDs) {
					properties.Add("Action ID", actionId.ToString());
				}
			}

			return properties;
		}
	}
}