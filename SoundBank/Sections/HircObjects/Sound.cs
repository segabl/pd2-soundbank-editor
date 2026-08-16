using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace PD2SoundBankEditor {
	public class Sound : HircObject {
		public uint PluginId { get; protected set; }
		public byte StreamType { get; protected set; }
		public uint SourceId { get; protected set; }
		public uint FileSize { get; protected set; }
		public byte SourceBits { get; protected set; }
		public uint PluginSize { get; protected set; }
        public byte[] PluginData { get; protected set; }
        public byte[] Unhandled { get; protected set; }

		public string StreamTypeName {
			get => StreamType switch {
				0 => "Embedded",
				1 => "Prefetch Streamed",
				2 => "Streamed",
				_ => $"Unknown (0x{StreamType:x2})"
			};
		}

		public Sound(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			PluginId = reader.ReadUInt32();
			StreamType = reader.ReadByte(); // 0 = embedded, 1 = prefetch, 2 = streamed
			SourceId = reader.ReadUInt32();
            FileSize = reader.ReadUInt32();

			if (StreamType == 0) {
				var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
				if (streamInfo != null) {
					streamInfo.HasReferences = true;
				}
			}

			SourceBits = reader.ReadByte(); // 0 = sfx, 1 = voice

			var pluginType = PluginId & 0xF;
			if (pluginType == 2 || pluginType == 5) { // Source or MotionSource
				PluginSize = reader.ReadUInt32();
				PluginData = reader.ReadBytes((int)PluginSize);
			}

			NodeBaseParams = new(reader);

            Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(PluginId);
			dataWriter.Write(StreamType);
			dataWriter.Write(SourceId);

			if (StreamType == 0) {
				var streamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
				if (streamInfo != null) {
					FileSize = (uint)streamInfo.Data.Length;
				}
			}

			dataWriter.Write(FileSize);

			dataWriter.Write(SourceBits);

            var pluginType = PluginId & 0xF;
            if (pluginType == 2 || pluginType == 5) // Source or MotionSource
            { 
                dataWriter.Write(PluginSize);
                dataWriter.Write(PluginData);
            }

			NodeBaseParams.Write(dataWriter);
			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}

		public override Dictionary<string, string> DisplayProperties() {
			var properties = base.DisplayProperties();

			properties.Add("Sound Type", StreamTypeName);
			properties.Add("Sound ID", SourceId.ToString());

			foreach (var prop in NodeBaseParams.DisplayProperties()) {
				properties.Add(prop.Key, prop.Value);
			}

			return properties;
		}
	}
}