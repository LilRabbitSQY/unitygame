using System.Collections;
using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Tower
{
    [System.Serializable]
    public class SkillData
    {
        public string skillName;
        public int spCost = 10;
        public int damage = 50;
        public float cooldown = 5f;
        public float range = 3f;
        public enum SkillType { AreaDamage, Heal, SpeedBuff, Stun }
        public SkillType type = SkillType.AreaDamage;
    }

    public class TowerSkill : MonoBehaviour
    {
        [SerializeField] private SkillData[] skills;
        
        private float currentSP;
        private float maxSP = 50f;
        private float spRegenRate = 2f;
        private Tower tower;

        public float CurrentSP => currentSP;
        public float MaxSP => maxSP;
        public SkillData[] Skills => skills;

        private void Awake()
        {
            tower = GetComponent<Tower>();
        }

        public void Initialize(SkillData[] skillList)
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

        private void ExecuteSkill(SkillData skill)
        {
            switch (skill.type)
            {
                case SkillData.SkillType.AreaDamage:
                    var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
                    foreach (var hit in hits)
                    {
                        var health = hit.GetComponent<Enemy.EnemyHealth>();
                        if (health != null) health.TakeDamage(skill.damage);
                    }
                    break;

                case SkillData.SkillType.Heal:
                    var towerHealth = GetComponent<TowerHealth>();
                    if (towerHealth != null)
                    {
                        int healAmount = Mathf.RoundToInt(towerHealth.MaxHP * 0.3f);
                        towerHealth.Heal(healAmount);
                    }
                    break;

                case SkillData.SkillType.Stun:
                    var enemies = Physics2D.OverlapCircleAll(transform.position, skill.range, LayerMask.GetMask("Enemy"));
                    foreach (var e in enemies)
                    {
                        var enemy = e.GetComponent<Enemy.Enemy>();
                        if (enemy != null) StartCoroutine(StunEnemy(enemy, 2f));
                    }
                    break;
            }

            Core.EventBus.SkillActivated(gameObject);
        }

        private IEnumerator StunEnemy(Enemy.Enemy enemy, float duration)
        {
            if (enemy == null) yield break;
            enemy.IsAttacking = true;
            yield return new WaitForSeconds(duration);
            if (enemy != null) enemy.IsAttacking = false;
        }
    }
}
