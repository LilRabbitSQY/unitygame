using System.Collections;
using UnityEngine;
using FinalDefense.Data;
using FinalDefense.Battle;

namespace FinalDefense.Tower
{
    public class TowerSkill : MonoBehaviour
    {
        private TowerSkillData[] skills;
        private float currentSP;
        private float maxSP = 50f;
        private float spRegenRate = 2f;
        private Tower tower;
        private bool skillActive;

        public float CurrentSP => currentSP;
        public float MaxSP => maxSP;
        public TowerSkillData[] Skills => skills;

        private void Awake()
        {
            tower = GetComponent<Tower>();
        }

        public void Initialize(TowerSkillData[] skillList)
        {
            skills = skillList;
            currentSP = 0;
        }

        private void Update()
        {
            if (tower != null)
            {
                var health = GetComponent<TowerHealth>();
                if (health != null && health.IsDowned) return;
            }

            currentSP = Mathf.Min(currentSP + spRegenRate * Time.deltaTime, maxSP);

            if (skills != null && skills.Length > 0 && currentSP >= skills[0].spCost && !skillActive)
            {
                TryActivateSkill(0);
            }
        }

        public bool TryActivateSkill(int index)
        {
            if (skills == null || index >= skills.Length) return false;
            var skill = skills[index];
            if (currentSP < skill.spCost) return false;

            currentSP -= skill.spCost;
            ExecuteSkill(skill);
            return true;
        }

        private void ExecuteSkill(TowerSkillData skill)
        {
            switch (skill.effectType)
            {
                case SkillEffectType.AreaDamage:
                    DealAreaDamage(skill);
                    break;
                case SkillEffectType.SingleDamage:
                    DealSingleDamage(skill);
                    break;
                case SkillEffectType.Heal:
                    HealSelf(skill);
                    break;
                case SkillEffectType.HealArea:
                    HealArea(skill);
                    break;
                case SkillEffectType.Shield:
                    ApplyShield(skill);
                    break;
                case SkillEffectType.Taunt:
                    ApplyTaunt(skill);
                    break;
                case SkillEffectType.Stun:
                    ApplyStun(skill);
                    break;
                case SkillEffectType.SlowField:
                    ApplySlowField(skill);
                    break;
                case SkillEffectType.WeakenField:
                    ApplyWeakenField(skill);
                    break;
                case SkillEffectType.CostRecovery:
                    RecoverCost(skill);
                    break;
                case SkillEffectType.PoisonField:
                    StartCoroutine(PoisonFieldCoroutine(skill));
                    break;
                case SkillEffectType.MultiStrike:
                    StartCoroutine(MultiStrikeCoroutine(skill));
                    break;
                case SkillEffectType.PierceShot:
                    PierceShot(skill);
                    break;
                case SkillEffectType.Summon:
                    SummonUnit(skill);
                    break;
                default:
                    DealAreaDamage(skill);
                    break;
            }

            Core.EventBus.SkillActivated(gameObject);
        }

        private void DealAreaDamage(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<Enemy.EnemyHealth>();
                if (health != null)
                {
                    int dmg = DamageCalculator.Calculate(skill.value, health.Defense);
                    health.TakeDamage(dmg);
                }
            }
        }

        private void DealSingleDamage(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            float minDist = float.MaxValue;
            Enemy.EnemyHealth target = null;
            foreach (var hit in hits)
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < minDist)
                {
                    var h = hit.GetComponent<Enemy.EnemyHealth>();
                    if (h != null && !h.IsDead)
                    {
                        minDist = dist;
                        target = h;
                    }
                }
            }
            if (target != null)
            {
                int dmg = DamageCalculator.Calculate(skill.value * 2, target.Defense);
                target.TakeDamage(dmg);
            }
        }

        private void HealSelf(TowerSkillData skill)
        {
            var health = GetComponent<TowerHealth>();
            if (health != null)
                health.Heal(skill.value);
        }

        private void HealArea(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Tower"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<TowerHealth>();
                if (health != null && !health.IsDowned)
                    health.Heal(skill.value);
            }
        }

        private void ApplyShield(TowerSkillData skill)
        {
            var health = GetComponent<TowerHealth>();
            if (health != null)
                health.Heal(Mathf.RoundToInt(health.MaxHP * 0.3f));
        }

        private void ApplyTaunt(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<Enemy.Enemy>();
                if (enemy != null)
                {
                    enemy.IsAttacking = true;
                    StartCoroutine(ReleaseEnemy(enemy, skill.duration));
                }
            }
        }

        private void ApplyStun(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<Enemy.Enemy>();
                if (enemy != null)
                {
                    enemy.ApplySlow(0f, skill.duration);
                }
            }
        }

        private void ApplySlowField(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<Enemy.Enemy>();
                if (enemy != null)
                    enemy.ApplySlow(0.5f, skill.duration);
            }
        }

        private void ApplyWeakenField(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<Enemy.EnemyHealth>();
                if (health != null)
                    health.TakeDamage(Mathf.RoundToInt(skill.value * 0.2f));
            }
        }

        private void RecoverCost(TowerSkillData skill)
        {
            var costSystem = Object.FindFirstObjectByType<DeployCostSystem>();
            if (costSystem != null)
                costSystem.AddCost(skill.value);
        }

        private IEnumerator PoisonFieldCoroutine(TowerSkillData skill)
        {
            skillActive = true;
            float elapsed = 0f;
            while (elapsed < skill.duration)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
                foreach (var hit in hits)
                {
                    var health = hit.GetComponent<Enemy.EnemyHealth>();
                    if (health != null)
                        health.TakeDamage(Mathf.RoundToInt(skill.value * 0.3f));
                    var enemy = hit.GetComponent<Enemy.Enemy>();
                    if (enemy != null)
                        enemy.ApplySlow(0.6f, 1f);
                }
                yield return new WaitForSeconds(1f);
                elapsed += 1f;
            }
            skillActive = false;
        }

        private IEnumerator MultiStrikeCoroutine(TowerSkillData skill)
        {
            skillActive = true;
            for (int i = 0; i < 5; i++)
            {
                DealSingleDamage(skill);
                yield return new WaitForSeconds(0.2f);
            }
            skillActive = false;
        }

        private void PierceShot(TowerSkillData skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<Enemy.EnemyHealth>();
                if (health != null)
                    health.TakeDamage(skill.value);
            }
        }

        private void SummonUnit(TowerSkillData skill)
        {
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
            Vector3 pos = transform.position + offset;

            var go = new GameObject("SummonedUnit");
            go.transform.position = pos;
            go.layer = LayerMask.NameToLayer("Tower");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.5f, 1f, 0.5f);
            sr.sortingOrder = 11;
            Texture2D tex = new Texture2D(24, 24);
            Color[] colors = new Color[24 * 24];
            for (int c = 0; c < colors.Length; c++) colors[c] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 24, 24), Vector2.one * 0.5f, 24);
            go.transform.localScale = Vector3.one * 0.5f;

            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
            var th = go.AddComponent<TowerHealth>();
            th.Initialize(new TowerData { maxHP = skill.value, redeployCooldown = 999f, redeployCost = 999 });

            Destroy(go, skill.duration);
        }

        private IEnumerator ReleaseEnemy(Enemy.Enemy enemy, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (enemy != null) enemy.IsAttacking = false;
        }
    }
}
