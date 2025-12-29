using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PD2SoundBankEditor {
	public class RTPC {
		uint Id;
		uint Param;
		int CurveId;
		byte Scaling;
		List<Tuple<float, float, uint>> GraphPoints = new();

		public RTPC(BinaryReader reader) {
			Id = reader.ReadUInt32();
			Param = reader.ReadUInt32();
			CurveId = reader.ReadInt32();
			Scaling = reader.ReadByte();

			var numPoints = reader.ReadUInt16();
			for (var i = 0; i < numPoints; i++) {
				GraphPoints.Add(new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadUInt32()));
			}
		}

		public void Write(BinaryWriter writer) {
			writer.Write(Id);
			writer.Write(Param);
			writer.Write(CurveId);
			writer.Write(Scaling);
			writer.Write((ushort)GraphPoints.Count);
			foreach (var state in GraphPoints) {
				writer.Write(state.Item1);
				writer.Write(state.Item2);
				writer.Write(state.Item3);
			}
		}
	}
}