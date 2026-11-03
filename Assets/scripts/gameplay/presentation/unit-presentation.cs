using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Only presentation: never changes board occupancy, damage, or the simulation clock.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UnitPresentation : MonoBehaviour
    {
        private BattleUnit unit;
        private BoardWorldMapper mapper;
        private UnitArt art;
        private SpriteRenderer body;
        private SpriteRenderer torso;
        private SpriteRenderer leftFoot;
        private SpriteRenderer rightFoot;
        private SpriteRenderer shadow;
        private SpriteRenderer hammerJaw;
        private SpriteRenderer hammerNeck;
        private readonly Dictionary<Sprite, HammerParts> hammerParts = new Dictionary<Sprite, HammerParts>();
        private Transform visualRoot;
        private UnitFacing facing = UnitFacing.South;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private Vector3 dashThrough;
        private Vector3 attackDirection;
        private float moveElapsed;
        private float walkElapsed;
        private float moveDuration;
        private float groundLift;
        private bool popowWasStunned;
        private float popowLiftElapsed;
        private UnitFacing moveFacing;
        private float attackElapsed;
        private float attackContactDelay;
        private float attackRecoveryDuration,attackHoldDuration;
        private float deathElapsed;
        private float reviveElapsed, reviveDuration;
        private bool reviving;
        private float idleElapsed;
        private float baseScale = 1f;
        private bool moving;
        private bool woodenDummy;
        private float dummyHopSide = -1;
        private bool leftFootHop;
        public bool IsLeftFootHop => leftFootHop;
        private bool flightMotion;
        private bool pullMotion, backflipMotion;
        private float backflipLanding;
        private sealed class RollParts { public Sprite Head, Lower; public Vector3 Center; public float HeadHeight, LowerHeight; }
        private readonly Dictionary<Sprite,RollParts> rollParts=new Dictionary<Sprite,RollParts>();
        private bool dashMotion;
        private bool flowerPreparing;
        private bool attacking;
        private bool dead;
        private bool rangedAttack;
        private bool frozen;
        private int observedAttackReset;
        private float hitRemaining, abilityRemaining;
        private bool Heavy => art != null && (art.Id=="trimol"||art.Id=="tokoro"||art.Id=="tauris"||art.Id=="gochan");
        private float HitFlashDuration => Mathf.Max(.05f, art?.HitFlashDuration ?? .16f);
        private float AbilityPulseDuration => Mathf.Max(.05f, art?.AbilityPulseDuration ?? .42f);
        public void Hit() { if(!dead)hitRemaining=HitFlashDuration; }
        private float specialElapsed, specialDuration;
        private bool specialActive;
        private bool taurisBiteActive,headbuttActive,phasedSpecial;
        private int headbuttImpactVersion;
        private float specialCast,specialHold,specialReturn;
        private bool CanContinueHeadbutt => specialActive && headbuttActive && unit!=null && unit.IsAlive &&
            !unit.IsFlying && !unit.IsBeingPulled && specialCombatResetVersion==unit.CombatResetVersion &&
            unit.HeadbuttImpactVersion!=headbuttImpactVersion;
        public float GetAbilityDuration(string kind)
        {
            float fallback=kind=="blotan-income"?AbilityTiming.BlotanIncomeFallbackDuration:AbilityAnimationDuration;
            float duration=unit==null?fallback:AbilityTiming.Duration(unit.BaseStats,kind,fallback);
            return kind=="blotan-income"&&duration<=0?AbilityTiming.BlotanIncomeFallbackDuration:duration;
        }
        private int specialCombatResetVersion;
        private bool CanContinueTaurisBite => specialActive && taurisBiteActive && unit != null &&
            specialCombatResetVersion == unit.CombatResetVersion && unit.IsAlive &&
            !unit.IsStunned && !unit.IsFlying && !unit.IsBeingPulled;
        private bool LunarGuard => art?.Id=="bugaloo" && unit!=null && unit.IsReflecting &&
            unit.IsAlive && !dead && !reviving && !frozen;
        public bool IsUsingAbility => specialActive || LunarGuard;
        public float AbilityAnimationDuration => art?.GetAbilityAnimationDuration(facing) ?? .65f;
        public bool IsDashing => dashMotion;
        public bool IsBackflipping => backflipMotion;
        public bool IsBeingPulled => pullMotion;
        public void UseAbility(string kind = null)
        {
            if(dead||reviving||frozen)return;
            if(UsesActionPreparation && kind!=activeAbilityEffect)return; // Passive VFX are not a new Super body cast.
            if(AbilityTiming.UsesBasicAttackAnimation(kind))return;
            // Hunger rewards the same bite; its kill signal must not restart that bite's visual clock.
            if(kind=="tauris-kill"&&CanContinueTaurisBite){abilityRemaining=AbilityPulseDuration;return;}
            // A critical lands during the hammer's contact pose; never replace that slam with a generic special.
            if(kind=="atong-critical"&&art?.Id=="atong"&&attacking){abilityRemaining=AbilityPulseDuration;return;}
            // The cast event owns Flo's full pull-out/plant sequence; do not restart it on AbilityUsed.
            if(flowerPreparing&&art?.Id=="flo")return;
            if(dashMotion&&art?.Id=="popow")return;
            if(backflipMotion||pullMotion)return;
            if(kind=="blotan-income"){observedAttackReset=unit?.AttackResetVersion??observedAttackReset;attacking=false;}
            abilityRemaining=AbilityPulseDuration;
            // Passive rewards/stealth/sleep are reactions, not a new offensive windup.
            if(art?.AuthoredBodyMotion==true && (kind=="tokoro-sleep" || kind=="sepora-stealth" ||
                (kind!=null && kind.EndsWith("-kill",System.StringComparison.Ordinal))))return;
            var clip=art?.Animation(facing);
            if(clip?.Special==null||clip.Special.Length==0)return;
            ClearDisplacementPresentation();
            observedAttackReset=unit?.AttackResetVersion??observedAttackReset;
            specialCast=unit==null?0:AbilityTiming.Cast(unit.BaseStats,kind);
            specialHold=unit==null?0:AbilityTiming.Hold(unit.BaseStats,kind);
            specialReturn=unit==null?0:AbilityTiming.Recovery(unit.BaseStats,kind);
            phasedSpecial=unit!=null&&AbilityTiming.HasPhases(unit.BaseStats,kind);
            ResetDagataPresentationClock();
            specialElapsed=0;specialDuration=GetAbilityDuration(kind);specialActive=specialDuration>0;
            headbuttActive=kind=="tokoro-super";headbuttImpactVersion=unit?.HeadbuttImpactVersion??0;
            taurisBiteActive=kind=="tauris-super"&&art?.Id=="tauris";
            specialCombatResetVersion=unit?.CombatResetVersion??0;
            releasedEronyaSpecial=UsesActionPreparation;
            if(releasedEronyaSpecial)
            {
                // AbilityUsed is the actual release. Preparation was read from Core's existing cast clock.
                attacking=false;
                if(phasedSpecial)
                {
                    // Preparation already elapsed in Core. Render only its edited hold and recovery.
                    specialElapsed=specialCast;
                    specialDuration=specialCast+specialHold+specialReturn;
                    specialActive=specialHold+specialReturn>0;
                    if(!specialActive)CompleteAbilityPose();
                }
                else
                {
                    specialElapsed=0;
                    specialDuration=Mathf.Max(CombatSimulation.TickDuration,AbilityAnimationDuration*(1-Mathf.Clamp(clip.SpecialReleasePoint,.15f,.85f)));
                }
            }
        }

        /// <summary>The simulation supplies the exact cast duration; the bloom is planted only when it ends.</summary>
        public void PrepareFlower(float duration)
        {
            if(dead||reviving||frozen||art?.Id!="flo")return;
            ClearDisplacementPresentation();
            observedAttackReset=unit?.AttackResetVersion??observedAttackReset;
            specialElapsed=0;
            specialCast=duration;specialHold=AbilityTiming.Hold(unit.BaseStats,"flo-super");specialReturn=AbilityTiming.Recovery(unit.BaseStats,"flo-super");
            specialDuration=Mathf.Max(.01f,specialCast+specialHold+specialReturn);
            flowerPreparing=specialActive=true;phasedSpecial=headbuttActive=false;
            attacking=false;
            abilityRemaining=0;
        }
        private static Sprite shadowSprite;
        private static Material sharedSpriteMaterial;
        // Presentation only: 20% larger art/rig around the unchanged foot anchor; stats and timing are unchanged.
        private const float VisualScaleMultiplier = UnitArt.AuthoredScaleMultiplier;
        // Fallback for isolated previews without a mapper: main-scene's cell width after arena expansion.
        public const float ReferenceTileWidth = 1.2774138f * 1.10f;
        private const float DeathDuration = .62f;
        private const float ShadowOpacity = .55f;

        public Transform VisualRoot => visualRoot;
        public UnitFacing Facing => facing;
        public bool IsMoving => moving;
        public bool IsAttacking => attacking;
        public bool IsDying => dead;
        public bool IsReviving => reviving;
        public bool DeathFinished => dead && deathElapsed >= DeathDuration;
        public bool IsFrozen => frozen;
        public float AttackContactDelay => attackContactDelay;
        public bool AttackContactReached => attacking && attackElapsed + .00001f >= attackContactDelay;
        public SpriteRenderer Renderer => body;
        public Vector3 VitalsHeadWorldPosition
        {
            get
            {
                if(visualRoot==null)return transform.position;
                Vector2 anchor=art!=null?art.VitalsHeadAnchor:Vector2.zero;
                if(anchor==Vector2.zero)
                {
                    Sprite reference=art?.Idle(facing)??body?.sprite;
                    anchor=new Vector2(0,reference!=null?reference.bounds.max.y:0);
                }
                if(body!=null&&body.flipX)anchor.x=-anchor.x;
                // The roll pivots around the body centre, not its feet. Keep its HUD
                // upright above that centre instead of orbiting with the rotating art.
                if(backflipMotion&&art!=null)
                {
                    Vector3 center=art.AuthoredBodyMotion
                        ? (body!=null&&body.sprite!=null?body.sprite.bounds.center:Vector3.zero)
                        : (GetRollParts(art.Idle(facing))?.Center??Vector3.zero);
                    if(art.AuthoredBodyMotion&&body!=null&&body.flipX)center.x=-center.x;
                    anchor-=(Vector2)center;
                }
                Vector3 scale=visualRoot.lossyScale;
                // Follow the authored point and current size/lift; pose bounds include
                // transparent padding, weapons and effects and must not move this anchor.
                return visualRoot.position+new Vector3(anchor.x*Mathf.Abs(scale.x),anchor.y*Mathf.Abs(scale.y),0);
            }
        }
        public void SetFullyHidden(bool hidden)
        {
            if(body!=null)body.forceRenderingOff=hidden;
            if(torso!=null)torso.forceRenderingOff=hidden;
            if(leftFoot!=null)leftFoot.forceRenderingOff=hidden;
            if(rightFoot!=null)rightFoot.forceRenderingOff=hidden;
            if(shadow!=null)shadow.forceRenderingOff=hidden;
            if(hammerJaw!=null)hammerJaw.forceRenderingOff=hidden;
            if(hammerNeck!=null)hammerNeck.forceRenderingOff=hidden;
        }

        // Preparation snapshots share the combat figure's geometry and depth.
        public static float PresentationScale(UnitArt unitArt, float tileWidth = ReferenceTileWidth)
        {
            Sprite reference = unitArt != null ? unitArt.Portrait : null;
            return reference != null
                ? VisualScaleMultiplier * unitArt.DisplayScale * unitArt.GetInGameScale(Mathf.Abs(tileWidth)) * unitArt.WorldWidth * reference.pixelsPerUnit / Mathf.Max(1, unitArt.ReferencePixelWidth) : 1;
        }
        public static int BodySortingOrder(float worldY) => 122 - Mathf.RoundToInt(worldY * 4);
        public static Sprite ContactShadowSprite => GetShadowSprite();
        public static Vector3 ContactShadowScale(UnitArt unitArt, float tileWidth = ReferenceTileWidth)
        {
            float bodySize = unitArt != null ? unitArt.DisplayScale : 1f;
            float gameScale = unitArt?.GetInGameScale(Mathf.Abs(tileWidth)) ?? 1f;
            return new Vector3((unitArt != null ? unitArt.WorldWidth * .95f : .8f) * VisualScaleMultiplier * bodySize * gameScale, .34f * Mathf.Sqrt(bodySize) * gameScale, 1);
        }
        public static SpriteRenderer CreateSpriteRenderer(string name, Transform parent) => NewRenderer(name, parent);

        public static void ApplyCharacterMaterial(SpriteRenderer renderer,string id)
        {
            if(renderer==null)return;
            renderer.sharedMaterial=id=="anuik"?Resources.Load<Material>("MonsterPouch/AnuikContour")??sharedSpriteMaterial:sharedSpriteMaterial;
        }

        public void Configure(BattleUnit newUnit, BoardWorldMapper newMapper, UnitArt newArt)
        {
            unit = newUnit;
            actionPreparationSource=null;activeAbilityEffect=null;actionPreparation=default;releasedEronyaSpecial=false;
            ResetDisplacementTracking();
            mapper = newMapper;
            art = newArt;
            if (newUnit != null && newUnit.IsCombatSummon && newArt?.Id == "dummy")
                art = WoodenDummyArt.Create(newArt);
            woodenDummy = art != newArt;
            dummyHopSide = -1;
            leftFootHop = false;
            // The caller supplies the character asset; never override edited art by a hardcoded ID.
            if (visualRoot == null)
            {
                visualRoot = new GameObject("unit-visual").transform;
                visualRoot.SetParent(transform, false);
                body = NewRenderer("pose", visualRoot);
                torso = NewRenderer("body-rig", visualRoot);
                leftFoot = NewRenderer("left-foot-rig", visualRoot);
                rightFoot = NewRenderer("right-foot-rig", visualRoot);
                shadow = NewRenderer("ground-shadow", transform);
                hammerJaw = NewRenderer("hammer-jaw", visualRoot);
                hammerNeck = NewRenderer("hammer-neck", visualRoot);
                shadow.sprite = GetShadowSprite();
                shadow.color = new Color(.015f, .025f, .035f, ShadowOpacity);
            }

            // Old prototype renderers are owned by their prototype. Avoid duplicate art if a caller reuses that GO.
            ApplyCharacterMaterial(body,art?.Id);
            SpriteRenderer oldRenderer = GetComponent<SpriteRenderer>();
            if (oldRenderer != null) oldRenderer.enabled = false;
            moving = attacking = dead = frozen = reviving = false;
            ClearReservedMovementTracking();
            flightMotion=false;
            pullMotion=backflipMotion=false;backflipLanding=0;
            dashMotion=flowerPreparing=false;
            groundLift=0;
            specialActive=taurisBiteActive=false;
            deathElapsed = idleElapsed = walkElapsed = 0;
            observedAttackReset=unit?.AttackResetVersion??0;hitRemaining=abilityRemaining=0;
            facing = unit != null && unit.Side == BoardSide.Blue ? UnitFacing.North : UnitFacing.South;
            unit?.SetAbilityAnimationDuration(art?.GetAbilityAnimationDuration(facing) ?? .65f);
            baseScale = PresentationScale(art, mapper != null ? mapper.CellSize.x : ReferenceTileWidth);
            ResetPose();
            UpdateSorting();
        }

        /// <summary>Delta is in board coordinates; positive board Y faces south.</summary>
        public void Face(int deltaX, int deltaY)
        {
            if (dead || reviving || (deltaX == 0 && deltaY == 0)) return;
            // Target updates can arrive during a step. The walking pose must face its actual travel.
            if ((moving || displacementActive) && !attacking) return;
            float angle = Mathf.Atan2(deltaX, -deltaY) * Mathf.Rad2Deg;
            facing = (UnitFacing)((Mathf.RoundToInt(angle / 45f) + 8) % 8);
            unit?.SetAbilityAnimationDuration(art?.GetAbilityAnimationDuration(facing) ?? .65f);
            if (!attacking && body != null && art != null)
            {
                body.sprite = art.Idle(facing);
                body.flipX = art.Animation(facing)?.FlipX ?? false;
            }
        }

        public void Face(Vector2Int delta) { Face(delta.x, delta.y); }

        public void Move(Vector3 from, Vector3 to, float duration)
        {
            if (dead || reviving || frozen || visualRoot == null) return;
            ClearDisplacementPresentation();
            if(UsesActionPreparation)ClearBoundActionPresentation();
            // A dash commits the logical cell and emits Moved after DashStarted. Keep its authored route.
            if(dashMotion&&(moveTo-to).sqrMagnitude<.0001f)return;
            // A hook replaces an in-flight step from the figure's visible position.
            if(moving&&unit!=null&&unit.IsBeingPulled)from=transform.position;
            dashMotion=false;
            flightMotion=unit!=null&&unit.IsFlying;
            backflipMotion=flightMotion&&art?.Id=="hymay";
            pullMotion=unit!=null&&unit.IsBeingPulled;
            backflipLanding=0;
            if(flightMotion)duration=unit.FlightDuration;
            if(pullMotion)duration=unit.PullDuration;
            if(flightMotion||pullMotion){attacking=specialActive=false;abilityRemaining=0;}
            Vector3 direction = to - from;
            // A hooked unit is dragged; Hymay stays facing his victim while somersaulting away.
            if(!backflipMotion&&!pullMotion)FaceWorld(direction);
            moveFacing = facing;
            if(!flightMotion)attacking=false;
            moveFrom = from;
            moveTo = to;
            moveElapsed = 0;
            groundLift = 0;
            moveDuration = Mathf.Max(.001f, duration);
            moving = duration > 0;
            TrackReservedMovement();
            if (moving && !flightMotion && !pullMotion && direction.sqrMagnitude > .0001f &&
                art.TravelMotion == UnitTravelMotion.AlternatingHop)
                leftFootHop = !leftFootHop;
            if (woodenDummy && moving && !flightMotion && !pullMotion && direction.sqrMagnitude > .0001f)
                dummyHopSide = -dummyHopSide;
            transform.position = moving ? from : to;
        }

        /// <summary>Presentation follows the same route through the target as the authoritative dash.</summary>
        public void Dash(Vector3 from, Vector3 throughTarget, Vector3 to, float duration)
        {
            if(dead||reviving||frozen||visualRoot==null)return;
            ClearDisplacementPresentation();
            ClearReservedMovementTracking();
            moveFrom=from;dashThrough=throughTarget;moveTo=to;
            moveElapsed=groundLift=0;
            moveDuration=Mathf.Max(.001f,duration);
            dashMotion=moving=true;
            attacking=flightMotion=pullMotion=backflipMotion=flowerPreparing=specialActive=false;
            backflipLanding=0;
            abilityRemaining=0;
            FaceWorld(throughTarget-from);moveFacing=facing;
            attackDirection=(throughTarget-from).normalized;
            transform.position=from;
        }

        public void Attack(Vector3 target, bool projectile)
        {
            Attack(target, projectile, unit != null ? CombatSimulation.GetAttackWindup(unit) : .2f);
        }

        /// <summary>
        /// Delay is the simulation's event-to-impact time. Melee contact occurs at that instant.
        /// A ranged pose releases at the quantized windup; its separate projectile owns flight/contact.
        /// </summary>
        public void Attack(Vector3 target, bool projectile, float impactDelay)
        {
            if (dead || reviving || frozen || visualRoot == null || backflipMotion || pullMotion) return;
            ClearDisplacementPresentation();
            Vector3 delta = target - transform.position;
            FaceWorld(delta);
            attackDirection = delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.down;
            attackDirection.z = 0;
            ResetDagataPresentationClock();
            rangedAttack = projectile;
            float delay = projectile && unit != null ? CombatSimulation.GetAttackWindup(unit) : impactDelay;
            attackContactDelay = float.IsNaN(delay) || float.IsInfinity(delay) ? .2f : Mathf.Max(0, delay);
            var tail=BasicAttackTiming.Tail(unit);
            // Prototype actors constructed without a roster definition keep their legacy return.
            // Explicit zero phases on catalogue characters remain zero.
            if(unit!=null&&string.IsNullOrEmpty(unit.BaseStats.DefinitionId))
            {
                tail=new Vector2(0,Mathf.Clamp(unit.BaseStats.AttackInterval-attackContactDelay-.02f,.01f,.18f));
                if(art?.Id=="atong")tail.y=Mathf.Max(.18f,tail.y);
            }
            attackHoldDuration=tail.x;
            attackRecoveryDuration=Mathf.Max(.0001f,tail.y);
            attackElapsed = 0;
            attacking = true;
            observedAttackReset=unit?.AttackResetVersion??0;
        }

        public void Attack(Transform target, bool projectile)
        {
            Attack(target != null ? target.position : transform.position + Vector3.down, projectile);
        }

        public void Attack(Transform target, bool projectile, float impactDelay)
        {
            Attack(target != null ? target.position : transform.position + Vector3.down, projectile, impactDelay);
        }

        public void Freeze() { SetFrozen(true); }

        /// <summary>Settle surviving actors at round end; allow already-triggered deaths to finish.</summary>
        public void SetFrozen(bool value)
        {
            frozen = value;
            if(value) { specialActive=flowerPreparing=dashMotion=false; }
            if(value && reviving) { reviving = false; dead = true; deathElapsed = 0; }
            if (!value || dead || visualRoot == null) return;
            attacking = moving = false;
            SnapToCell();
            ResetPose();
            AnimateIdle();
            ApplyDirectionalArticulation();
            UpdateSorting();
        }

        public void Die()
        {
            if (dead) return;
            // Settle the travel arc immediately, including previews without a reservation.
            // This changes only the renderer's transform; the simulation owns the occupied cell.
            SnapToCell();
            if (visualRoot != null) ResetPose();
            specialActive=false;
            flowerPreparing=dashMotion=flightMotion=pullMotion=backflipMotion=false;
            backflipLanding=0;
            reviving = false;
            dead = true;
            if(shadow!=null)shadow.enabled=false;
            attacking = moving = false;
            deathElapsed = 0;
            if (body != null && art != null) body.sprite = art.Idle(facing);
        }

        public void BeginRevive(float duration)
        {
            specialActive=false;
            flowerPreparing=dashMotion=false;
            dead = attacking = moving = false;
            reviving = true;
            reviveElapsed = 0;
            reviveDuration = Mathf.Max(.1f, duration);
            SnapToCell();
            ResetPose();
            AnimateRevive(0);
        }

        public void Revive()
        {
            specialActive=false;
            flowerPreparing=dashMotion=false;
            reviving = dead = attacking = moving = false;
            deathElapsed = 0;
            SnapToCell();
            ResetPose();
            UpdateSorting();
        }

        public void SnapToCell()
        {
            ClearReservedMovementTracking();
            ClearDisplacementPresentation();
            // Remove the procedural lift even when an isolated preview has no board mapper.
            transform.position -= Vector3.up * groundLift;
            moving = false;
            flightMotion=false;
            pullMotion=backflipMotion=false;
            dashMotion=false;
            backflipLanding=0;
            groundLift=0;
            if (unit == null || mapper == null || unit.CurrentCell == null) return;
            if (mapper.TryGetWorldPosition(unit.CurrentCell, out Vector3 position)) transform.position = position;
        }

        private void FaceWorld(Vector3 delta)
        {
            if (delta.sqrMagnitude < .0001f) return;
            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            facing = (UnitFacing)((Mathf.RoundToInt(angle / 45f) + 8) % 8);
        }

        private void Update()
        {
            AdvancePresentation(Time.deltaTime);
        }

        internal void AdvancePresentation(float deltaTime)
        {
            if (art == null || visualRoot == null) return;
            ValidateReservedMovement();
            ReadDagataJump();
            ValidateDisplacementPresentation();
            if (frozen && !dead) return;
            float dt = DagataPresentationDelta(float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0 : Mathf.Max(0, deltaTime));
            if(unit!=null&&specialActive&&(taurisBiteActive||headbuttActive)&&specialCombatResetVersion!=unit.CombatResetVersion)
                specialActive=false;
            if(unit!=null&&observedAttackReset!=unit.AttackResetVersion)
            {
                // The lethal bite invalidates its dead target and resets the basic attack.
                // Finish the configured visual duration while the next target/attack proceeds normally.
                bool finishBite=CanContinueTaurisBite||CanContinueHeadbutt;
                observedAttackReset=unit.AttackResetVersion;attacking=false;attackElapsed=0;specialActive=finishBite;
                flowerPreparing=false;
                if(dashMotion)SnapToCell();
            }
            ReadActionPreparation();
            idleElapsed += dt;
            if(flightMotion && (unit==null||!unit.IsFlying))
            {
                bool landed=backflipMotion&&unit!=null&&unit.IsAlive;
                SnapToCell();if(landed)backflipLanding=AbilityTiming.BackflipLandingDuration;
            }
            if(pullMotion&&(unit==null||!unit.IsBeingPulled))SnapToCell();
            if (moving && !dead)
            {
                moveElapsed += dt;
                walkElapsed += dt;
                float raw=Mathf.Clamp01(moveElapsed/moveDuration);
                float eased=pullMotion?raw:Mathf.Lerp(raw,Mathf.SmoothStep(0,1,raw),Mathf.Clamp01(art.MoveEase));
                var profile=TileHopProfile.For(art.Id);
                bool hop=!flightMotion&&!dashMotion&&!pullMotion&&!profile.Walk&&art.TravelMotion!=UnitTravelMotion.Flying;
                float air=profile.Flight(raw);
                if(hop)eased=Mathf.SmoothStep(0,1,air);
                float arc=hop?Mathf.Sin(air*Mathf.PI)*Mathf.Abs(mapper!=null?mapper.CellSize.y:1f)*profile.Height:0;
                groundLift=arc;
                transform.position=dashMotion?DashPosition(raw):Vector3.Lerp(moveFrom,moveTo,eased)+Vector3.up*arc;
                if (moveElapsed + .00001f >= moveDuration) { moving=false; groundLift=0; transform.position=moveTo; }
            }
            ResetPose();
            if (reviving) AnimateRevive(dt);
            else if (dead) AnimateDeath(dt);
            else if (displacementActive) AnimateDisplacement(dt);
            else if(dashMotion)AnimateDash();
            else if(backflipMotion)AnimateBackflip();
            else if(flightMotion)AnimateFlight();
            else if(pullMotion)AnimatePull();
            else if(backflipLanding>0)
            {
                backflipLanding=Mathf.Max(0,backflipLanding-dt);
                float squash=Mathf.Sin(backflipLanding/AbilityTiming.BackflipLandingDuration*Mathf.PI)*.16f;
                visualRoot.localScale=new Vector3(baseScale*(1+squash*.6f),baseScale*(1-squash),1);
            }
            else if(LunarGuard&&!unit.IsStunned)
            {
                // Hold one calm guard drawing for the authoritative reflection window.
                // The shield artwork carries the ability; never loop the special poses.
                specialActive=attacking=false;abilityRemaining=0;
                ShowFrame(art.Idle(facing),art.Animation(facing));
            }
            else if(specialActive)
            {
                if(attacking){attackElapsed+=dt;if(attackElapsed>=attackContactDelay+attackHoldDuration+attackRecoveryDuration)attacking=false;}
                AnimateSpecial(dt);
            }
            else if (attacking) AnimateAttack(dt);
            else if (moving) AnimateHop();
            else if (AnimateActionPreparation()) { }
            else if(unit!=null&&unit.IsEnergyLocked&&!unit.IsStunned&&art.Animation(facing)?.Special?.Length>0)
                ShowSpecialFrame(art.Animation(facing),Mathf.FloorToInt(idleElapsed/AbilityAnimationDuration*art.Animation(facing).Special.Length)%art.Animation(facing).Special.Length);
            else AnimateIdle();
            if (art.TravelMotion == UnitTravelMotion.Flying && !dead && !reviving && !flightMotion && !pullMotion)
                visualRoot.localPosition += Vector3.up * (.10f + Mathf.Sin(idleElapsed * 5f) * .012f);
            if(!dead&&!reviving)
            {
                if(unit!=null&&unit.IsStunned&&!pullMotion&&!CanContinueHeadbutt)
                { attacking=false;flowerPreparing=specialActive=false;visualRoot.localRotation=Quaternion.Euler(0,0,Mathf.Sin(idleElapsed*32)*3); }
                if(hitRemaining>0)
                {
                    hitRemaining=Mathf.Max(0,hitRemaining-dt);
                    float hit=Mathf.Clamp01(hitRemaining/HitFlashDuration);
                    if (hitRemaining > 0 && !moving && !attacking && !specialActive && !flowerPreparing &&
                        !dashMotion && !flightMotion && !pullMotion && !backflipMotion && backflipLanding <= 0 &&
                        !LunarGuard && !displacementActive && (unit == null || !unit.IsStunned && !unit.IsEnergyLocked))
                        ApplyMartioIdleHurtPose();
                    body.color=Color.Lerp(Color.white,new Color(1,.48f,.48f),hit);
                    visualRoot.localPosition+=Vector3.down*(hit*.016f);
                }
                if(abilityRemaining>0)
                {
                    abilityRemaining=Mathf.Max(0,abilityRemaining-dt);
                    float pulse=Mathf.Sin((1-abilityRemaining/AbilityPulseDuration)*Mathf.PI);
                    visualRoot.localScale*=1+pulse*Mathf.Max(0,art?.AbilityPulseScale??.09f);
                }
                if(unit!=null&&!unit.IsEnergyLocked&&unit.BaseStats.EnergyMax>0&&unit.Energy+.001f>=unit.BaseStats.EnergyMax)
                {
                    float ready=(.5f+.5f*Mathf.Sin(idleElapsed*7.5f))*Mathf.Max(0,art.SuperReadyPulseScale);
                    visualRoot.localScale*=1+ready;
                    body.color=Color.Lerp(body.color,new Color(1f,.9f,.45f,body.color.a),ready*3f);
                }
            }
            if(!dead&&!reviving&&unit!=null&&unit.IsTaunted)
                body.color=Color.Lerp(body.color,new Color(1,.48f,.43f,body.color.a),.38f);
            if (!dead && !reviving)
            {
                ApplyDirectionalArticulation();
                ApplyControlAppearance();
            }
            Color lighting=SceneCharacterLight.SampleWorld(transform.position);
            body.color*=lighting;
            if(torso!=null&&torso.enabled)torso.color=body.color;
            if(leftFoot!=null&&leftFoot.enabled)leftFoot.color=body.color;
            if(rightFoot!=null&&rightFoot.enabled)rightFoot.color=body.color;
            if(hammerJaw!=null&&hammerJaw.enabled){hammerJaw.color=body.color;hammerNeck.color=body.color;}
            UpdateSorting();
        }

        // Martio owns the optional partial implementation; no extra clock or pose priority.
        partial void ApplyMartioIdleHurtPose();

        private void ApplyControlAppearance()
        {
            if(unit==null)return;
            if(unit.IsPopowStunned&&!popowWasStunned)popowLiftElapsed=0;
            popowWasStunned=unit.IsPopowStunned;
            if(unit.IsPopowStunned&&!unit.IsBeingPulled&&!unit.IsFlying)
            {
                var settings=ImpactPresentationSettings.Current;
                float lift=Mathf.Abs(mapper!=null?mapper.CellSize.y:1)*ImpactPresentationSettings.Lift(popowLiftElapsed,settings!=null?settings.PopowRise:.10f,settings!=null?settings.PopowFall:.14f,settings!=null?settings.PopowHeightInCells:.32f);
                popowLiftElapsed+=Time.deltaTime;
                visualRoot.localPosition+=Vector3.up*lift;
                shadow.transform.localScale*=lift>0?.82f:1f;
                shadow.color=new Color(.015f,.025f,.035f,ShadowOpacity*(lift>0?.65f:1f));
            }
            if(unit.IsElectrocuted)
                body.color=new Color(.24f,.22f,.28f,body.color.a);
            if(unit.IsInvisible)
            {
                Color hologram=Color.Lerp(body.color,new Color(.28f,.86f,1),.65f);
                hologram.a=.40f+.06f*Mathf.Sin(idleElapsed*18);
                body.color=hologram;
                shadow.color=new Color(.06f,.25f,.32f,.18f);
            }
        }

        private Vector3 DashPosition(float progress)
        {
            float before=Vector3.Distance(moveFrom,dashThrough),after=Vector3.Distance(dashThrough,moveTo);
            float split=before/Mathf.Max(.001f,before+after);
            bool first=progress<=split;
            Vector3 start=first?moveFrom:dashThrough,end=first?dashThrough:moveTo;
            FaceWorld(end-start);moveFacing=facing;
            attackDirection=(end-start).normalized;
            return first?Vector3.Lerp(start,end,progress/Mathf.Max(.001f,split)):
                Vector3.Lerp(start,end,(progress-split)/Mathf.Max(.001f,1-split));
        }

        private void AnimateDash()
        {
            UnitDirectionalAnimation clip=art.Animation(facing);
            if(clip?.Special?.Length>0)
                ShowSpecialFrame(clip,Mathf.Min(1,clip.Special.Length-1));
            else if(clip?.Attack?.Length>0)ShowFrame(art.Attack(facing),clip);
            float drive=Mathf.Sin(Mathf.Clamp01(moveElapsed/moveDuration)*Mathf.PI);
            visualRoot.localRotation=Quaternion.Euler(0,0,-attackDirection.x*19);
            visualRoot.localPosition=attackDirection*.08f+Vector3.up*(.035f+drive*.035f);
            visualRoot.localScale=new Vector3(baseScale*(1+.09f*Mathf.Abs(attackDirection.x)),baseScale*.94f,1);
            if(moveElapsed+.00001f>=moveDuration){dashMotion=false;CompleteAbilityPose();}
        }

        private void AnimateFlight()
        {
            var clip=art.Animation(facing);
            if(clip?.Special?.Length>0)
                ShowSpecialFrame(clip,moveElapsed<.12f?0:1+Mathf.FloorToInt((moveElapsed-.12f)*8)%Mathf.Max(1,clip.Special.Length-1));
            else AnimateIdle();
            float progress=Mathf.Clamp01(moveElapsed/moveDuration);
            float lift=Mathf.Sin(progress*Mathf.PI)*.42f;
            visualRoot.localPosition=Vector3.up*lift;
            visualRoot.localRotation=Quaternion.Euler(0,0,Mathf.Sin(progress*Mathf.PI*2)*4);
            shadow.color=new Color(.015f,.025f,.035f,ShadowOpacity-lift*.2f);
            specialActive=false;
        }

        private RollParts GetRollParts(Sprite source)
        {
            if(source==null)return null;
            if(rollParts.TryGetValue(source,out var parts))return parts;
            Rect rect=source.rect;float cut=Mathf.Clamp(Mathf.Round(rect.height*.40f),1,rect.height-1);
            float ppu=source.pixelsPerUnit;
            parts=new RollParts
            {
                Head=Sprite.Create(source.texture,new Rect(rect.x,rect.y+cut,rect.width,rect.height-cut),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect),
                Lower=Sprite.Create(source.texture,new Rect(rect.x,rect.y,rect.width,cut),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect),
                Center=source.bounds.center,HeadHeight=(rect.height-cut)/ppu,LowerHeight=cut/ppu
            };
            parts.Head.name="Hymay tucked head";parts.Lower.name="Hymay tucked legs";
            rollParts[source]=parts;return parts;
        }

        private void AnimateBackflip()
        {
            if (art.AuthoredBodyMotion)
            {
                var clip = art.Animation(facing);
                float progress = Mathf.Clamp01(moveElapsed / moveDuration);
                var poses = clip?.HopLeft;
                if (poses != null && poses.Length >= 3)
                    ShowFrame(poses[progress < .15f ? 0 : progress < .85f ? 1 : 2], clip);
                Vector3 center = body.sprite != null ? body.sprite.bounds.center : Vector3.zero;
                if (body.flipX) center.x = -center.x;
                body.transform.localPosition = -center;
                float lift = Mathf.Sin(progress * Mathf.PI) * .82f;
                visualRoot.localPosition = center * baseScale + Vector3.up * lift;
                float authoredSpin = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .08f) / .84f));
                visualRoot.localRotation = Quaternion.Euler(0, 0, (moveTo.x < moveFrom.x ? 1 : -1) * 360 * authoredSpin);
                shadow.transform.localScale *= 1 - lift * .23f;
                shadow.color = new Color(.015f, .025f, .035f, ShadowOpacity - lift * .20f);
                specialActive = false;
                return;
            }
            var parts=GetRollParts(art.Idle(facing));if(parts==null)return;
            float p=Mathf.Clamp01(moveElapsed/moveDuration);
            float tuck=Mathf.SmoothStep(0,1,p/.20f)*(1-Mathf.SmoothStep(0,1,(p-.78f)/.20f));
            float spin=Mathf.SmoothStep(0,1,Mathf.Clamp01((p-.08f)/.84f));
            float height=Mathf.Sin(p*Mathf.PI)*.82f;
            body.sprite=parts.Head;body.transform.localPosition=Vector3.up*(parts.LowerHeight*.5f*(1-tuck));
            body.transform.localScale=new Vector3(1-tuck*.10f,1-tuck*.03f,1);
            SetPart(torso,parts.Lower,Vector3.down*(parts.HeadHeight*.5f*(1-tuck*.66f)));
            torso.flipX=body.flipX;torso.transform.localScale=new Vector3(1-tuck*.34f,1-tuck*.78f,1);
            // Tuck the legs into the hood/body silhouette and rotate around its centre,
            // rather than spinning the feet around the board anchor.
            visualRoot.localPosition=parts.Center*baseScale+Vector3.up*height;
            visualRoot.localRotation=Quaternion.Euler(0,0,(moveTo.x<moveFrom.x?1:-1)*360*spin);
            shadow.transform.localScale*=1-height*.23f;
            shadow.color=new Color(.015f,.025f,.035f,ShadowOpacity-height*.20f);
            specialActive=false;
        }

        private void AnimatePull()
        {
            Vector3 direction=(moveTo-moveFrom).normalized;
            visualRoot.localRotation=Quaternion.Euler(0,0,-direction.x*12);
            visualRoot.localPosition=Vector3.up*.04f;
            visualRoot.localScale=new Vector3(baseScale*1.04f,baseScale*.94f,1);
        }

        private void ResetPose()
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one * baseScale;
            body.enabled = true;
            body.color = Color.white;
            body.flipX = art != null && (art.Animation(facing)?.FlipX ?? false);
            body.sprite = art != null ? art.Idle(facing) : null;
            body.transform.localPosition=Vector3.zero;
            body.transform.localRotation=Quaternion.identity;
            body.transform.localScale=Vector3.one;
            torso.enabled = leftFoot.enabled = rightFoot.enabled = false;
            if(hammerJaw!=null)hammerJaw.enabled=false;
            if(hammerNeck!=null)hammerNeck.enabled=false;
            shadow.enabled = !dead;
            // Compensate the presentation arc: the simple oval stays on the board while the body steps.
            shadow.transform.localPosition = new Vector3(0, -.035f-groundLift, .02f);
            shadow.transform.localRotation=Quaternion.identity;
            shadow.transform.localScale = ContactShadowScale(art, mapper != null ? mapper.CellSize.x : ReferenceTileWidth);
            shadow.color = new Color(.015f, .025f, .035f, ShadowOpacity);
        }

        private void AnimateIdle()
        {
            if (woodenDummy)
            {
                RockWoodenDummy(Mathf.Sin(idleElapsed * 2.6f) * 2f, 0);
                return;
            }
            UnitDirectionalAnimation clip = art.Animation(facing);
            bool hasPose = clip != null && ShowLoop(clip.Idle, idleElapsed, clip.IdleFramesPerSecond, clip);
            // A single authored drawing still needs a little life while waiting.
            // Multi-frame idles already contain that motion; keep their artwork unchanged.
            if (hasPose && clip.Idle.Length > 1) return;
            if (!hasPose && art.ProvisionalDirectionalProjection) SetRig();
            float breath = Mathf.Sin(idleElapsed * 2.4f) * .012f;
            if(!hasPose)
            {
                float rate=Heavy?1.8f:3.1f;
                breath=Mathf.Sin(idleElapsed*rate)*.018f;
                visualRoot.localRotation=Quaternion.Euler(0,0,Mathf.Sin(idleElapsed*rate*.5f)*(Heavy?.6f:1.5f));
                if(unit!=null&&unit.IsEnergyLocked)visualRoot.localPosition=Vector3.up*(.015f+Mathf.Abs(Mathf.Sin(idleElapsed*11))*.025f);
            }
            float projection = DirectionProjection();
            visualRoot.localScale = new Vector3(baseScale * projection * (1 - breath * .4f), baseScale * (1 + breath), 1);
        }

        private void AnimateHop()
        {
            facing=moveFacing;
            var clip=art.Animation(facing);
            body.enabled=true;body.sprite=art.Idle(facing);body.flipX=clip?.FlipX??false;
            torso.enabled=leftFoot.enabled=rightFoot.enabled=false;
            float t=Mathf.Clamp01(moveElapsed/moveDuration);
            var profile=TileHopProfile.For(art.Id);
            if(profile.Walk || art.TravelMotion == UnitTravelMotion.Flying)
            {
                if(clip?.Move?.Length>0){body.sprite=clip.Move[Mathf.FloorToInt(walkElapsed*clip.MoveFramesPerSecond)%clip.Move.Length];body.flipX=(clip.FlipX!=clip.MoveFlipX);}
                return;
            }
            if (art.TravelMotion == UnitTravelMotion.AlternatingHop ||
                (art.TravelMotion == UnitTravelMotion.WholeBodyHop && art.WholeBodyHopUsesPoses))
            {
                // New full-body poses opt in; old Kazuru triplets remain deliberately unused.
                // Poses contain articulation only. The travel loop owns the sole elevation arc.
                var poses = art.TravelMotion == UnitTravelMotion.WholeBodyHop
                    ? clip?.HopLeft : leftFootHop ? clip?.HopLeft : clip?.HopRight;
                if (poses != null && poses.Length >= 3)
                {
                    int phase = t < profile.Prepare ? 0 : t < profile.Prepare + profile.Air ? 1 : 2;
                    if (poses[phase] != null) body.sprite = poses[phase];
                }
            }
            float launch=Mathf.Sin(Mathf.Clamp01(t/profile.Prepare)*Mathf.PI);
            float landing=Mathf.Sin(Mathf.Clamp01((t-profile.Prepare-profile.Air)/(1-profile.Prepare-profile.Air))*Mathf.PI);
            float air=Mathf.Sin(profile.Flight(t)*Mathf.PI);
            float squash=(launch+landing)*profile.Squash;
            if (woodenDummy)
            {
                // Rigid wood tips onto its base, then hops toward the opposite side on the next tile.
                float tilt = dummyHopSide * (launch * -9f + air * 17f - landing * 7f);
                float sway = dummyHopSide * air * Mathf.Abs(mapper != null ? mapper.CellSize.x : 1f) * .09f;
                RockWoodenDummy(tilt, sway);
                shadow.transform.localScale *= 1 - air * .18f;
                shadow.color = new Color(.015f, .025f, .035f, ShadowOpacity - air * .1f);
                return;
            }
            visualRoot.localScale=new Vector3(baseScale*DirectionProjection()*(1+squash*.65f-air*.025f),
                baseScale*(1-squash+air*.025f),1);
            // One planted takeoff, one clear arc, one landing per tile. Never cycle walking frames.
            shadow.transform.localScale*=1-air*.09f;
            shadow.color=new Color(.015f,.025f,.035f,ShadowOpacity-air*.05f);
        }

        private void RockWoodenDummy(float angle, float sideways)
        {
            visualRoot.localRotation = Quaternion.Euler(0, 0, angle);
            // Compensate the tilted base so its lower corner never sinks through the board.
            float halfWidth = (body.sprite != null ? body.sprite.bounds.extents.x : 0) * baseScale;
            visualRoot.localPosition = new Vector3(sideways, Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * halfWidth, 0);
        }

        private void AnimateAttack(float dt)
        {
            attackElapsed += dt;
            float windup = attackContactDelay <= 0 ? 1 : Mathf.Clamp01(attackElapsed / attackContactDelay);
            bool hasContact = attackElapsed + .00001f >= attackContactDelay;
            float recovery = hasContact ? Mathf.Clamp01((attackElapsed - attackContactDelay - attackHoldDuration) / attackRecoveryDuration) : 0;
            float impact = hasContact ? 1 - Mathf.SmoothStep(0, 1, recovery) : 0;
            float extension = Mathf.SmoothStep(0, 1, Mathf.Clamp01((windup - .65f) / .35f));
            UnitDirectionalAnimation clip = art.Animation(facing);
            if (clip != null && clip.Attack != null && clip.Attack.Length > 0)
            {
                // Authored limb poses own the motion; no rigid sprite rotation or projected silhouette.
                int contactFrame = Mathf.Clamp(clip.AttackContactFrame, 0, clip.Attack.Length - 1);
                int frame = !hasContact
                    ? Mathf.Min(Mathf.Max(0, contactFrame - 1), Mathf.FloorToInt(windup * contactFrame))
                    : Mathf.Min(clip.Attack.Length - 1, contactFrame + Mathf.FloorToInt(recovery * (clip.Attack.Length - contactFrame)));
                if(UsesActionPreparation)frame=dagataElapsedSource!=null?DagataAttackFrame(clip,windup,recovery,hasContact):CharacterCombatMotion.AttackFrame(clip,windup,recovery,hasContact);
                ShowFrame(clip.Attack[frame], clip);
                if(!AnimateCharacterAttack(clip,windup,recovery,hasContact))
                {
                // The simulation owns contact timing. These offsets only add readable anticipation and follow-through.
                float anticipation=hasContact?0:windup*(1-extension);
                float drive=hasContact?impact:extension;
                float lunge=Mathf.Max(0,clip.AttackLungeDistance);
                float squash=Mathf.Clamp(clip.AttackImpactSquash,0,.3f);
                visualRoot.localPosition=attackDirection*(drive*lunge-anticipation*lunge*.34f);
                visualRoot.localScale=new Vector3(baseScale*(1+drive*squash),baseScale*(1-drive*squash*.62f-anticipation*squash*.38f),1);
                visualRoot.localRotation=Quaternion.Euler(0,0,-attackDirection.x*(drive*4-anticipation*2));
                }
            }
            else if (art.Id == "bugaloo")
            {
                if (windup > .18f && recovery < .72f) body.sprite = art.Attack(facing);
                float jump = hasContact ? 0 : Mathf.Sin(Mathf.Clamp01((windup - .18f) / .82f) * Mathf.PI) * .55f;
                float anticipation = hasContact ? 0 : Mathf.Sin(Mathf.Clamp01(windup / .28f) * Mathf.PI);
                float forward = hasContact ? impact : Mathf.Sin(windup * Mathf.PI * .5f);
                visualRoot.localPosition = Vector3.up * jump + attackDirection * (.13f * forward);
                visualRoot.localScale = new Vector3(baseScale * (1 + impact * .2f + anticipation * .06f), baseScale * (1 - impact * .15f - anticipation * .07f), 1);
                shadow.transform.localScale *= 1 - jump * .4f;
                // The ground anchor and board occupancy never follow this visual jump.
            }
            else if (art.Id == "popow")
            {
                // Each right-hand pose is authored separately; never flip it.
                if (windup >= .7f && recovery < .72f) body.sprite = art.Attack(facing);
                float reach = hasContact ? impact * .15f : Mathf.Lerp(-Mathf.Min(1, windup / .65f) * .055f, .15f, extension);
                visualRoot.localPosition = attackDirection * reach;
                visualRoot.localRotation = Quaternion.Euler(0, 0, -attackDirection.x * (hasContact ? impact : extension) * 5);
            }
            else
            {
                // Local body/foot articulation is the provisional attack for both Whelps.
                float release = hasContact ? impact : extension;
                if (SetRig())
                {
                    UnitSpriteRig rig = art.WalkRigs[(int)facing];
                    torso.transform.localPosition = (Vector3)rig.BodyOffset + attackDirection * (release * .075f - windup * (1 - extension) * .025f);
                    torso.transform.localRotation = Quaternion.Euler(0, 0, -attackDirection.x * release * 11);
                    leftFoot.transform.localRotation = Quaternion.Euler(0, 0, release * 7);
                    rightFoot.transform.localRotation = Quaternion.Euler(0, 0, -release * 7);
                }
                visualRoot.localScale = new Vector3(baseScale * DirectionProjection() * (1 + release * .035f), baseScale * (1 - windup * (1 - extension) * .08f), 1);
                float anticipation=windup*(1-extension)*(hasContact?0:1);
                visualRoot.localPosition = attackDirection * (release * (rangedAttack ? -.07f : Heavy?.19f:.14f)-anticipation*.05f);
                visualRoot.localRotation=Quaternion.Euler(0,0,-attackDirection.x*(release*7-anticipation*4));
                if(!rangedAttack)visualRoot.localScale=new Vector3(baseScale*DirectionProjection()*(1+impact*.13f),baseScale*(1-anticipation*.09f-impact*.06f),1);
            }
            if (hasContact && recovery >= 1) attacking = false;
        }

        private void ApplyCombatPose(CharacterCombatMotion.Pose pose)
        {
            visualRoot.localPosition=pose.Offset;
            visualRoot.localScale=new Vector3(baseScale*pose.Scale.x,baseScale*pose.Scale.y,1);
            visualRoot.localRotation=Quaternion.Euler(0,0,pose.Angle);
        }

        private bool AnimateCharacterAttack(UnitDirectionalAnimation clip,float windup,float recovery,bool contact)
        {
            if(art.AuthoredBodyMotion && CharacterCombatMotion.Supports(art.Id))
            {
                ShowFrame(clip.Attack[CharacterCombatMotion.AttackFrame(clip,windup,recovery,contact)],clip);
                ApplyCombatPose(CharacterCombatMotion.Evaluate(art.Id,clip,attackDirection,windup,recovery,contact));
                return true;
            }
            if(art.Id!="popow"&&art.Id!="atong"&&art.Id!="hymay")return false;
            // Hold the anticipation, accelerate over the last fifth, then give contact a short readable hold.
            // None of these curves can move the simulation's impact or shorten a reset attack windup.
            float snap=Mathf.Pow(Mathf.Clamp01((windup-.8f)/.2f),3);
            float hold=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((recovery-.22f)/.78f));
            float drive=contact?hold:snap;
            float coil=contact?0:Mathf.SmoothStep(0,1,windup/.68f)*(1-snap);
            float reach=Mathf.Max(.01f,clip.AttackLungeDistance);
            float squash=Mathf.Clamp(clip.AttackImpactSquash,0,.3f);
            if(contact&&recovery<.22f)ShowFrame(art.Attack(facing),clip);
            if(art.Id=="popow")
            {
                // A planted windup and a fast forward shoulder drive read as a punch, not a soft sway.
                visualRoot.localPosition=attackDirection*(drive*reach-coil*reach*.42f)+Vector3.up*(coil*.018f);
                visualRoot.localRotation=Quaternion.Euler(0,0,attackDirection.x*(coil*10-drive*16));
                body.transform.localRotation=Quaternion.Euler(0,0,attackDirection.x*(coil*4-drive*5));
                visualRoot.localScale=new Vector3(baseScale*(1+drive*squash),baseScale*(1-coil*.07f-drive*squash*.45f),1);
            }
            else if(art.Id=="atong")
            {
                AnimateHammerJaw(clip,coil,drive,contact,windup,reach,squash);
            }
            else
            {
                // Shift the weight onto the rear leg, then snap the shoulder as the whip leaves the hand.
                float hop=contact?0:Mathf.Sin(Mathf.Clamp01(windup/.88f)*Mathf.PI)*.085f;
                visualRoot.localPosition=attackDirection*(drive*reach-coil*reach*.6f)+Vector3.up*hop;
                visualRoot.localRotation=Quaternion.Euler(0,0,attackDirection.x*(coil*13-drive*17));
                body.transform.localRotation=Quaternion.Euler(0,0,-attackDirection.x*drive*6);
                visualRoot.localScale=new Vector3(baseScale*(1+drive*squash*.7f),baseScale*(1-coil*.06f-drive*squash*.3f),1);
            }
            return true;
        }

        private sealed class HammerParts
        {
            public Sprite Lower,Jaw,Neck;
            public Vector3 Hinge;
        }

        private HammerParts GetHammerParts(Sprite source)
        {
            if(source==null)return null;
            if(hammerParts.TryGetValue(source,out HammerParts cached))return cached;
            // Slice the existing authored image into a planted lower body and the large jaw/head.
            // Its original pixels, palette and silhouette are retained; only the jaw hinge is articulated.
            Rect rect=source.rect;
            float cut=Mathf.Clamp(Mathf.Round(rect.height*.34f),1,rect.height-1);
            float ppu=source.pixelsPerUnit;
            var parts=new HammerParts();
            parts.Lower=Sprite.Create(source.texture,new Rect(rect.x,rect.y,rect.width,cut),
                new Vector2(source.pivot.x/rect.width,source.pivot.y/cut),ppu,0,SpriteMeshType.FullRect);
            parts.Jaw=Sprite.Create(source.texture,new Rect(rect.x,rect.y+cut,rect.width,rect.height-cut),
                new Vector2(source.pivot.x/rect.width,0),ppu,0,SpriteMeshType.FullRect);
            float neckWidth=Mathf.Max(2,Mathf.Round(rect.width*.16f));
            float neckHeight=Mathf.Max(2,Mathf.Round(rect.height*.045f));
            parts.Neck=Sprite.Create(source.texture,new Rect(rect.x+(rect.width-neckWidth)*.5f,rect.y+cut-neckHeight,neckWidth,neckHeight),
                new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);
            parts.Lower.name="Atong planted body";parts.Jaw.name="Atong hammer jaw";
            parts.Hinge=new Vector3(0,(cut-source.pivot.y)/ppu,0);
            hammerParts.Add(source,parts);return parts;
        }

        private void AnimateHammerJaw(UnitDirectionalAnimation clip,float coil,float drive,bool contact,float windup,float reach,float squash)
        {
            HammerParts parts=GetHammerParts(art.Idle(facing));
            if(parts==null)return;
            body.enabled=false;
            SetPart(torso,parts.Lower,Vector2.zero);
            torso.flipX=clip.FlipX;
            SetPart(hammerJaw,parts.Jaw,parts.Hinge);
            hammerJaw.flipX=clip.FlipX;
            float inverseScale=1/Mathf.Max(.001f,baseScale);
            float side=Mathf.Abs(attackDirection.x)>.25f?Mathf.Sign(attackDirection.x):0;
            // Pull the entire head far behind the planted body, hold it there, then drive the jaw down.
            // These world-space excursions remain legible for every size and all eight directions.
            float profile=1-Mathf.Abs(attackDirection.y);
            Vector3 offset=-attackDirection*(coil*.48f)+Vector3.up*(coil*.26f*profile)
                +attackDirection*(drive*.36f)-Vector3.up*(drive*.24f*profile);
            hammerJaw.transform.localPosition+=offset*inverseScale;
            hammerJaw.transform.localRotation=Quaternion.Euler(0,0,side*(coil*55-drive*86));
            hammerJaw.transform.localScale=new Vector3(1+drive*.12f,1-coil*(side==0?.12f:0)-drive*(side==0?.42f:.10f),1);
            visualRoot.localPosition=Vector3.zero;
            torso.transform.localScale=new Vector3(1+drive*.12f,1-drive*.12f,1);

            // Stretch a small strip of the original neck pixels between both articulated pieces.
            // The connecting hinge stays behind the body/head instead of leaving a floating-head gap.
            Vector3 neckStart=parts.Hinge-Vector3.up*.04f*inverseScale;
            Vector3 neckEnd=hammerJaw.transform.localPosition;
            Vector3 neckDelta=neckEnd-neckStart;
            SetPart(hammerNeck,parts.Neck,(neckStart+neckEnd)*.5f);
            hammerNeck.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(neckDelta.y,neckDelta.x)*Mathf.Rad2Deg-90);
            hammerNeck.transform.localScale=new Vector3(1,(neckDelta.magnitude+.08f*inverseScale)/parts.Neck.bounds.size.y,1);
        }

        private void ApplyDirectionalArticulation()
        {
            if (!art.ProvisionalDirectionalProjection || !torso.enabled) return;
            float angle = (int)facing * Mathf.PI / 4;
            float x = Mathf.Sin(angle), y = Mathf.Cos(angle);
            // Reposition existing silhouette parts only; no face, markings, or replacement drawing.
            torso.transform.localPosition += new Vector3(x * .025f, y * .012f, 0);
            torso.transform.localRotation *= Quaternion.Euler(0, 0, -x * 3);
            torso.transform.localScale = new Vector3(1, 1 - y * .025f, 1);
            leftFoot.transform.localPosition += new Vector3(0, -x * .008f, 0);
            rightFoot.transform.localPosition += new Vector3(0, x * .008f, 0);
        }

        private void AnimateRevive(float dt)
        {
            reviveElapsed += dt;
            float progress = Mathf.Clamp01(reviveElapsed / reviveDuration);
            var clip = art.Animation(facing);
            if (clip != null && clip.Revive != null && clip.Revive.Length > 0)
            {
                int frame = Mathf.Min(clip.Revive.Length - 1, Mathf.FloorToInt(progress * clip.Revive.Length));
                ShowFrame(clip.Revive[frame], clip);
            }
            else
            {
                visualRoot.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(180,360,Mathf.SmoothStep(0,1,progress)));
                visualRoot.localPosition=Vector3.up*Mathf.Sin(progress*Mathf.PI)*.18f;
                visualRoot.localScale*=.75f+.25f*progress;
            }
            // Health and return to idle are controlled solely by the simulation's Revived event.
        }

        private void AnimateSpecial(float dt)
        {
            specialElapsed+=dt;var clip=art.Animation(facing);
            if(clip?.Special==null||clip.Special.Length==0){specialActive=false;return;}
            float progress=Mathf.Clamp01(specialElapsed/Mathf.Max(.0001f,specialDuration));
            float release=Mathf.Clamp(clip.SpecialReleasePoint,.15f,.85f);
            float releasedRecovery=progress;
            if(releasedEronyaSpecial&&!phasedSpecial)progress=Mathf.Lerp(release,1,progress);
            if(phasedSpecial&&!flowerPreparing)
            {
                if(specialElapsed<specialCast)progress=release*Mathf.Clamp01(specialElapsed/Mathf.Max(.0001f,specialCast));
                else if(specialElapsed<specialCast+specialHold)progress=release;
                else progress=Mathf.Lerp(release,1,Mathf.Clamp01((specialElapsed-specialCast-specialHold)/Mathf.Max(.0001f,specialReturn)));
                if(releasedEronyaSpecial)releasedRecovery=Mathf.Clamp01((specialElapsed-specialCast-specialHold)/Mathf.Max(.0001f,specialReturn));
            }
            if(flowerPreparing)
            {
                progress=specialElapsed<specialCast ? .95f*Mathf.Clamp01(specialElapsed/Mathf.Max(.0001f,specialCast)) :
                    specialElapsed<specialCast+specialHold?.95f:Mathf.Lerp(.95f,1,Mathf.Clamp01((specialElapsed-specialCast-specialHold)/Mathf.Max(.0001f,specialReturn)));
                AnimateFlower(clip,progress);
                if(specialElapsed>=specialDuration){flowerPreparing=specialActive=false;CompleteAbilityPose();}
                return;
            }
            if(art.AuthoredBodyMotion && CharacterCombatMotion.Supports(art.Id))
            {
                // Cast/hold/recovery are supplied by the match. Keep the preparation drawing until
                // actual release; equal-duration sprite indexing showed the release pose too early.
                bool released=phasedSpecial?specialElapsed+.00001f>=specialCast:progress+.00001f>=release;
                float windup=phasedSpecial?Mathf.Clamp01(specialElapsed/Mathf.Max(.0001f,specialCast)):Mathf.Clamp01(progress/release);
                float recovery=phasedSpecial?Mathf.Clamp01((specialElapsed-specialCast-specialHold)/Mathf.Max(.0001f,specialReturn)):
                    Mathf.Clamp01((progress-release)/(1-release));
                float angle=(int)facing*Mathf.PI/4;
                // Suki turns toward her allies when releasing fish; a previous enemy
                // attack direction must not pull her throwing pose the opposite way.
                Vector3 castDirection=art.Id!="suki"&&attackDirection.sqrMagnitude>.0001f?attackDirection:new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
                if(released && art.Id=="hymay")
                {
                    // Keep the throwing hand aimed at the victim. The tucked roll drawing belongs
                    // to the separately animated backstep, not the grapple's release.
                    ShowFrame(recovery<.56f?art.Attack(facing):art.Idle(facing),clip);
                }
                else if(released && art.Id=="tokoro")
                {
                    // Lead the headbutt with a braced, closed-eye pose instead of the raised-arm roar.
                    Sprite braced=clip.Idle.Length>1?clip.Idle[1]:art.Idle(facing);
                    ShowFrame(recovery<.56f?braced:art.Idle(facing),clip);
                }
                else ShowSpecialFrame(clip,CharacterCombatMotion.SpecialFrame(clip,recovery,released));
                ApplyCombatPose(CharacterCombatMotion.Evaluate(art.Id,clip,castDirection,windup,recovery,released,true));
                if(specialElapsed>=specialDuration){specialActive=false;CompleteAbilityPose();}
                return;
            }
            int frame=releasedEronyaSpecial?(dagataElapsedSource!=null?DagataSpecialFrame(clip,releasedRecovery):CharacterCombatMotion.SpecialFrame(clip,releasedRecovery,true)):
                Mathf.Min(clip.Special.Length-1,Mathf.FloorToInt(progress*clip.Special.Length));
            ShowSpecialFrame(clip,frame);
            float anticipation=progress<release?Mathf.SmoothStep(0,1,progress/release):0;
            float follow=progress>=release?1-Mathf.SmoothStep(0,1,(progress-release)/(1-release)):0;
            float force=progress<release?-anticipation*.28f:follow;
            float facingAngle=(int)facing*Mathf.PI/4;
            // The current Super preparation already faced its own target, which may differ
            // from the previous basic. Keep the body lunge aligned with that release drawing.
            Vector3 direction=!releasedEronyaSpecial&&attackDirection.sqrMagnitude>.0001f?attackDirection:new Vector3(Mathf.Sin(facingAngle),Mathf.Cos(facingAngle),0);
            visualRoot.localPosition=direction*(force*Mathf.Max(0,clip.SpecialLungeDistance))+
                Vector3.up*(Mathf.Sin(progress*Mathf.PI)*Mathf.Max(0,clip.SpecialLift));
            float punch=follow*Mathf.Clamp(clip.SpecialPunchScale,0,.3f);
            visualRoot.localScale=new Vector3(baseScale*(1+punch),baseScale*(1-punch*.55f),1);
            visualRoot.localRotation=Quaternion.Euler(0,0,-direction.x*force*5);
            if(art.Id=="hymay")
            {
                float cast=Mathf.Pow(Mathf.Clamp01(progress/release),2);
                float strike=progress<release?0:1-Mathf.SmoothStep(0,1,(progress-release)/(1-release));
                visualRoot.localPosition=direction*(strike*Mathf.Max(.12f,clip.SpecialLungeDistance)-cast*(1-strike)*.07f)+
                    Vector3.up*(Mathf.Sin(progress*Mathf.PI)*Mathf.Max(.10f,clip.SpecialLift));
                visualRoot.localRotation=Quaternion.Euler(0,0,direction.x*(progress<release?cast*17:-strike*22));
            }
            if(specialElapsed>=specialDuration){specialActive=false;CompleteAbilityPose();}
        }

        private void CompleteAbilityPose()
        {
            // The completed action must show its resting pose immediately, rather than
            // retaining the last special frame until the next render. Timed Supers keep
            // their existing active pose. This never advances or resets the combat clock.
            if(unit!=null&&unit.IsEnergyLocked)return;
            ResetPose();
            if(attacking)AnimateAttack(0);
            else if(moving)AnimateHop();
            else AnimateIdle();
        }

        private void AnimateFlower(UnitDirectionalAnimation clip,float progress)
        {
            // Authored frames show reaching up and holding the flower. The effect carries it to the cell.
            int held=Mathf.Min(1,clip.Special.Length-1);
            int frame=progress<.38f?0:progress<.72f?held:Mathf.Min(clip.Special.Length-1,held+1);
            ShowSpecialFrame(clip,frame);
            float pull=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.45f));
            float plant=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.56f)/.44f));
            float bend=progress<.45f?(1-pull)*.08f:plant*.10f;
            if(art.AuthoredBodyMotion)
            {
                // Lift the flower clearly out of the pot, then lean into its destination cell.
                float angle=(int)facing*Mathf.PI/4;
                Vector3 direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
                visualRoot.localPosition=direction*(plant*.16f)+Vector3.up*(Mathf.Sin(pull*Mathf.PI*.5f)*(1-plant)*Mathf.Max(.16f,clip.SpecialLift));
                visualRoot.localScale=new Vector3(baseScale*(1+plant*.07f),baseScale*(1+pull*(1-plant)*.055f-plant*.12f),1);
                visualRoot.localRotation=Quaternion.Euler(0,0,direction.x*(pull*(1-plant)*7-plant*16));
            }
            else
            {
                visualRoot.localPosition=Vector3.up*(Mathf.Sin(pull*Mathf.PI*.5f)*(1-plant)*Mathf.Max(.06f,clip.SpecialLift)-plant*.025f);
                visualRoot.localScale=new Vector3(baseScale*(1+bend*.35f),baseScale*(1-bend),1);
                visualRoot.localRotation=Quaternion.Euler(0,0,Mathf.Sin((int)facing*Mathf.PI/4)*plant*-10);
            }
        }
        private void ShowSpecialFrame(UnitDirectionalAnimation clip,int frame)
        {
            if(clip?.Special==null||clip.Special.Length==0)return;
            ShowFrame(clip.Special[Mathf.Clamp(frame,0,clip.Special.Length-1)],clip);body.flipX=clip.SpecialFlipX;
        }

        private void AnimateDeath(float dt)
        {
            deathElapsed += dt;
            float t = Mathf.Clamp01(deathElapsed / DeathDuration);
            UnitDirectionalAnimation clip = art.Animation(facing);
            if (clip != null && clip.Death != null && clip.Death.Length > 0)
            {
                int frame = Mathf.Min(clip.Death.Length - 1, Mathf.FloorToInt(t * clip.Death.Length));
                ShowFrame(clip.Death[frame], clip);
                // Keep the authored fallen pose readable before fading; death still finishes after round freeze.
                float frameAlpha = 1 - Mathf.Clamp01((t - .68f) / .32f);
                body.color = new Color(1, 1, 1, frameAlpha);
                body.enabled = t < 1;
                shadow.enabled=false;
                return;
            }
            float collapse = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .65f));
            visualRoot.localRotation = Quaternion.Euler(0, 0, collapse * 76f);
            visualRoot.localScale = new Vector3(baseScale * (1 + .08f * collapse), baseScale * (1 - .28f * collapse), 1);
            visualRoot.localPosition = Vector3.down * (collapse * .05f);
            float alpha = 1 - Mathf.Clamp01((t - .5f) / .5f);
            body.color = new Color(1, 1, 1, alpha);
            body.enabled = t < 1;
            shadow.enabled=false;
        }

        private bool ShowLoop(Sprite[] frames, float elapsed, float framesPerSecond, UnitDirectionalAnimation clip)
        {
            if (frames == null || frames.Length == 0) return false;
            float rate = float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) ? 1 : Mathf.Max(.1f, framesPerSecond);
            int frame = Mathf.FloorToInt(elapsed * rate) % frames.Length;
            return ShowFrame(frames[frame], clip);
        }

        private bool ShowFrame(Sprite frame, UnitDirectionalAnimation clip)
        {
            if (frame == null) return false;
            body.enabled = true;
            body.sprite = frame;
            body.flipX = clip.FlipX;
            torso.enabled = leftFoot.enabled = rightFoot.enabled = false;
            return true;
        }

        private bool SetRig()
        {
            int index = (int)facing;
            if (art.WalkRigs == null || index >= art.WalkRigs.Length || art.WalkRigs[index] == null) return false;
            UnitSpriteRig rig = art.WalkRigs[index];
            if (rig.Body == null || rig.LeftFoot == null || rig.RightFoot == null) return false;
            body.enabled = false;
            SetPart(torso, rig.Body, rig.BodyOffset);
            SetPart(leftFoot, rig.LeftFoot, rig.LeftFootOffset);
            SetPart(rightFoot, rig.RightFoot, rig.RightFootOffset);
            return true;
        }

        private static void SetPart(SpriteRenderer renderer, Sprite sprite, Vector2 offset)
        {
            renderer.enabled = true;
            renderer.color = Color.white;
            renderer.sprite = sprite;
            renderer.transform.localPosition = offset;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
        }

        private float DirectionProjection()
        {
            if (!art.ProvisionalDirectionalProjection) return 1;
            if (facing == UnitFacing.East || facing == UnitFacing.West) return .65f;
            return facing == UnitFacing.North || facing == UnitFacing.South ? 1 : .83f;
        }

        private void UpdateSorting()
        {
            if (body == null) return;
            float groundY = displacementActive ? displacementGroundY : transform.position.y;
            int order = BodySortingOrder(groundY)-2+(flightMotion?80:0);
            body.sortingOrder = torso.sortingOrder = order + 2;
            if(hammerJaw!=null)hammerJaw.sortingOrder=order+3;
            if(hammerNeck!=null)hammerNeck.sortingOrder=order+1;
            leftFoot.sortingOrder = rightFoot.sortingOrder = order + 1;
            // The table surface uses order 18; equal sorting could hide the shadow behind the board.
            shadow.sortingOrder = 19;
        }

        private void OnDestroy()
        {
            foreach(var parts in rollParts.Values)
            {
                if(Application.isPlaying){Destroy(parts.Head);Destroy(parts.Lower);}
                else{DestroyImmediate(parts.Head);DestroyImmediate(parts.Lower);}
            }
            rollParts.Clear();
            foreach(HammerParts parts in hammerParts.Values)
            {
                if(Application.isPlaying){Destroy(parts.Lower);Destroy(parts.Jaw);Destroy(parts.Neck);}
                else{DestroyImmediate(parts.Lower);DestroyImmediate(parts.Jaw);DestroyImmediate(parts.Neck);}
            }
            hammerParts.Clear();
        }

        private static SpriteRenderer NewRenderer(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            if (sharedSpriteMaterial == null)
            {
                sharedSpriteMaterial = Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
                if (sharedSpriteMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null) sharedSpriteMaterial = new Material(shader) { name = "Monster Pouch unlit sprites" };
                }
            }
            if (sharedSpriteMaterial != null) renderer.sharedMaterial = sharedSpriteMaterial;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            return renderer;
        }

        private static Sprite GetShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            // New procedural geometry texture, not a modification of any character asset.
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "unit-ground-disc", filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f);
                float dy = (y + .5f - size * .5f) / (size * .5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(dx * dx + dy * dy <= 1 ? 255 : 0));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            shadowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return shadowSprite;
        }
    }
}
