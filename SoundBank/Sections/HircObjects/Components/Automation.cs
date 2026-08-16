using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace PD2SoundBankEditor
{
    // 3D Automation objects
    public class AutomationPathVertex
    {
        float x;
        float y;
        float z;
        int Duration;

        public AutomationPathVertex(BinaryReader reader)
        {
            x = reader.ReadSingle();
            y = reader.ReadSingle();
            z = reader.ReadSingle();
            Duration = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
            writer.Write(Duration);
        }
    }

    public class AutomationPlaylistItem
    {
        uint VerticesOffset;
        uint NumVertices;

        public AutomationPlaylistItem(BinaryReader reader)
        {
            VerticesOffset = reader.ReadUInt32();
            NumVertices = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(VerticesOffset);
            writer.Write(NumVertices);
        }
    }
    public class AutomationParam3D
    {
        float FXRange;
        float FYRange;
        float FZRange;

        public AutomationParam3D(BinaryReader reader)
        {
            FXRange = reader.ReadSingle();
            FYRange = reader.ReadSingle();
            FZRange = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(FXRange);
            writer.Write(FYRange);
            writer.Write(FZRange);
        }
    }
}