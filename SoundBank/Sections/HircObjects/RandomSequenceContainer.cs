using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class RandomSequenceContainer : HircObject {
		public ushort LoopCount;
		public ushort LoopModMin;
		public ushort LoopModMax;
		public float TransitionTime;
		public float TransitionTimeModMin;
		public float TransitionTimeModMax;
		public ushort AvoidRepeatCount;
		public byte TransitionMode;
		public byte RandomMode;
		public byte Mode;
		public byte[] UnhandledSettings;
		public List<uint> Children = new();
		public List<Tuple<uint, int>> Playlist = new();
		public byte[] Unhandled;

		public bool Broken;

		public RandomSequenceContainer(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			NodeBaseParams = new(reader);

			LoopCount = reader.ReadUInt16();
			LoopModMin = reader.ReadUInt16();
			LoopModMax = reader.ReadUInt16();
			TransitionTime = reader.ReadSingle();
			TransitionTimeModMin = reader.ReadSingle();
			TransitionTimeModMax = reader.ReadSingle();
			AvoidRepeatCount = reader.ReadUInt16();
			TransitionMode = reader.ReadByte();
			RandomMode = reader.ReadByte();
			Mode = reader.ReadByte();

			UnhandledSettings = reader.ReadBytes(5);

			var numChildren = reader.ReadUInt32();
			var bytesLeft = amount + dataOffset - (int)reader.BaseStream.Position;
			if (numChildren * 4 > bytesLeft) {
				Trace.WriteLine($"{Id} Impossible number of children ({numChildren}) with {bytesLeft} bytes left");
				Broken = true;
			}

			if (!Broken) {
				for (var i = 0; i < numChildren; i++) {
					Children.Add(reader.ReadUInt32());
				}

				var numPlaylistItem = reader.ReadUInt16();
				for (var i = 0; i < numPlaylistItem; i++) {
					Playlist.Add(new(reader.ReadUInt32(), reader.ReadInt32()));
				}
			}

			Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			NodeBaseParams.Write(dataWriter);

			dataWriter.Write(LoopCount);
			dataWriter.Write(LoopModMin);
			dataWriter.Write(LoopModMax);
			dataWriter.Write(TransitionTime);
			dataWriter.Write(TransitionTimeModMin);
			dataWriter.Write(TransitionTimeModMax);
			dataWriter.Write(AvoidRepeatCount);
			dataWriter.Write(TransitionMode);
			dataWriter.Write(RandomMode);
			dataWriter.Write(Mode);

			dataWriter.Write(UnhandledSettings);

			if (!Broken) {
				dataWriter.Write((uint)Children.Count);
				foreach (var child in Children) {
					dataWriter.Write(child);
				}

				dataWriter.Write((ushort)Playlist.Count);
				foreach (var playlistItem in Playlist) {
					dataWriter.Write(playlistItem.Item1);
					dataWriter.Write(playlistItem.Item2);
				}
			}

			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}

		public override Dictionary<string, string> DisplayProperties() {
			var properties = base.DisplayProperties();

			properties.Add("Loop Count", LoopCount.ToString());
			properties.Add("Transition Time", TransitionTime.ToString());
			properties.Add("Avoid Rep. Count", AvoidRepeatCount.ToString());
			properties.Add("Random Mode", RandomMode switch {
				0x00 => "Normal",
				_ => $"Unknown (0x{RandomMode:x2})"
			});
			properties.Add("Mode", Mode switch {
				0x00 => "Random",
				0x01 => "Sequence",
				_ => $"Unknown (0x{Mode:x2})"
			});

			foreach (var prop in NodeBaseParams.DisplayProperties()) {
				properties.Add(prop.Key, prop.Value);
			}

			properties.Add("Playlist", string.Join("\n", Playlist.Select(i => $"{i.Item1} ({i.Item2})")));

			return properties;
		}
	}
}