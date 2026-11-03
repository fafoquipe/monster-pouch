using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Local deck choices, never ownership or commercial authority.</summary>
    public sealed class LocalDeckStore
    {
        public const int CurrentVersion = 4;
        [Serializable] sealed class Envelope { public int Version; public string Payload, Sha256; }
        [Serializable] sealed class StarterApplication
        {
            public string ProfileId, ReceiptId, MonsterId;
            public string[] WhelpIds;
            public long Revision;
        }
        [Serializable] sealed class Record
        {
            public int Version;
            public long Revision;
            public string DecksJson, OriginalLegacyJson, OriginalLegacyMonster;
            public string[] OriginalLegacyWhelps;
            public StarterApplication[] StarterApplications;
            public string InventoryProfileId;
        }
        public enum SaveResult { Saved, Unchanged, Conflict, RecoveryRequired, UnavailableCharacters, IncompleteRoster }
        public enum StarterApplyResult { Applied, AlreadyApplied, Conflict, RecoveryRequired, UnavailableCharacters, InvalidSelection }
        readonly string directory, primary, backup;
        public long Revision { get; private set; }
        public bool NeedsRecovery { get; private set; }
        public bool NeedsCatalogue { get; private set; }
        public string InventoryProfileId { get; private set; }
        public bool HasConfirmedInventoryRoster(string profileId)=>!string.IsNullOrEmpty(profileId)&&InventoryProfileId==profileId;

        public LocalDeckStore(string deckDirectory)
        {
            directory=Path.GetFullPath(deckDirectory);
            primary=Path.Combine(directory,"decks.json");
            backup=Path.Combine(directory,"decks.backup.json");
        }
        public DeckLibrary LoadOrImport(string legacyJson,MatchConfig catalogue,string legacyMonster,IEnumerable<string> legacyWhelps)
        {
            if(catalogue==null)throw new ArgumentNullException(nameof(catalogue));
            using(Lock())
            {
                var record=LoadExisting();
                if(record==null)
                {
                    var whelps=(legacyWhelps??Array.Empty<string>()).ToArray();
                    // A damaged legacy source is not silently replaced by a repaired deck.
                    string json=string.IsNullOrWhiteSpace(legacyJson)?
                        DeckLibrary.Restore(null,catalogue,legacyMonster,whelps).Serialize():legacyJson;
                    ReadDecks(json);
                    record=new Record{Version=CurrentVersion,Revision=0,DecksJson=json,OriginalLegacyJson=legacyJson??"",
                        OriginalLegacyMonster=legacyMonster,OriginalLegacyWhelps=whelps,StarterApplications=Array.Empty<StarterApplication>()};
                    Write(record);
                }
                Revision=record.Revision;InventoryProfileId=record.InventoryProfileId;
                NeedsCatalogue=!FitsCatalogue(ReadDecks(record.DecksJson),catalogue);
                // Temporary catalogue gaps only affect this playable view. Original IDs
                // remain on disk, and saving is disabled until they can all be resolved.
                return DeckLibrary.Restore(record.DecksJson,catalogue,legacyMonster,legacyWhelps);
            }
        }
        public SaveResult Save(DeckLibrary library,long expectedRevision,MatchConfig catalogue,IEnumerable<string> availableWhelps=null,string inventoryProfileId=null,IEnumerable<string> availableMonsters=null)
        {
            if(library==null||catalogue==null)throw new ArgumentNullException(library==null?nameof(library):nameof(catalogue));
            if(inventoryProfileId!=null&&(availableWhelps==null||!Guid.TryParseExact(inventoryProfileId,"N",out _)))
                throw new ArgumentException("Inventory confirmation requires a valid guest profile and its available Whelps.");
            string requested=library.Serialize();
            var candidate=ReadDecks(requested);
            if((availableWhelps!=null||availableMonsters!=null)&&!candidate.CanPlayAll(catalogue,availableWhelps,out _,availableMonsters))return SaveResult.IncompleteRoster;
            using(Lock())
            {
                var record=LoadExisting();
                if(record==null)throw new InvalidOperationException("Load decks before saving.");
                Revision=record.Revision;InventoryProfileId=record.InventoryProfileId;
                if(NeedsRecovery)return SaveResult.RecoveryRequired;
                NeedsCatalogue=!FitsCatalogue(ReadDecks(record.DecksJson),catalogue);
                if(NeedsCatalogue||!FitsCatalogue(candidate,catalogue))return SaveResult.UnavailableCharacters;
                // A previous attempt may already have committed. An identical retry
                // does not rotate backups or increment the revision.
                if(ReadDecks(record.DecksJson).Serialize()==requested&&
                    (inventoryProfileId==null||record.InventoryProfileId==inventoryProfileId))return SaveResult.Unchanged;
                if(record.Revision!=expectedRevision)return SaveResult.Conflict;
                record.DecksJson=requested;record.Revision=checked(record.Revision+1);
                if(inventoryProfileId!=null)record.InventoryProfileId=inventoryProfileId;
                Write(record);Revision=record.Revision;InventoryProfileId=record.InventoryProfileId;
                return SaveResult.Saved;
            }
        }
        /// <summary>
        /// Applies the guest's persisted starter receipt once. Deck zero and the
        /// marker commit together; existing extra slots and later edits survive.
        /// Call LoadOrImport first. This never grants ownership or currency.
        /// </summary>
        public StarterApplyResult ApplyStarterSelection(string profileId,StarterSelection selection,long expectedRevision,MatchConfig catalogue)
        {
            if(!Guid.TryParseExact(profileId,"N",out _)||selection==null||
                !DateTime.TryParse(selection.TimestampUtc,System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,out _)||selection.Deck==null||
                !StarterRoster.IsApprovedComposition(selection.ReceiptId,selection.Deck.Monster,selection.Deck.Whelps))
                return StarterApplyResult.InvalidSelection;
            using(Lock())
            {
                var record=LoadExisting();
                if(record==null)throw new InvalidOperationException("Load decks before applying the starter selection.");
                Revision=record.Revision;InventoryProfileId=record.InventoryProfileId;
                if(NeedsRecovery)return StarterApplyResult.RecoveryRequired;
                var previous=record.StarterApplications.FirstOrDefault();
                if(previous!=null)
                    return previous.ProfileId==profileId&&previous.ReceiptId==selection.ReceiptId&&
                        previous.MonsterId==selection.Deck.Monster&&previous.WhelpIds.SequenceEqual(selection.Deck.Whelps,StringComparer.Ordinal)?
                        StarterApplyResult.AlreadyApplied:StarterApplyResult.Conflict;
                // Even a fresh caller must not replace a deck customized before
                // the starter UI became available, or edited by another session.
                if(record.Revision!=0||record.Revision!=expectedRevision)return StarterApplyResult.Conflict;
                var library=ReadDecks(record.DecksJson);
                NeedsCatalogue=catalogue==null||!FitsCatalogue(library,catalogue);
                if(NeedsCatalogue||!StarterRoster.TryBuildReceipt(selection,catalogue,out var starterDeck))
                    return StarterApplyResult.UnavailableCharacters;
                var whelpSlots=new string[MatchConfig.MaxWhelpTypes];
                Array.Copy(starterDeck.Whelps,whelpSlots,starterDeck.Whelps.Length);
                library.Decks[0]=new SavedDeck{Monster=starterDeck.Monster,Whelps=whelpSlots};library.Active=0;
                record.DecksJson=library.Serialize();record.Revision=1;record.InventoryProfileId=profileId;
                record.StarterApplications=new[]{new StarterApplication{
                    ProfileId=profileId,ReceiptId=selection.ReceiptId,MonsterId=starterDeck.Monster,
                    WhelpIds=(string[])starterDeck.Whelps.Clone(),Revision=record.Revision
                }};
                Write(record);Revision=record.Revision;InventoryProfileId=record.InventoryProfileId;
                return StarterApplyResult.Applied;
            }
        }
        FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory,"decks.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        Record LoadExisting()
        {
            NeedsRecovery=false;NeedsCatalogue=false;
            if(!File.Exists(primary)&&!File.Exists(backup))return null;
            try{return Read(primary);}
            catch(NotSupportedException){throw;}
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is ArgumentException)
            {
                if(!File.Exists(backup))throw new InvalidDataException("Decks could not be read. Original data was preserved.",error);
                var record=Read(backup);NeedsRecovery=true;return record;
            }
        }
        static Record Read(string path)
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null||envelope.Version<1)throw new InvalidDataException("Missing deck envelope.");
            if(envelope.Version!=1)throw new NotSupportedException("Unsupported deck envelope version.");
            if(string.IsNullOrEmpty(envelope.Payload)||envelope.Sha256!=Hash(envelope.Payload))
                throw new InvalidDataException("Deck checksum mismatch.");
            var record=new Record{Revision=-1};
            JsonUtility.FromJsonOverwrite(envelope.Payload,record);
            if(record==null||record.Version<1||record.Revision<0)throw new InvalidDataException("Invalid deck record.");
            if(record.Version>CurrentVersion)throw new NotSupportedException("Unsupported deck record version.");
            if(record.Version<3&&(!string.IsNullOrEmpty(record.InventoryProfileId)||
                record.StarterApplications!=null&&record.StarterApplications.Any(a=>a!=null&&a.ReceiptId!=StarterRoster.LegacyReceiptId)))
                throw new InvalidDataException("An older deck record cannot contain current inventory or starter receipts.");
            if(record.Version==1)
            {
                if(record.StarterApplications!=null&&record.StarterApplications.Length!=0)
                    throw new InvalidDataException("A v1 deck record cannot contain starter applications.");
                record.StarterApplications=Array.Empty<StarterApplication>();
                record.Version=CurrentVersion;
            }
            ValidateStarterApplications(record);
            record.Version=CurrentVersion;
            ReadDecks(record.DecksJson);
            return record;
        }
        static void ValidateStarterApplications(Record record)
        {
            if(!string.IsNullOrEmpty(record.InventoryProfileId)&&!Guid.TryParseExact(record.InventoryProfileId,"N",out _))
                throw new InvalidDataException("Invalid inventory roster confirmation.");
            if(record.StarterApplications==null||record.StarterApplications.Length>1)
                throw new InvalidDataException("Invalid starter application records.");
            foreach(var application in record.StarterApplications)
                if(application==null||!Guid.TryParseExact(application.ProfileId,"N",out _)||
                    application.Revision!=1||application.Revision>record.Revision||application.WhelpIds==null||
                    !StarterRoster.IsApprovedComposition(application.ReceiptId,application.MonsterId,application.WhelpIds))
                    throw new InvalidDataException("Invalid starter application receipt.");
        }
        static DeckLibrary ReadDecks(string json)
        {
            if(string.IsNullOrWhiteSpace(json))throw new InvalidDataException("Missing deck data.");
            var library=new DeckLibrary{Active=-1,Decks=null};
            JsonUtility.FromJsonOverwrite(json,library);
            // Accept the old three-deck payload without rewriting it on load.
            // Restore expands the playable view; the next explicit save commits v4 atomically.
            if(library.Decks==null||(library.Decks.Length!=3&&library.Decks.Length!=DeckLibrary.DeckCount)||
                library.Active<0||library.Active>=library.Decks.Length)
                throw new InvalidDataException("Invalid deck library.");
            foreach(var deck in library.Decks)
            {
                if(deck==null||deck.Whelps==null||deck.Whelps.Length!=MatchConfig.MaxWhelpTypes)
                    throw new InvalidDataException("Incomplete deck.");
                if(deck.IsEmpty)continue;
                if(string.IsNullOrWhiteSpace(deck.Monster))throw new InvalidDataException("Missing deck hero.");
                var ids=deck.Whelps.Where(id=>!string.IsNullOrEmpty(id)).ToArray();
                if(ids.Length==0||ids.Any(string.IsNullOrWhiteSpace)||ids.Contains(deck.Monster)||
                    ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)
                    throw new InvalidDataException("Invalid deck IDs.");
            }
            return library;
        }
        static bool FitsCatalogue(DeckLibrary library,MatchConfig catalogue)=>
            library.Decks.All(deck=>deck.IsEmpty || catalogue.Get(deck.Monster)?.IsMonster==true&&
                deck.Whelps.Where(id=>!string.IsNullOrEmpty(id)).All(id=>catalogue.Get(id)?.IsMonster==false)&&
                deck.Whelps.Skip(catalogue.SelectionLimit).All(string.IsNullOrEmpty));
        void Write(Record record)
        {
            ValidateStarterApplications(record);
            ReadDecks(record.DecksJson);
            string payload=JsonUtility.ToJson(record);
            string encoded=JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=Hash(payload)},true);
            string temporary=Path.Combine(directory,"decks."+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    var bytes=Encoding.UTF8.GetBytes(encoded);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                if(File.Exists(primary))File.Replace(temporary,primary,backup);
                else File.Move(temporary,primary);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        static string Hash(string value)
        {
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
    }
}
