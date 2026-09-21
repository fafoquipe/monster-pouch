using MonsterPouch.Gameplay.Match;
namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ShowDocumentedInspection(OwnedUnit owned,bool enemy) => ShowAbilityCard(owned,enemy,false);
    }
}
