using System;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public static partial class AndroidDeveloperContent
    {
        // Profile-scoped: compiling in the editor must not skip a normal fixture's tutorial.
        public static bool IsUnlocked(LocalGuestProfile profile)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return profile != null && profile.DevelopmentMode == DevelopmentAccountMode.AllContent;
#else
            return false;
#endif
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace MonsterPouch.Gameplay.Profiles
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEngine;

    public enum DevelopmentAccountMode { None, AllContent, Fresh }

    public sealed partial class LocalGuestProfile
    {
        // Serialized only in development. Missing on real/older profiles means no override.
        public DevelopmentAccountMode DevelopmentMode;
    }

    public sealed partial class LocalGuestProfileStore
    {
        internal LocalGuestProfile InitializeDevelopmentAccount()
        {
            using (Lock())
            {
                var profile = LoadExisting() ?? throw new InvalidOperationException("Create the isolated profile first.");
                if (NeedsRecovery || profile.DevelopmentMode != DevelopmentAccountMode.None ||
                    profile.Revision != 0 || profile.Starter != null || profile.HasLegacyAccess ||
                    profile.OnboardingStage != GuestOnboardingStage.ChooseStarter)
                    throw new InvalidOperationException("Only a new isolated profile can initialize development mode.");
                profile.DevelopmentMode = DevelopmentAccountMode.Fresh;
                profile.Revision = checked(profile.Revision + 1);
                Save(profile);
                return profile;
            }
        }
    }

    /// <summary>
    /// Development-only account generations. The real GuestProfile/PlayerDecks/PlayerProgress
    /// directories are never opened, imported, moved or removed. A reset publishes its backup
    /// first, then its fully initialized generation, then atomically replaces the active pointer.
    /// </summary>
    public sealed class DevelopmentAccountStore
    {
        public const string FolderName = "DevelopmentAccounts-v1";
        [Serializable] sealed class Pointer { public int Version = 1; public string GenerationId, PreviousGenerationId, BackupId; }
        [Serializable] sealed class Envelope { public int Version = 1; public string Payload, Sha256; }
        [Serializable] sealed class BackupManifest { public int Version = 1; public string GenerationId, RequestId; public BackupEntry[] Files; }
        [Serializable] sealed class BackupEntry { public string Path, Sha256; }
        readonly string root, primary, backup;
        Pointer current;
        public string GenerationId => current?.GenerationId;
        public string AccountDirectory => current == null ? throw new InvalidOperationException("Open the development account first.") : GenerationDirectory(current.GenerationId);
        public LocalGuestProfile Profile { get; private set; }
        public string LastBackupDirectory => string.IsNullOrEmpty(current?.BackupId) ? null : Path.Combine(root, "Backups", current.BackupId);

        public DevelopmentAccountStore(string persistentDirectory)
        {
            root = Path.Combine(Path.GetFullPath(persistentDirectory), FolderName);
            primary = Path.Combine(root, "active.json");
            backup = Path.Combine(root, "active.backup.json");
        }

        public LocalGuestProfile Open(MatchConfig catalogue)
        {
            using (LockAccount())
            {
                current = ReadPointer();
                if (current == null)
                {
                    // A partially created account is evidence to preserve, never permission to reset.
                    if (Directory.Exists(Path.Combine(root, "Generations")) &&
                        Directory.EnumerateDirectories(Path.Combine(root, "Generations")).Any())
                        throw new InvalidDataException("An unfinished development account needs recovery.");
                    var next = new Pointer { GenerationId = Guid.NewGuid().ToString("N") };
                    CreateGeneration(next.GenerationId, catalogue, true);
                    SavePointer(next);
                    current = next;
                }
                return LoadCurrent(catalogue);
            }
        }

        public LocalGuestProfile StartFresh(string expectedGeneration, string requestId, MatchConfig catalogue)
        {
            RequireId(requestId);
            using (LockAccount())
            {
                current = ReadPointer() ?? throw new InvalidOperationException("No development account is open.");
                // A retry after commit does not erase any progress made in the new account.
                if (current.GenerationId == requestId) return LoadCurrent(catalogue);
                if (current.GenerationId != expectedGeneration)
                    throw new InvalidOperationException("The active development account changed. Reopen settings.");
                LoadCurrent(catalogue);
                PublishBackup(requestId);
                string destination = GenerationDirectory(requestId);
                if (!Directory.Exists(destination)) CreateGeneration(requestId, catalogue, false);
                else
                {
                    var prepared = new LocalGuestProfileStore(Path.Combine(destination, "GuestProfile"));
                    var profile = prepared.Reload();
                    if (prepared.NeedsRecovery || profile.DevelopmentMode != DevelopmentAccountMode.Fresh ||
                        profile.OnboardingStage != GuestOnboardingStage.ChooseStarter || profile.Revision != 1)
                        throw new InvalidDataException("The prepared development reset needs recovery.");
                }
                var next = new Pointer { GenerationId = requestId, PreviousGenerationId = current.GenerationId, BackupId = requestId };
                SavePointer(next);
                current = next;
                return LoadCurrent(catalogue);
            }
        }

        public LocalGuestProfile UnlockAll(string expectedGeneration, MatchConfig catalogue)
        {
            using (LockAccount())
            {
                current = ReadPointer() ?? throw new InvalidOperationException("No development account is open.");
                if (current.GenerationId != expectedGeneration)
                    throw new InvalidOperationException("The active development account changed. Reopen settings.");
                LoadCurrent(catalogue);
                Profile = new LocalGuestProfileStore(Path.Combine(AccountDirectory, "GuestProfile")).LoadDeveloperContent(catalogue);
                return Profile;
            }
        }

        LocalGuestProfile LoadCurrent(MatchConfig catalogue)
        {
            var store = new LocalGuestProfileStore(Path.Combine(AccountDirectory, "GuestProfile"));
            var profile = store.Reload();
            if (store.NeedsRecovery) throw new InvalidDataException("The development profile needs recovery; original files were preserved.");
            if (profile.DevelopmentMode != DevelopmentAccountMode.Fresh && profile.DevelopmentMode != DevelopmentAccountMode.AllContent)
                throw new InvalidDataException("The isolated account has no valid development mode.");
            // New catalogue entries are granted only in all-content mode, never after a fresh reset.
            Profile = profile.DevelopmentMode == DevelopmentAccountMode.AllContent ? store.LoadDeveloperContent(catalogue) : profile;
            return Profile;
        }

        void CreateGeneration(string generationId, MatchConfig catalogue, bool allContent)
        {
            var store = new LocalGuestProfileStore(Path.Combine(GenerationDirectory(generationId), "GuestProfile"));
            store.OpenOrCreate(false); // Exact release new-account path: welcome, 0 trophies and starter choice.
            store.InitializeDevelopmentAccount();
            if (allContent) store.LoadDeveloperContent(catalogue);
        }

        void PublishBackup(string requestId)
        {
            string destination = Path.Combine(root, "Backups", requestId);
            if (Directory.Exists(destination))
            {
                var previous = JsonUtility.FromJson<BackupManifest>(File.ReadAllText(Path.Combine(destination, "backup-manifest.json")));
                if (previous == null || previous.Version != 1 || previous.GenerationId != current.GenerationId || previous.RequestId != requestId)
                    throw new InvalidDataException("This reset request belongs to a different generation.");
                VerifyBackup(destination, previous);
                var currentFiles = SafeFiles(AccountDirectory).Where(f => !f.EndsWith(".lock", StringComparison.Ordinal) && !f.EndsWith(".tmp", StringComparison.Ordinal)).ToArray();
                if (currentFiles.Length != previous.Files.Length || previous.Files.Any(f =>
                    !File.Exists(Path.Combine(AccountDirectory, f.Path)) || Hash(File.ReadAllBytes(Path.Combine(AccountDirectory, f.Path))) != f.Sha256))
                    throw new InvalidDataException("The account changed after this backup. Start a new reset request.");
                return;
            }
            string temporary = Path.Combine(root, "Backups", ".pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            var handles = new List<FileStream>();
            try
            {
                // Take the existing stores' locks while snapshotting. No lock files enter the backup.
                foreach (string relative in new[] { "GuestProfile/guest-profile.lock", "PlayerDecks/decks.lock",
                    "PlayerProgress/progress.lock", "DailyGifts/" + Profile.ProfileId + ".json.lock" })
                {
                    string path = Path.Combine(AccountDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    handles.Add(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
                var entries = new List<BackupEntry>();
                foreach (string source in SafeFiles(AccountDirectory))
                {
                    if (source.EndsWith(".lock", StringComparison.Ordinal) || source.EndsWith(".tmp", StringComparison.Ordinal)) continue;
                    string relative = source.Substring(AccountDirectory.Length + 1);
                    byte[] bytes = File.ReadAllBytes(source);
                    WriteDurable(Path.Combine(temporary, relative), bytes);
                    entries.Add(new BackupEntry { Path = relative, Sha256 = Hash(bytes) });
                }
                var manifest = new BackupManifest { GenerationId = current.GenerationId, RequestId = requestId, Files = entries.ToArray() };
                WriteDurable(Path.Combine(temporary, "backup-manifest.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true)));
                VerifyBackup(temporary, manifest);
                Directory.Move(temporary, destination); // Publish the complete backup before changing active.json.
            }
            finally { foreach (var handle in handles) handle.Dispose(); }
            // Interrupted .pending directories are deliberately retained for recovery, never auto-deleted.
        }

        static IEnumerable<string> SafeFiles(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Development backup cannot follow a linked directory.");
            foreach (string file in Directory.GetFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Development backup cannot follow a linked file.");
                yield return file;
            }
            foreach (string child in Directory.GetDirectories(directory))
                foreach (string file in SafeFiles(child)) yield return file;
        }

        static void VerifyBackup(string directory, BackupManifest manifest)
        {
            if (manifest.Files == null || manifest.Files.Length == 0) throw new InvalidDataException("Empty development backup.");
            foreach (var file in manifest.Files)
            {
                string path = Path.GetFullPath(Path.Combine(directory, file.Path));
                if (!path.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    Hash(File.ReadAllBytes(path)) != file.Sha256)
                    throw new InvalidDataException("Development backup verification failed.");
            }
        }

        FileStream LockAccount()
        {
            Directory.CreateDirectory(root);
            return new FileStream(Path.Combine(root, "account.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        string GenerationDirectory(string id) { RequireId(id); return Path.Combine(root, "Generations", id); }
        static void RequireId(string id) { if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid development generation/request id."); }
        Pointer ReadPointer()
        {
            if (!File.Exists(primary))
            {
                if (File.Exists(backup)) throw new InvalidDataException("The development account pointer needs recovery.");
                return null;
            }
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(primary, Encoding.UTF8));
            if (envelope == null || envelope.Version != 1 || string.IsNullOrEmpty(envelope.Payload) || Hash(Encoding.UTF8.GetBytes(envelope.Payload)) != envelope.Sha256)
                throw new InvalidDataException("The development account pointer checksum is invalid.");
            var pointer = JsonUtility.FromJson<Pointer>(envelope.Payload);
            if (pointer == null || pointer.Version != 1) throw new InvalidDataException("Unsupported development account pointer.");
            RequireId(pointer.GenerationId);
            if (!string.IsNullOrEmpty(pointer.BackupId)) RequireId(pointer.BackupId);
            if (!string.IsNullOrEmpty(pointer.PreviousGenerationId)) RequireId(pointer.PreviousGenerationId);
            return pointer;
        }
        void SavePointer(Pointer pointer)
        {
            string payload = JsonUtility.ToJson(pointer);
            string encoded = JsonUtility.ToJson(new Envelope { Payload = payload, Sha256 = Hash(Encoding.UTF8.GetBytes(payload)) }, true);
            string temporary = Path.Combine(root, "active." + Guid.NewGuid().ToString("N") + ".tmp");
            WriteDurable(temporary, Encoding.UTF8.GetBytes(encoded));
            if (File.Exists(primary)) File.Replace(temporary, primary, backup);
            else File.Move(temporary, primary);
        }
        static void WriteDurable(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
#endif
