// Generated from the delivered faction modules. Do not edit manually.
using System;
namespace MonsterPouch.Gameplay.Match {
public sealed partial class CombatSimulation {
private void EronyaBindModule(ActorClock actor) {
var state=EronyaState(actor);if(!EronyaOwns(actor))return;
switch(Id(actor)) {
case "serya":
case "legionario":
case "dagata":
case "canonero-del-orvek":
state.Module=new EronyaModuleCallbacks {
Begin=() => EronyaVolkanarBegin(actor),
Tick=() => EronyaVolkanarTick(actor),
EndTick=() => EronyaVolkanarEndTick(actor),
MovementStarted=() => EronyaVolkanarMovementStarted(actor),
Died=() => EronyaVolkanarDied(actor),
End=() => EronyaVolkanarEnd(actor),
Clear=() => EronyaVolkanarClear(),
Plan=() => EronyaVolkanarTryPlanCast(actor,out var plan) ? plan : null,
Cast=plan => EronyaVolkanarResolveCast(actor,plan),
BeforeOutgoing=damage => EronyaVolkanarBeforeOutgoing(actor,damage),
BeforeDamage=damage => EronyaVolkanarBeforeDamage(actor,damage),
BasicImpact=hit => EronyaVolkanarBasicImpact(actor,hit),
AfterDamage=hit => EronyaVolkanarAfterDamage(actor,hit),
AfterOwnDamage=hit => EronyaVolkanarAfterOwnDamage(actor,hit),
AfterOwnImpact=hit => EronyaVolkanarAfterOwnImpact(actor,hit),
Killed=(victim,hit) => EronyaVolkanarKilled(actor,victim,hit),
ShieldEnded=ended => EronyaVolkanarShieldEnded(actor,ended),
StatusAttempt=(source,kind,id) => EronyaVolkanarStatusAttempt(actor,source,kind,id),
Moved=(from,to,forced) => EronyaVolkanarMoved(actor,from,to,forced),
Interrupt=reason => EronyaVolkanarInterrupt(actor,reason),
};break;
case "cael":
case "guarda-veterano":
case "arboreo":
case "antidota":
state.Module=new EronyaModuleCallbacks {
Begin=() => EronyaNaturalisBegin(actor),
Tick=() => EronyaNaturalisTick(actor),
EndTick=() => EronyaNaturalisEndTick(actor),
MovementStarted=() => EronyaNaturalisMovementStarted(actor),
Died=() => EronyaNaturalisDied(actor),
End=() => EronyaNaturalisEnd(actor),
Clear=() => EronyaNaturalisClear(),
Plan=() => EronyaNaturalisTryPlanCast(actor,out var plan) ? plan : null,
Cast=plan => EronyaNaturalisResolveCast(actor,plan),
BeforeOutgoing=damage => EronyaNaturalisBeforeOutgoing(actor,damage),
BeforeDamage=damage => EronyaNaturalisBeforeDamage(actor,damage),
BasicImpact=hit => EronyaNaturalisBasicImpact(actor,hit),
AfterDamage=hit => EronyaNaturalisAfterDamage(actor,hit),
AfterOwnDamage=hit => EronyaNaturalisAfterOwnDamage(actor,hit),
AfterOwnImpact=hit => EronyaNaturalisAfterOwnImpact(actor,hit),
Killed=(victim,hit) => EronyaNaturalisKilled(actor,victim,hit),
ShieldEnded=ended => EronyaNaturalisShieldEnded(actor,ended),
StatusAttempt=(source,kind,id) => EronyaNaturalisStatusAttempt(actor,source,kind,id),
Moved=(from,to,forced) => EronyaNaturalisMoved(actor,from,to,forced),
Interrupt=reason => EronyaNaturalisInterrupt(actor,reason),
};break;
case "alveyr":
case "zephalontropo":
case "alboguera":
case "palista":
state.Module=new EronyaModuleCallbacks {
Begin=() => EronyaAireolaBegin(actor),
Tick=() => EronyaAireolaTick(actor),
EndTick=() => EronyaAireolaEndTick(actor),
MovementStarted=() => EronyaAireolaMovementStarted(actor),
Died=() => EronyaAireolaDied(actor),
End=() => EronyaAireolaEnd(actor),
Clear=() => EronyaAireolaClear(),
Plan=() => EronyaAireolaTryPlanCast(actor,out var plan) ? plan : null,
Cast=plan => EronyaAireolaResolveCast(actor,plan),
BeforeOutgoing=damage => EronyaAireolaBeforeOutgoing(actor,damage),
BeforeDamage=damage => EronyaAireolaBeforeDamage(actor,damage),
BasicImpact=hit => EronyaAireolaBasicImpact(actor,hit),
AfterDamage=hit => EronyaAireolaAfterDamage(actor,hit),
AfterOwnDamage=hit => EronyaAireolaAfterOwnDamage(actor,hit),
AfterOwnImpact=hit => EronyaAireolaAfterOwnImpact(actor,hit),
Killed=(victim,hit) => EronyaAireolaKilled(actor,victim,hit),
ShieldEnded=ended => EronyaAireolaShieldEnded(actor,ended),
StatusAttempt=(source,kind,id) => EronyaAireolaStatusAttempt(actor,source,kind,id),
Moved=(from,to,forced) => EronyaAireolaMoved(actor,from,to,forced),
Interrupt=reason => EronyaAireolaInterrupt(actor,reason),
};break;
case "drayven":
case "titanica":
case "jabalinero":
case "maestro-de-las-mareas":
state.Module=new EronyaModuleCallbacks {
Begin=() => EronyaAquarionBegin(actor),
Tick=() => EronyaAquarionTick(actor),
EndTick=() => EronyaAquarionEndTick(actor),
MovementStarted=() => EronyaAquarionMovementStarted(actor),
Died=() => EronyaAquarionDied(actor),
End=() => EronyaAquarionEnd(actor),
Clear=() => EronyaAquarionClear(),
Plan=() => EronyaAquarionTryPlanCast(actor,out var plan) ? plan : null,
Cast=plan => EronyaAquarionResolveCast(actor,plan),
BeforeOutgoing=damage => EronyaAquarionBeforeOutgoing(actor,damage),
BeforeDamage=damage => EronyaAquarionBeforeDamage(actor,damage),
BasicImpact=hit => EronyaAquarionBasicImpact(actor,hit),
AfterDamage=hit => EronyaAquarionAfterDamage(actor,hit),
AfterOwnDamage=hit => EronyaAquarionAfterOwnDamage(actor,hit),
Killed=(victim,hit) => EronyaAquarionKilled(actor,victim,hit),
ShieldEnded=ended => EronyaAquarionShieldEnded(actor,ended),
StatusAttempt=(source,kind,id) => EronyaAquarionStatusAttempt(actor,source,kind,id),
Moved=(from,to,forced) => EronyaAquarionMoved(actor,from,to,forced),
Interrupt=reason => EronyaAquarionInterrupt(actor,reason),
};break;
case "korran":
case "golem-tectonico":
case "la-minera":
case "protectora-del-mausoleo":
state.Module=new EronyaModuleCallbacks {
Begin=() => EronyaGeotharBegin(actor),
Tick=() => EronyaGeotharTick(actor),
EndTick=() => EronyaGeotharEndTick(actor),
MovementStarted=() => EronyaGeotharMovementStarted(actor),
Died=() => EronyaGeotharDied(actor),
End=() => EronyaGeotharEnd(actor),
Clear=() => EronyaGeotharClear(),
Plan=() => EronyaGeotharTryPlanCast(actor,out var plan) ? plan : null,
Cast=plan => EronyaGeotharResolveCast(actor,plan),
BeforeOutgoing=damage => EronyaGeotharBeforeOutgoing(actor,damage),
BeforeDamage=damage => EronyaGeotharBeforeDamage(actor,damage),
BasicImpact=hit => EronyaGeotharBasicImpact(actor,hit),
AfterDamage=hit => EronyaGeotharAfterDamage(actor,hit),
AfterOwnDamage=hit => EronyaGeotharAfterOwnDamage(actor,hit),
AfterOwnImpact=hit => EronyaGeotharAfterOwnImpact(actor,hit),
Killed=(victim,hit) => EronyaGeotharKilled(actor,victim,hit),
ShieldEnded=ended => EronyaGeotharShieldEnded(actor,ended),
StatusAttempt=(source,kind,id) => EronyaGeotharStatusAttempt(actor,source,kind,id),
Moved=(from,to,forced) => EronyaGeotharMoved(actor,from,to,forced),
Interrupt=reason => EronyaGeotharInterrupt(actor,reason),
};break;
default:throw new InvalidOperationException("Unregistered Eronya module: "+Id(actor));
}}
}}
