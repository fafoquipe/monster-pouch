using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Only presentation: never changes board occupancy, damage, or the simulation clock.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitPresentation : MonoBehaviour
    {
        private BattleUnit unit;
        private BoardWorldMapper mapper;
        private UnitArt art;
        private SpriteRenderer body;
        private SpriteRenderer torso;
        private SpriteRenderer leftFoot;
        private SpriteRenderer rightFoot;
        private SpriteRenderer shadow;
        private Transform visualRoot;
        private UnitFacing facing = UnitFacing.South;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private Vector3 attackDirection;
        private float moveElapsed;
        private float walkElapsed;
        private float moveDuration;
        private float attackElapsed;
        private float attackContactDelay;
        private float attackRecoveryDuration;
        private float deathElapsed;
        private float reviveElapsed, reviveDuration;
        private bool reviving;
        private float idleElapsed;
        private float baseScale = 1f;
        private bool moving;
        private bool attacking;
        private bool dead;
        private bool rangedAttack;
        private bool frozen;
        private static Sprite shadowSprite;
        private static Material sharedSpriteMaterial;
        // Presentation only: 20% larger art/rig around the unchanged foot anchor; stats and timing are unchanged.
        private const float VisualScaleMultiplier = 1.2f;
        private const float DefaultRecoveryDuration = .18f;
        private const float DeathDuration = .62f;

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

        public void Configure(BattleUnit newUnit, BoardWorldMapper newMapper, UnitArt newArt)
        {
            unit = newUnit;
            mapper = newMapper;
            art = newArt;
            if (visualRoot == null)
            {
                visualRoot = new GameObject("unit-visual").transform;
                visualRoot.SetParent(transform, false);
                body = NewRenderer("pose", visualRoot);
                torso = NewRenderer("body-rig", visualRoot);
                leftFoot = NewRenderer("left-foot-rig", visualRoot);
                rightFoot = NewRenderer("right-foot-rig", visualRoot);
                shadow = NewRenderer("ground-shadow", transform);
                shadow.sprite = GetShadowSprite();
                shadow.color = new Color(.015f, .025f, .035f, .22f);
            }

            // Old prototype renderers are owned by their prototype. Avoid duplicate art if a caller reuses that GO.
            SpriteRenderer oldRenderer = GetComponent<SpriteRenderer>();
            if (oldRenderer != null) oldRenderer.enabled = false;
            moving = attacking = dead = frozen = reviving = false;
            deathElapsed = idleElapsed = walkElapsed = 0;
            facing = unit != null && unit.Side == BoardSide.Blue ? UnitFacing.North : UnitFacing.South;
            Sprite reference = art != null ? art.Portrait : null;
            baseScale = reference != null
                ? VisualScaleMultiplier * art.WorldWidth * reference.pixelsPerUnit / Mathf.Max(1, art.ReferencePixelWidth) : 1;
            ResetPose();
            UpdateSorting();
        }

        /// <summary>Delta is in board coordinates; positive board Y faces south.</summary>
        public void Face(int deltaX, int deltaY)
        {
            if (dead || reviving || (deltaX == 0 && deltaY == 0)) return;
            float angle = Mathf.Atan2(deltaX, -deltaY) * Mathf.Rad2Deg;
            facing = (UnitFacing)((Mathf.RoundToInt(angle / 45f) + 8) % 8);
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
            Vector3 direction = to - from;
            Face(Mathf.RoundToInt(Mathf.Sign(direction.x)) * (Mathf.Abs(direction.x) > .001f ? 1 : 0),
                -Mathf.RoundToInt(Mathf.Sign(direction.y)) * (Mathf.Abs(direction.y) > .001f ? 1 : 0));
            moveFrom = from;
            moveTo = to;
            moveElapsed = 0;
            moveDuration = Mathf.Max(.001f, duration);
            moving = duration > 0;
            transform.position = moving ? from : to;
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
            if (dead || reviving || frozen || visualRoot == null) return;
            Vector3 delta = target - transform.position;
            FaceWorld(delta);
            attackDirection = delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.down;
            attackDirection.z = 0;
            rangedAttack = projectile;
            float delay = projectile && unit != null ? CombatSimulation.GetAttackWindup(unit) : impactDelay;
            attackContactDelay = float.IsNaN(delay) || float.IsInfinity(delay) ? .2f : Mathf.Max(0, delay);
            float interval = unit != null ? unit.BaseStats.AttackInterval : attackContactDelay + DefaultRecoveryDuration;
            attackRecoveryDuration = Mathf.Clamp(interval - attackContactDelay - .02f, .01f, DefaultRecoveryDuration);
            attackElapsed = 0;
            attacking = true;
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
            reviving = false;
            dead = true;
            attacking = moving = false;
            deathElapsed = 0;
            if (body != null && art != null) body.sprite = art.Idle(facing);
        }

        public void BeginRevive(float duration)
        {
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
            reviving = dead = attacking = moving = false;
            deathElapsed = 0;
            SnapToCell();
            ResetPose();
            UpdateSorting();
        }

        public void SnapToCell()
        {
            if (unit == null || mapper == null || unit.CurrentCell == null) return;
            if (mapper.TryGetWorldPosition(unit.CurrentCell, out Vector3 position)) transform.position = position;
            moving = false;
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
            if (frozen && !dead) return;
            float dt = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0 : Mathf.Max(0, deltaTime);
            idleElapsed += dt;
            if (moving && !dead)
            {
                moveElapsed += dt;
                walkElapsed += dt;
                transform.position = Vector3.Lerp(moveFrom, moveTo, Mathf.Clamp01(moveElapsed / moveDuration));
                if (moveElapsed >= moveDuration) moving = false;
            }
            ResetPose();
            if (reviving) AnimateRevive(dt);
            else if (dead) AnimateDeath(dt);
            else if (attacking) AnimateAttack(dt);
            else if (moving) AnimateWalk();
            else AnimateIdle();
            if (!dead && !reviving) ApplyDirectionalArticulation();
            UpdateSorting();
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
            torso.enabled = leftFoot.enabled = rightFoot.enabled = false;
            shadow.enabled = true;
            shadow.transform.localPosition = new Vector3(0, -.035f, .02f);
            shadow.transform.localScale = new Vector3((art != null ? art.WorldWidth * .7f : .6f) * VisualScaleMultiplier, .17f, 1);
            shadow.color = new Color(.015f, .025f, .035f, .22f);
        }

        private void AnimateIdle()
        {
            UnitDirectionalAnimation clip = art.Animation(facing);
            if (clip != null && ShowLoop(clip.Idle, idleElapsed, clip.IdleFramesPerSecond, clip)) return;
            if (art.ProvisionalDirectionalProjection) SetRig();
            float breath = Mathf.Sin(idleElapsed * 2.4f) * .008f;
            float projection = DirectionProjection();
            visualRoot.localScale = new Vector3(baseScale * projection * (1 - breath * .4f), baseScale * (1 + breath), 1);
        }

        private void AnimateWalk()
        {
            UnitDirectionalAnimation clip = art.Animation(facing);
            if (clip != null && ShowLoop(clip.Move, walkElapsed, clip.MoveFramesPerSecond, clip)) return;
            float phase = (moveElapsed / moveDuration) * Mathf.PI * 2;
            float step = Mathf.Sin(phase);
            if (SetRig())
            {
                UnitSpriteRig rig = art.WalkRigs[(int)facing];
                leftFoot.transform.localPosition = (Vector3)rig.LeftFootOffset + new Vector3(-step * .018f, Mathf.Max(0, step) * .034f, 0);
                rightFoot.transform.localPosition = (Vector3)rig.RightFootOffset + new Vector3(step * .018f, Mathf.Max(0, -step) * .034f, 0);
                leftFoot.transform.localRotation = Quaternion.Euler(0, 0, step * 4);
                rightFoot.transform.localRotation = Quaternion.Euler(0, 0, -step * 4);
                torso.transform.localPosition = (Vector3)rig.BodyOffset + new Vector3(step * .008f, Mathf.Abs(step) * .012f, 0);
                torso.transform.localRotation = Quaternion.Euler(0, 0, -step * 1.1f);
            }
            visualRoot.localPosition = new Vector3(0, Mathf.Abs(step) * .018f, 0);
            visualRoot.localScale = new Vector3(baseScale * DirectionProjection(), baseScale, 1);
        }

        private void AnimateAttack(float dt)
        {
            attackElapsed += dt;
            float windup = attackContactDelay <= 0 ? 1 : Mathf.Clamp01(attackElapsed / attackContactDelay);
            bool hasContact = attackElapsed + .00001f >= attackContactDelay;
            float recovery = hasContact ? Mathf.Clamp01((attackElapsed - attackContactDelay) / attackRecoveryDuration) : 0;
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
                ShowFrame(clip.Attack[frame], clip);
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
                visualRoot.localPosition = attackDirection * release * (rangedAttack ? .035f : .1f);
            }
            if (hasContact && recovery >= 1) attacking = false;
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
            // Health and return to idle are controlled solely by the simulation's Revived event.
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
                shadow.color = new Color(.015f, .025f, .035f, .22f * frameAlpha);
                body.enabled = shadow.enabled = t < 1;
                return;
            }
            float collapse = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .65f));
            visualRoot.localRotation = Quaternion.Euler(0, 0, collapse * 76f);
            visualRoot.localScale = new Vector3(baseScale * (1 + .08f * collapse), baseScale * (1 - .28f * collapse), 1);
            visualRoot.localPosition = Vector3.down * (collapse * .05f);
            float alpha = 1 - Mathf.Clamp01((t - .5f) / .5f);
            body.color = new Color(1, 1, 1, alpha);
            shadow.color = new Color(.015f, .025f, .035f, .22f * alpha);
            body.enabled = shadow.enabled = t < 1;
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
            int order = 120 - Mathf.RoundToInt(transform.position.y * 4);
            body.sortingOrder = torso.sortingOrder = order + 2;
            leftFoot.sortingOrder = rightFoot.sortingOrder = order + 1;
            shadow.sortingOrder = 18;
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
