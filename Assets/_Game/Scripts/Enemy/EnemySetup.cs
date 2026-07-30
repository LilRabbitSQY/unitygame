using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Enemy
{
    public class EnemySetup : MonoBehaviour
    {
        public void ApplyVisuals(EnemyData data)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;

            sr.color = data.enemyColor;

            if (data.moveType == MoveType.Air)
            {
                sr.color = new Color(data.enemyColor.r, data.enemyColor.g, data.enemyColor.b, 0.8f);
            }

            if (data.enemyType == EnemyType.Stealth)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.3f);
            }

            if (data.enemyType == EnemyType.Heavy || data.enemyType == EnemyType.BossPhased || data.enemyType == EnemyType.BossSummoner)
            {
                transform.localScale = Vector3.one * 1.2f;
            }
            else if (data.enemyType == EnemyType.Swarm)
            {
                transform.localScale = Vector3.one * 0.5f;
            }
            else if (data.enemyType == EnemyType.Rusher)
            {
                transform.localScale = new Vector3(0.8f, 0.6f, 1f);
            }

            gameObject.name = $"Enemy_{data.enemyName}_{data.enemyType}";
        }
    }
}
