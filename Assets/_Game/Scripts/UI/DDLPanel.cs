using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.DDL;

namespace FinalDefense.UI
{
    public class DDLPanel : MonoBehaviour
    {
        [SerializeField] private Transform ddlListContainer;
        [SerializeField] private TextMeshProUGUI ddlTitleText;

        private void Start()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (DDLManager.Instance == null) return;

            if (ddlTitleText != null)
                ddlTitleText.text = $"当前DDL (难度倍率: x{DDLManager.Instance.WaveMultiplier:F1})";

            if (ddlListContainer == null) return;
            foreach (Transform child in ddlListContainer)
                Destroy(child.gameObject);

            foreach (var ddl in DDLManager.Instance.ActiveDDLs)
            {
                if (ddl.completed) continue;
                var go = new GameObject("DDLItem");
                go.transform.SetParent(ddlListContainer, false);
                var rt = go.AddComponent<RectTransform>();
                var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
                le.minHeight = 30;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 14;
                tmp.color = ddl.remainingDays <= 1 ? Color.red : Color.white;
                tmp.text = $"{ddl.data.ddlName} - 剩余{ddl.remainingDays}天";
            }
        }
    }
}
