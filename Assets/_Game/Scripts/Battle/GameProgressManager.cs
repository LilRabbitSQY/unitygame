using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.NPC;

namespace FinalDefense.Battle
{
    public class GameProgressManager : MonoBehaviour
    {
        public static GameProgressManager Instance { get; private set; }

        private NPCId[] semesterOpponents = new NPCId[]
        {
            NPCId.Roommate,         // 第1局 大一上
            NPCId.Crush,            // 第2局 大一下
            NPCId.ClubSenior,       // 第3局 大二上
            NPCId.Professor,        // 第4局 大二下
            NPCId.Roommate,         // 第5局 大三上
            NPCId.Crush,            // 第6局 大三下
            NPCId.ClubSenior,       // 第7局 大四上
            NPCId.Dean              // 第8局 大四下 Boss关
        };

        public NPCId CurrentOpponent
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null) return NPCId.Roommate;
                int idx = Mathf.Clamp(gm.CurrentSemester - 1, 0, semesterOpponents.Length - 1);
                return semesterOpponents[idx];
            }
        }

        public bool IsBossStage
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.CurrentSemester >= 8;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public string GetSemesterName()
        {
            var gm = GameManager.Instance;
            if (gm == null) return "大一上";
            return gm.CurrentGrade switch
            {
                1 => gm.CurrentSemester <= 1 ? "大一上" : "大一下",
                2 => gm.CurrentSemester <= 3 ? "大二上" : "大二下",
                3 => gm.CurrentSemester <= 5 ? "大三上" : "大三下",
                4 => gm.CurrentSemester <= 7 ? "大四上" : "大四下",
                _ => "大学"
            };
        }

        public string GetTimeOfDay(int actionIndex)
        {
            return actionIndex switch
            {
                0 => "早晨",
                1 => "中午",
                2 => "晚上",
                _ => "深夜"
            };
        }
    }
}
