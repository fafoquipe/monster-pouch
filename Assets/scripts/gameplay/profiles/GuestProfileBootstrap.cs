using System;
using System.IO;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>
    /// Read-only startup probe. Capture before loading
    /// XP or decks: those stores create files for a genuinely new player.
    /// </summary>
    public static class GuestProfileBootstrap
    {
        static readonly string[] LegacyGameKeys={
            "monster-pouch.progress.v1","monster-pouch.decks.v1",
            "monster-pouch.monster","monster-pouch.whelps"
        };
        public static bool CaptureExistingPlayerData(string persistentDirectory)=>
            CaptureExistingPlayerData(persistentDirectory,PlayerPrefs.HasKey,File.Exists);

        // Explicit readers let tests verify the initialization order without
        // touching the user's PlayerPrefs or real persistent-data directory.
        public static bool CaptureExistingPlayerData(string persistentDirectory,Func<string,bool> hasPreference,Func<string,bool> fileExists)
        {
            string root=Path.GetFullPath(persistentDirectory);
            return CaptureExistingPlayerDataAt(Path.Combine(root,"PlayerProgress"),Path.Combine(root,"PlayerDecks"),hasPreference,fileExists);
        }
        public static bool CaptureExistingPlayerDataAt(string progressDirectory,string deckDirectory,Func<string,bool> hasPreference,Func<string,bool> fileExists)
        {
            if(hasPreference==null||fileExists==null)throw new ArgumentNullException(hasPreference==null?nameof(hasPreference):nameof(fileExists));
            foreach(string key in LegacyGameKeys)if(hasPreference(key))return true;
            foreach(string path in new[]{
                Path.Combine(progressDirectory,"progress.json"),Path.Combine(progressDirectory,"progress.backup.json"),
                Path.Combine(deckDirectory,"decks.json"),Path.Combine(deckDirectory,"decks.backup.json")
            })
                if(fileExists(path))return true;
            // QA profiles, locks, incomplete temporaries and presentation settings
            // do not establish previous access to the playable catalogue.
            return false;
        }
    }
}
