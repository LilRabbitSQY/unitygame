using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.DDL
{
    [System.Serializable]
    public class DDLInstance
    {
        public DDLData data;
        public int remainingDays;
        public bool completed;
    }

    public class DDLManager : MonoBehaviour
    {
        [SerializeField] private DDLData[] allDDLs;
        
        private List<DDLInstance> activeDDLs = new List<DDLInstance>();
        private float waveMultiplier = 1f;

        public List<DDLInstance> ActiveDDLs => activeDDLs;
        public float WaveMultiplier => waveMultiplier;

        public static DDLManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            GenerateDailyDDLs();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void GenerateDailyDDLs()
        {
            if (allDDLs == null || allDDLs.Length == 0) return;

            var gm = GameManager.Instance;
            int day = gm != null ? gm.CurrentDay : 1;
            int newDDLCount = Mathf.Min(1 + day / 3, 3);

            for (int i = 0; i < newDDLCount && i < allDDLs.Length; i++)
            {
                int idx = (day + i) % allDDLs.Length;
                bool alreadyActive = activeDDLs.Exists(d => d.data == allDDLs[idx] && !d.completed);
                if (!alreadyActive)
                {
                    activeDDLs.Add(new DDLInstance
                    {
                        data = allDDLs[idx],
                        remainingDays = allDDLs[idx].deadlineDays,
                        completed = false
                    });
                }
            }

            RecalculateMultiplier();
        }

        public void OnBattleWon()
        {
            for (int i = activeDDLs.Count - 1; i >= 0; i--)
            {
                if (!activeDDLs[i].completed)
                {
                    activeDDLs[i].completed = true;
                    break;
                }
            }
        }

        public void AdvanceDay()
        {
            for (int i = activeDDLs.Count - 1; i >= 0; i--)
            {
                if (activeDDLs[i].completed) continue;

                activeDDLs[i].remainingDays--;
                if (activeDDLs[i].remainingDays <= 0)
                {
                    var gm = GameManager.Instance;
                    if (gm != null)
                        gm.TakeGPADamage(activeDDLs[i].data.gpaPenalty);
                    activeDDLs.RemoveAt(i);
                    EventBus.DDLExpired();
                }
            }

            RecalculateMultiplier();
        }

        private void RecalculateMultiplier()
        {
            waveMultiplier = 1f;
            int overdueCount = 0;
            foreach (var ddl in activeDDLs)
            {
                if (!ddl.completed && ddl.remainingDays <= 1)
                    overdueCount++;
            }
            waveMultiplier = 1f + overdueCount * 0.5f;
        }
    }
}
