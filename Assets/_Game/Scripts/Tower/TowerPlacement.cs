using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using FinalDefense.Data;
using FinalDefense.Battle;

namespace FinalDefense.Tower
{
    public class TowerPlacement : MonoBehaviour
    {
        [SerializeField] private Tilemap placeableTilemap;
        [SerializeField] private Camera mainCamera;

        private TowerData selectedTower;
        private GameObject previewInstance;
        private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        private bool isPlacing;
        private bool placedThisFrame;

        public bool IsPlacing => isPlacing;

        private void Update()
        {
            if (!isPlacing || selectedTower == null) return;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || placeableTilemap == null) return;

            if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(pointerScreenPosition);
            mouseWorld.z = 0;
            Vector3Int cellPos = placeableTilemap.WorldToCell(mouseWorld);
            Vector3 cellCenter = placeableTilemap.GetCellCenterWorld(cellPos);

            if (previewInstance != null)
            {
                previewInstance.transform.position = cellCenter;
                var sr = previewInstance.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    bool valid = IsValidPlacement(cellPos);
                    sr.color = valid ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
                }
            }

            if (placedThisFrame)
            {
                placedThisFrame = false;
                return;
            }

            if (WasPrimaryClickPressed() && !IsPointerOverUI())
            {
                TryPlace(cellPos, cellCenter);
            }

            if (WasCancelPressed())
            {
                CancelPlacement();
            }
        }

        public void SelectTower(TowerData tower)
        {
            CancelPlacement();
            selectedTower = tower;
            isPlacing = true;
            placedThisFrame = true;

            previewInstance = new GameObject("TowerPreview");
            var sr = previewInstance.AddComponent<SpriteRenderer>();
            sr.sprite = tower.icon != null ? tower.icon : CreateTowerSprite();
            sr.color = new Color(0, 1, 0, 0.5f);
            sr.sortingOrder = 10;

            if (tower.icon == null)
                sr.color = new Color(tower.towerColor.r, tower.towerColor.g, tower.towerColor.b, 0.5f);

            EnsureTowerLabel(previewInstance, tower, 11, 0.45f);
            previewInstance.transform.localScale = Vector3.one * 0.7f;
        }

        public void CancelPlacement()
        {
            isPlacing = false;
            selectedTower = null;
            if (previewInstance != null)
            {
                Object.Destroy(previewInstance);
                previewInstance = null;
            }
        }

        private bool IsValidPlacement(Vector3Int cellPos)
        {
            return placeableTilemap.HasTile(cellPos) && !occupiedCells.Contains(cellPos);
        }

        private void TryPlace(Vector3Int cellPos, Vector3 worldPos)
        {
            if (!IsValidPlacement(cellPos)) return;

            var costSystem = FindFirstObjectByType<DeployCostSystem>();
            if (costSystem != null && !costSystem.TrySpend(selectedTower.deployCost)) return;

            GameObject towerGo;
            if (selectedTower.prefab != null)
            {
                towerGo = Instantiate(selectedTower.prefab, worldPos, Quaternion.identity);
            }
            else
            {
                towerGo = CreateDefaultTower(worldPos);
            }

            towerGo.layer = LayerMask.NameToLayer("Tower");
            var collider = towerGo.GetComponent<BoxCollider2D>();
            if (collider == null) collider = towerGo.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.8f;

            var tower = towerGo.GetComponent<Tower>();
            if (tower == null) tower = towerGo.AddComponent<Tower>();

            var health = towerGo.GetComponent<TowerHealth>();
            if (health == null) health = towerGo.AddComponent<TowerHealth>();

            tower.Initialize(selectedTower);
            ApplyTowerVisuals(towerGo, selectedTower);

            occupiedCells.Add(cellPos);
            CancelPlacement();
        }

        private GameObject CreateDefaultTower(Vector3 position)
        {
            var go = new GameObject("Tower_" + selectedTower.towerName);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateTowerSprite();
            sr.color = selectedTower.towerColor;
            sr.sortingOrder = 12;
            go.transform.localScale = Vector3.one * 0.7f;

            return go;
        }

        private Sprite CreateTowerSprite()
        {
            const int size = 48;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;
            float innerRadius = size * 0.32f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius)
                        colors[y * size + x] = Color.clear;
                    else if (dist > innerRadius)
                        colors[y * size + x] = new Color(1f, 1f, 1f, 0.95f);
                    else
                        colors[y * size + x] = Color.white;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private void ApplyTowerVisuals(GameObject towerGo, TowerData towerData)
        {
            if (towerGo == null || towerData == null) return;

            var sr = towerGo.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = towerGo.AddComponent<SpriteRenderer>();

            if (sr.sprite == null)
                sr.sprite = towerData.icon != null ? towerData.icon : CreateTowerSprite();

            sr.color = towerData.towerColor;
            sr.sortingOrder = 12;

            EnsureTowerLabel(towerGo, towerData, 13, 0.5f);
        }

        private void EnsureTowerLabel(GameObject towerGo, TowerData towerData, int sortingOrder, float alpha)
        {
            if (towerGo == null || towerData == null) return;

            var labelTransform = towerGo.transform.Find("TowerTypeLabel");
            TextMesh label;
            if (labelTransform == null)
            {
                var labelGo = new GameObject("TowerTypeLabel");
                labelGo.transform.SetParent(towerGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, -0.03f, -0.01f);
                label = labelGo.AddComponent<TextMesh>();
            }
            else
            {
                label = labelTransform.GetComponent<TextMesh>();
                if (label == null)
                    label = labelTransform.gameObject.AddComponent<TextMesh>();
            }

            label.text = GetTowerShortLabel(towerData);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 22;
            label.characterSize = 0.08f;
            label.color = new Color(1f, 1f, 1f, alpha);

            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = sortingOrder;
        }

        private string GetTowerShortLabel(TowerData towerData)
        {
            if (towerData == null || string.IsNullOrEmpty(towerData.towerName))
                return "T";

            string name = towerData.towerName.ToLower();
            if (name.Contains("notebook") || name.Contains("笔记"))
                return "N";
            if (name.Contains("calculator") || name.Contains("计算"))
                return "C";
            if (name.Contains("coffee") || name.Contains("咖啡"))
                return "K";

            return "T";
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool WasPrimaryClickPressed()
        {
            return Mouse.current?.leftButton.wasPressedThisFrame == true ||
                   Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true;
        }

        private bool WasCancelPressed()
        {
            return Mouse.current?.rightButton.wasPressedThisFrame == true ||
                   Keyboard.current?.escapeKey.wasPressedThisFrame == true;
        }
    }
}
