using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class ActorMixer : HircObject {
		public List<uint> Children = new();

		public ActorMixer(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			NodeBaseParams = new(reader);

			var numChildren = reader.ReadUInt32();
			for (var i = 0; i < numChildren; i++) {
				Children.Add(reader.ReadUInt32());
			}
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			NodeBaseParams.Write(dataWriter);

			dataWriter.Write((uint)Children.Count);
			foreach (var child in Children) {
				dataWriter.Write(child);
			}

			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}

		public override Dictionary<string, string> DisplayProperties() {
			var properties = base.DisplayProperties();

			foreach (var prop in NodeBaseParams.DisplayProperties()) {
				properties.Add(prop.Key, prop.Value);
			}

			properties.Add("Children", string.Join("\n", Children));

			return properties;
		}
	}
}