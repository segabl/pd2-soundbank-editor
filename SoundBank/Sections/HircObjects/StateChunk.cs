using System;
using System.Collections.Generic;
using System.IO;

namespace PD2SoundBankEditor {
	public class StateChunk {
		uint StateGroup;
		byte StateSyncType;
		List<Tuple<uint, uint>> States = new();

		public StateChunk(BinaryReader reader) {
			StateGroup = reader.ReadUInt32();
			StateSyncType = reader.ReadByte();

			var numStates = reader.ReadUInt16();
			for (var i = 0; i < numStates; i++) {
				States.Add(new(reader.ReadUInt32(), reader.ReadUInt32()));
			}
		}

		public void Write(BinaryWriter writer) {
			writer.Write(StateGroup);
			writer.Write(StateSyncType);
			writer.Write((ushort)States.Count);
			foreach (var state in States) {
				writer.Write(state.Item1);
				writer.Write(state.Item2);
			}
		}
	}
}