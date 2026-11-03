using System;
using System.IO;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        internal static string GuestProfileDirectoryOverride { get; set; }
        LocalGuestProfileStore guestProfileStore;
        LocalGuestProfile guestProfile;
        string guestProfileError;

        void SetupGuestProfile()
        {
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Explicit legacy test-store overrides keep their original fixtures. Live development
                // always uses a separate generation, without importing real PlayerPrefs or saves.
                if (SetupDevelopmentAccount()) return;
#endif
                // Run before the XP/deck stores can create their first files.
                bool legacy=GuestProfileBootstrap.CaptureExistingPlayerDataAt(
                    ProgressionDirectoryOverride??Path.Combine(Application.persistentDataPath,"PlayerProgress"),
                    DeckDirectoryOverride??Path.Combine(Application.persistentDataPath,"PlayerDecks"),
                    PlayerPrefs.HasKey,File.Exists);
                guestProfileStore=new LocalGuestProfileStore(GuestProfileDirectoryOverride??Path.Combine(Application.persistentDataPath,"GuestProfile"));
                guestProfile=guestProfileStore.OpenOrCreate(legacy);
                guestProfileError=guestProfileStore.NeedsRecovery?"recovery":null;
            }
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is ArgumentException||error is NotSupportedException||error is InvalidOperationException)
            {
                // Leave identity unavailable, never manufacture a replacement.
                // Existing local play and its independent saves can still start.
                guestProfileError=error is NotSupportedException?"version":"load";
            }
        }
    }
}
