using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using FinalDefense.Data;
using FinalDefense.Tower;

namespace FinalDefense.UI
{
    public class TowerButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private TowerData towerData;
        [SerializeField] private TowerPlacement towerPlacement;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image iconImage;

        private Button button;
        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private bool isHovering;

        private void Start()
        {
            button = GetComponent<Button>();
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();

            if (button != null)
                button.onClick.AddListener(OnClicked);

            if (towerData != null)
            {
                if (nameText != null) nameText.text = towerData.towerName;
                if (costText != null) costText.text = $"${towerData.cost}";
                if (iconImage != null && towerData.icon != null)
                    iconImage.sprite = towerData.icon;
                else if (iconImage != null)
                    iconImage.color = towerData.towerColor;
            }
        }

        private void Update()
        {
            if (towerData == null || rectTransform == null) return;
            if (!TryGetPointerPosition(out Vector2 pointerPosition)) return;

            bool hoveringNow = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, pointerPosition, GetUICamera());
            if (hoveringNow)
            {
                TowerTooltipUI.Show(towerData, pointerPosition);
            }
            else if (isHovering)
            {
                TowerTooltipUI.Hide();
            }

            isHovering = hoveringNow;
        }

        private void OnClicked()
        {
            TowerTooltipUI.Hide();
            isHovering = false;

            if (towerPlacement != null && towerData != null)
            {
                towerPlacement.SelectTower(towerData);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (towerData == null) return;
            isHovering = true;
            TowerTooltipUI.Show(towerData, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            TowerTooltipUI.Move(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            TowerTooltipUI.Hide();
        }

        private void OnDisable()
        {
            isHovering = false;
            TowerTooltipUI.Hide();
        }

        private bool TryGetPointerPosition(out Vector2 pointerPosition)
        {
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            pointerPosition = default;
            return false;
        }

        private Camera GetUICamera()
        {
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return parentCanvas.worldCamera;
        }
    }
}
