using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace PD2SoundBankEditor
{
    public static class HashList
    {
        static private Dictionary<uint, string> MatchTable = new Dictionary<uint, string>();

        static HashList()
        {
            var hashlistFile = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "hashlist");
            if (!File.Exists(hashlistFile))
            {
                MessageBox.Show("ID hashlist not found in the application's directory; event names will be unavailable.", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            List<string> strings = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(hashlistFile));
            foreach (string s in strings)
            {
                MatchTable[FNVHash(s)] = s;
            }
        }

        static private uint FNVHash(string name)
        {
            var namebytes = Encoding.UTF8.GetBytes(name.ToLower());
            var hash = 2166136261; // FNV initial offset

            foreach (byte namebyte in namebytes)
            {
                hash = hash * 16777619; // FNV prime
                hash = hash ^ namebyte;
                hash = hash & 0xFFFFFFFF;
            }

            return hash;
        }

        static public string DehashId(uint id)
        {
            string stringId;
            MatchTable.TryGetValue(id, out stringId);

            return stringId;
        }
    }
}
