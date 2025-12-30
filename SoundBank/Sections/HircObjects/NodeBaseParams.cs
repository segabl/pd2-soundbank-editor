using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class NodeBaseParams {
		public byte OverrideParentEffects;
		public byte EffectBitMask;
		public List<(byte, uint)> Effects = new();
		public uint OutputBus;
		public uint ParentObject;
		public byte OverrideParentPriority;
		public byte PriorityDistanceFactorEnabled;
		public SortedDictionary<byte, float> Properties1 = new();
		public SortedDictionary<byte, float> Properties2 = new();
		public byte ByVector;
		public byte Is2DPositioningAvailable;
		public byte Is3DPositioningAvailable;
		public byte[] PositioningParams2D;
		public byte[] PositioningParams3D;
		public byte[] AuxParams;
		public byte VirtualQueueBehaviour;
		public byte KillNewest;
		public byte UseVirtualBehavior;
		public ushort MaxNumInstance;
		public byte[] UnhandledSettings;
		public List<StateChunk> StateChunks = new();
		public List<RTPC> RTPCs = new();

		public NodeBaseParams(BinaryReader reader) {
			OverrideParentEffects = reader.ReadByte();
			var numEffects = reader.ReadByte();
			if (numEffects > 0) {
				EffectBitMask = reader.ReadByte();
				for (var i = 0; i < numEffects; i++) {
					var index = reader.ReadByte();
					var id = reader.ReadUInt32();
					Effects.Add((index, id));
					reader.ReadBytes(2); // 2 zero bytes
				}
			}

			OutputBus = reader.ReadUInt32();
			ParentObject = reader.ReadUInt32();

			OverrideParentPriority = reader.ReadByte();
			PriorityDistanceFactorEnabled = reader.ReadByte();

			var numProperties1 = reader.ReadByte();
			if (numProperties1 > 0) {
				var propertyTypes = new byte[numProperties1];
				for (var i = 0; i < numProperties1; i++) {
					propertyTypes[i] = reader.ReadByte();
				}

				for (var i = 0; i < numProperties1; i++) {
					var type = propertyTypes[i];
					var value = reader.ReadSingle();
					Properties1[type] = value;
				}
			}

			var numProperties2 = reader.ReadByte();
			if (numProperties2 > 0) {
				var propertyTypes = new byte[numProperties2];
				for (var i = 0; i < numProperties2; i++) {
					propertyTypes[i] = reader.ReadByte();
				}

				for (var i = 0; i < numProperties2; i++) {
					var type = propertyTypes[i];
					var value = reader.ReadSingle();
					Properties2[type] = value;
				}
			}

			ByVector = reader.ReadByte();
			if (ByVector > 0) {
				Is2DPositioningAvailable = reader.ReadByte();
				Is3DPositioningAvailable = reader.ReadByte();
				if (Is2DPositioningAvailable > 0) {
					PositioningParams2D = reader.ReadBytes(1);
				}
				if (Is3DPositioningAvailable > 0) {
					PositioningParams3D = reader.ReadBytes(10);
				}
			}

			AuxParams = reader.ReadBytes(4);

			VirtualQueueBehaviour = reader.ReadByte();
			KillNewest = reader.ReadByte();
			UseVirtualBehavior = reader.ReadByte();
			MaxNumInstance = reader.ReadUInt16();

			UnhandledSettings = reader.ReadBytes(8);

			var numStateChunks = reader.ReadUInt32();
			for (var i = 0; i < numStateChunks; i++) {
				StateChunks.Add(new StateChunk(reader));
			}

			var numRTPC = reader.ReadUInt16();
			for (var i = 0; i < numRTPC; i++) {
				RTPCs.Add(new RTPC(reader));
			}
		}

		public void Write(BinaryWriter writer) {
			writer.Write(OverrideParentEffects);
			writer.Write((byte)Effects.Count);
			if (Effects.Count > 0) {
				writer.Write(EffectBitMask);
				foreach (var (index, id) in Effects) {
					writer.Write(index);
					writer.Write(id);
					writer.Write((ushort)0);
				}
			}

			writer.Write(OutputBus);
			writer.Write(ParentObject);
			writer.Write(OverrideParentPriority);
			writer.Write(PriorityDistanceFactorEnabled);
			writer.Write((byte)Properties1.Count);
			foreach (var (type, _) in Properties1) {
				writer.Write(type);
			}

			foreach (var (_, value) in Properties1) {
				writer.Write(value);
			}

			writer.Write((byte)Properties2.Count);
			foreach (var (type, _) in Properties2) {
				writer.Write(type);
			}

			foreach (var (_, value) in Properties2) {
				writer.Write(value);
			}

			writer.Write(ByVector);
			if (ByVector > 0) {
				writer.Write(Is2DPositioningAvailable);
				writer.Write(Is3DPositioningAvailable);
				if (Is2DPositioningAvailable > 0) {
					writer.Write(PositioningParams2D);
				}
				if (Is3DPositioningAvailable > 0) {
					writer.Write(PositioningParams2D);
				}
			}

			writer.Write(AuxParams);
			writer.Write(VirtualQueueBehaviour);
			writer.Write(KillNewest);
			writer.Write(UseVirtualBehavior);
			writer.Write(MaxNumInstance);

			writer.Write(UnhandledSettings);

			writer.Write((uint)StateChunks.Count);
			foreach (var chunk in StateChunks) {
				chunk.Write(writer);
			}

			writer.Write((ushort)RTPCs.Count);
			foreach (var rtpc in RTPCs) {
				rtpc.Write(writer);
			}
		}

		public Dictionary<string, string> DisplayProperties() {
			var properties = new Dictionary<string, string>() {
				{ "Max Instances", MaxNumInstance.ToString() }
			};

			var propList = new List<string>();
			foreach (var prop in Properties1.Concat(Properties2)) {
				propList.Add(prop.Key switch {
					0x00 => $"Volume: {prop.Value}",
					0x05 => $"Priority: {prop.Value}",
					0x06 => $"Prio. Dist. Offset: {prop.Value}",
					_ => $"Unknown (0x{prop.Key:x2}): {prop.Value}"
				});
			}

			properties.Add("Properties", string.Join("\n", propList));

			return properties;
		}
	}
}