using System;
using System.IO;
using System.Linq;
using FinalDefense.Contracts;

namespace FinalDefense.Persistence
{
    public sealed class AtomicCampaignStore : ICampaignStore
    {
        private readonly string root; private readonly IDataCodec codec;
        public AtomicCampaignStore(string root, IDataCodec codec) { this.root = System.IO.Path.GetFullPath(root); this.codec = codec; Directory.CreateDirectory(this.root); }
        public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 80 && id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        private string FileFor(string id) { if (!ValidId(id)) throw new ArgumentException("Invalid save ID"); return System.IO.Path.Combine(root, id + ".json"); }
        public void Write(CampaignSnapshot snapshot)
        {
            if (snapshot == null || snapshot.version != 3) throw new ArgumentException("Unsupported save version");
            WriteAtomic(FileFor(snapshot.saveId), codec.Encode(snapshot));
        }
        public static void WriteAtomic(string path, string text)
        {
            string temp = path + ".pending";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) { writer.Write(text); writer.Flush(); file.Flush(true); }
            // Replace preserves the last committed file as backup and never exposes a half-written primary.
            if (File.Exists(path)) File.Replace(temp, path, path + ".backup"); else File.Move(temp, path);
        }
        public CampaignSnapshot Read(string saveId)
        {
            var value = codec.Decode<CampaignSnapshot>(File.ReadAllText(FileFor(saveId)));
            if (value == null || value.version != 3) throw new NotSupportedException("Unsupported save version");
            if (value.saveId != saveId) throw new InvalidDataException("Save identity mismatch");
            return value;
        }
        // Explicit recovery only: unknown versions are never silently overwritten by an older backup.
        public CampaignSnapshot ReadBackup(string saveId)
        {
            var value = codec.Decode<CampaignSnapshot>(File.ReadAllText(FileFor(saveId) + ".backup"));
            if (value == null || value.version != 3 || value.saveId != saveId) throw new InvalidDataException("Invalid backup");
            return value;
        }
        public SaveMetadata[] List() => Directory.GetFiles(root, "*.json").Where(p => System.IO.Path.GetFileNameWithoutExtension(p) != "settings" && System.IO.Path.GetFileNameWithoutExtension(p) != "unlocks").OrderBy(p => File.GetLastWriteTimeUtc(p)).ThenBy(p => p, StringComparer.Ordinal).Select(p =>
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(p);
            try { var s = Read(id); return new SaveMetadata { saveId = id, runId = s.runId, day = s.day, gpa = s.gpa, phase = s.phase, lastSavedUtc = File.GetLastWriteTimeUtc(p).ToString("O") }; }
            catch { return new SaveMetadata { saveId = id, error = "存档损坏或版本不支持；可尝试读取备份" }; }
        }).ToArray();
        public void SaveSettings(GameSettings value)
        {
            if (value == null || value.version != 1 || float.IsNaN(value.music) || float.IsNaN(value.sound) || value.music < 0 || value.music > 1 || value.sound < 0 || value.sound > 1 || value.width < 320 || value.height < 240) throw new ArgumentException("Invalid settings");
            WriteAtomic(System.IO.Path.Combine(root, "settings.json"), codec.Encode(value));
        }
        public GameSettings LoadSettings()
        {
            string path = System.IO.Path.Combine(root, "settings.json"); if (!File.Exists(path)) return new GameSettings();
            var s = codec.Decode<GameSettings>(File.ReadAllText(path));
            if (s == null || s.version != 1 || float.IsNaN(s.music) || float.IsNaN(s.sound) || s.music < 0 || s.music > 1 || s.sound < 0 || s.sound > 1 || s.width < 320 || s.height < 240) throw new InvalidDataException("Invalid settings");
            return s;
        }
        public UnlockData LoadUnlocks()
        {
            string path = System.IO.Path.Combine(root, "unlocks.json"); if (!File.Exists(path)) return new UnlockData();
            var u = codec.Decode<UnlockData>(File.ReadAllText(path)); if (u == null || u.version != 1 || u.endings == null) throw new InvalidDataException("Invalid unlocks"); return u;
        }
        public void RecordUnlock(string ending)
        {
            if (string.IsNullOrWhiteSpace(ending)) return;
            var u = LoadUnlocks(); if (u.endings.Contains(ending)) return;
            u.endings = u.endings.Concat(new[] { ending }).ToArray(); WriteAtomic(System.IO.Path.Combine(root, "unlocks.json"), codec.Encode(u));
        }
        public void ArchiveSlot(string saveId)
        {
            string path = FileFor(saveId); if (!File.Exists(path)) return;
            // Keep a permanent copy in addition to the rolling previous-transaction backup.
            string archive = path + ".archive-" + Guid.NewGuid().ToString("N");
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { source.CopyTo(target); target.Flush(true); }
        }
        public void PreserveLegacy(string json)
        {
            string path = System.IO.Path.Combine(root, "legacy-" + Guid.NewGuid().ToString("N") + ".readonly");
            using (var f = new StreamWriter(new FileStream(path, FileMode.CreateNew))) f.Write(json);
        }
    }
}
