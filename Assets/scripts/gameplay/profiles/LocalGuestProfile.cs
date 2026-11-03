using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestOnboardingStage { None, ChooseStarter, Tutorial, Complete }
    /// <summary>
    /// Device-local guest identity. This is neither a linked account nor an
    /// economic authority; earned local currency never includes QA or paid credits.
    /// </summary>
    [Serializable] public sealed partial class LocalGuestProfile
    {
        public const int CurrentVersion=11;
        public int Version=CurrentVersion;
        public string ProfileKind="local-guest";
        public string ProfileId;
        public long Revision;
        // Compatibility marker, not ownership of every character in the catalogue.
        public bool HasLegacyAccess;
        public List<StarterSelection> StarterSelections=new List<StarterSelection>();
        public StarterSelection Starter=>StarterSelections?.FirstOrDefault();
        public GuestOnboardingStage OnboardingStage;
        public List<string> CompletedTutorialModules=new List<string>();
        public bool OnboardingRequired=>!AndroidDeveloperContent.IsUnlocked(this)&&
            (OnboardingStage==GuestOnboardingStage.ChooseStarter||OnboardingStage==GuestOnboardingStage.Tutorial);
        public string NextTutorialModuleId=>!AndroidDeveloperContent.IsUnlocked(this)&&OnboardingStage==GuestOnboardingStage.Tutorial&&CompletedTutorialModules!=null&&
            CompletedTutorialModules.Count<GuestTutorialModules.OrderedIds.Count?GuestTutorialModules.OrderedIds[CompletedTutorialModules.Count]:null;

        public void UpgradeSchema()
        {
            if(Version>=1&&Version<=10&&(ProgressionClaims!=null&&ProgressionClaims.Count!=0||
                ProgressionPouches!=null&&ProgressionPouches.Count!=0||ProgressionOpenings!=null&&ProgressionOpenings.Count!=0||
                !string.IsNullOrEmpty(ProgressionTutorialVisitedUtc)))
                throw new InvalidDataException("An older guest cannot contain trophy-road receipts.");
            if(Version>=1&&Version<=9)
            {
                if(TrophyLadder!=null)
                    throw new InvalidDataException("An older guest cannot contain trophy records.");
                InitializeTrophyLadder();
            }
            if(Version>=1&&Version<=8)
            {
                if(TutorialPouches!=null&&TutorialPouches.Count!=0||TutorialMenuVisits!=null&&TutorialMenuVisits.Count!=0||
                    !string.IsNullOrEmpty(TutorialMachineReceiptId)||CompletedTutorialModules!=null&&CompletedTutorialModules.Count>6)
                    throw new InvalidDataException("An older guest cannot contain tutorial economy receipts.");
                TutorialPouches=new List<GuestTutorialPouch>();TutorialMenuVisits=new List<string>();TutorialMachineReceiptId=null;
            }

            if(Version>=1&&Version<=7 &&
                (WalletEvents!=null&&WalletEvents.Any(e=>e!=null&&(e.Kind==GuestWalletEventKind.AbilityPurchase||
                    e.PermanentPolicyVersion!=0||GuestPermanentEconomy.ReservedId(e.Id)||
                    e.SourceId==GuestPermanentEconomy.WelcomeApprovalId||e.SourceId==GuestPermanentEconomy.AbilityPurchaseApprovalId||
                    !string.IsNullOrEmpty(e.CharacterId)||!string.IsNullOrEmpty(e.AbilityId)))||
                AbilityUnlocks!=null&&AbilityUnlocks.Any(r=>r!=null&&(r.Source==GuestUnlockSource.Purchase||GuestPermanentEconomy.ReservedId(r.ReceiptId)))||
                CharacterUnlocks!=null&&CharacterUnlocks.Any(r=>r!=null&&(r.Source==GuestUnlockSource.Purchase||GuestPermanentEconomy.ReservedId(r.ReceiptId)))))
                throw new InvalidDataException("An older guest cannot contain permanent purchase or welcome-policy receipts.");
            if(Version>=1&&Version<=6)
            {
                if(!string.IsNullOrEmpty(PouchRoomDay)||PouchVictories!=null&&PouchVictories.Count!=0||
                    Pouches!=null&&Pouches.Count!=0||PouchOpenings!=null&&PouchOpenings.Count!=0)
                    throw new InvalidDataException("An older guest cannot contain pouch-room inventory or receipts.");
                PouchRoomDay=string.Empty;
                PouchVictories=new List<GuestPouchVictoryReceipt>();
                Pouches=new List<GuestPouchInstance>();
                PouchOpenings=new List<GuestPouchOpenReceipt>();
            }
            if(Version>=1&&Version<=5)
            {
                if(WalletEvents!=null&&WalletEvents.Count!=0||VendingMachines!=null&&VendingMachines.Count!=0)
                    throw new InvalidDataException("An older guest cannot contain local wallet or vending transactions.");
                WalletEvents=new List<GuestWalletEvent>();
                VendingMachines=new List<GuestVendingMachine>();
            }
            if(Version>=1&&Version<=4)
            {
                if(AbilityUnlocks!=null&&AbilityUnlocks.Count!=0)
                    throw new InvalidDataException("An older guest cannot contain permanent ability receipts.");
                // Absence of historical receipts does not define initial skills
                // or infer ownership from match upgrades or legacy catalogue access.
                AbilityUnlocks=new List<GuestAbilityUnlockReceipt>();
            }
            if(Version>=1&&Version<=3)
            {
                if(CharacterUnlocks!=null&&CharacterUnlocks.Count!=0)
                    throw new InvalidDataException("An older guest cannot contain character unlock receipts.");
                // Existing accounts retain their playable collection and custom
                // decks. Only newly created v4 identities use starter ownership.
                CollectionMode=GuestCollectionMode.Legacy;
                CharacterUnlocks=new List<GuestCharacterUnlockReceipt>();
            }
            if(Version==1)
            {
                ValidateIdentityAndStarter();
                // A pre-existing identity must never become a new account just
                // because the old format had no onboarding flag or starter.
                OnboardingStage=GuestOnboardingStage.None;
                CompletedTutorialModules=new List<string>();
                Version=CurrentVersion;
            }
            else if(Version==2)
            {
                ValidateIdentityAndStarter();
                // Version 2 could record only first-contact. Validate the old
                // contract before enabling new modules; preserve every receipt.
                ValidateOnboarding(id=>id==GuestTutorialModules.FirstContact);
                Version=CurrentVersion;
            }
            else if(Version==3)
            {
                ValidateIdentityAndStarter();
                ValidateOnboarding(GuestTutorialModules.IsImplemented);
                Version=CurrentVersion;
            }
            else if(Version==4)
            {
                ValidateIdentityAndStarter();
                ValidateOnboarding(GuestTutorialModules.IsImplemented);
                ValidateCollection();
                Version=CurrentVersion;
            }
            else if(Version==5||Version==6||Version==7||Version==8||Version==9)Version=CurrentVersion;
            if(Version==10)Version=CurrentVersion;
            UpgradeTrophyLadder();
            Validate();
        }

        public void Validate()
        {
            if(Version<1)throw new InvalidDataException("Missing guest profile version.");
            if(Version!=CurrentVersion)throw new NotSupportedException("Unsupported guest profile version.");
            ValidateIdentityAndStarter();
            ValidateOnboarding(GuestTutorialModules.IsImplemented);
            ValidateCollection();
            ValidateAbilities();
            ValidateVending();
            ValidatePouchRoom();
            ValidateTutorialEconomy();
            ValidateTrophyLadder();
            ValidateProgressionRewards();
        }
        void ValidateOnboarding(Func<string,bool> isImplemented)
        {
            if(!Enum.IsDefined(typeof(GuestOnboardingStage),OnboardingStage)||CompletedTutorialModules==null||
                CompletedTutorialModules.Count>GuestTutorialModules.OrderedIds.Count||
                CompletedTutorialModules.Any(id=>!isImplemented(id))||
                !CompletedTutorialModules.SequenceEqual(GuestTutorialModules.OrderedIds.Take(CompletedTutorialModules.Count),StringComparer.Ordinal))
                throw new InvalidDataException("Invalid tutorial sequence.");
            if(HasLegacyAccess&&OnboardingStage!=GuestOnboardingStage.None)
                throw new InvalidDataException("Legacy accounts cannot enter new-account onboarding.");
            if(OnboardingStage==GuestOnboardingStage.None&&CompletedTutorialModules.Count!=0||
                OnboardingStage==GuestOnboardingStage.ChooseStarter&&(Starter!=null||CompletedTutorialModules.Count!=0)||
                OnboardingStage==GuestOnboardingStage.Tutorial&&(Starter==null||CompletedTutorialModules.Count>=GuestTutorialModules.OrderedIds.Count)||
                OnboardingStage==GuestOnboardingStage.Complete&&(Starter==null||CompletedTutorialModules.Count!=GuestTutorialModules.OrderedIds.Count))
                throw new InvalidDataException("Onboarding stage does not match its persisted receipts.");
        }
        void ValidateIdentityAndStarter()
        {
            if(ProfileKind!="local-guest"||!Guid.TryParseExact(ProfileId,"N",out _)||Revision<0||
                StarterSelections==null||StarterSelections.Count>1||StarterSelections.Any(s=>s==null))
                throw new InvalidDataException("Invalid guest profile.");
            if(Starter==null)return;
            if(Version<4&&Starter.ReceiptId!=StarterRoster.LegacyReceiptId||!StarterRoster.IsValidSelection(Starter))
                throw new InvalidDataException("Invalid guest starter receipt.");
        }
    }
}

