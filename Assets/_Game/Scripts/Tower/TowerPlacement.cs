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
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap highGroundTilemap;
        [SerializeField] private Camera mainCamera;

        private TowerData selectedTower;
        private GameObject previewInstance;
        private HashSet<Vector3Int> occupiedGroundCells = new();
        private HashSet<Vector3Int> occupiedHighCells = new();
        private bool isPlacing;
        private bool placedThisFrame;

        public bool IsPlacing => isPlacing;

        private Tilemap placeableTilemap => selectedTower != null && selectedTower.deployPosition == DeployPosition.HighGround
            ? (highGroundTilemap != null ? highGroundTilemap : groundTilemap)
            : groundTilemap;

        private void Update()
        {
            if (!isPlacing || selectedTower == null) return;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            var tilemap = placeableTilemap;
            if (tilemap == null) return;

            if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(pointerScreenPosition);
            mouseWorld.z = 0;
            Vector3Int cellPos = tilemap.WorldToCell(mouseWorld);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPos);

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
            var tilemap = placeableTilemap;
            if (!tilemap.HasTile(cellPos)) return false;

            if (selectedTower.deployPosition == DeployPosition.HighGround)
                return !occupiedHighCells.Contains(cellPos);
            else if (selectedTower.deployPosition == DeployPosition.Both)
                return !occupiedGroundCells.Contains(cellPos) && !occupiedHighCells.Contains(cellPos);
            else
                return !occupiedGroundCells.Contains(cellPos);
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

            if (selectedTower.deployPosition == DeployPosition.HighGround)
                occupiedHighCells.Add(cellPos);
            else
                occupiedGroundCells.Add(cellPos);

            var analytics = BattleAnalytics.Instance;
            if (analytics != null)
                analytics.RecordTowerPlaced(selectedTower);

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
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++)
            {
                float dist = Vector2.Distance(new Vector2(i % 32, i / 32), new Vector2(16, 16));
                colors[i] = dist < 14 ? Color.white : Color.clear;
            }
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        }

        private void ApplyTowerVisuals(GameObject towerGo, TowerData towerData)
        {
            var sr = towerGo.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = towerData.towerColor;
                sr.sortingOrder = 12;
                if (towerData.icon != null) sr.sprite = towerData.icon;
            }
            EnsureTowerLabel(towerGo, towerData, 13, 1f);
        }

        private void EnsureTowerLabel(GameObject towerGo, TowerData towerData, int sortingOrder, float alpha)
        {
            string label = GetTowerShortLabel(towerData);
            if (string.IsNullOrEmpty(label)) return;

            Transform existing = towerGo.transform.Find("Label");
            if (existing != null) Object.Destroy(existing.gameObject);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(towerGo.transform);
            labelGo.transform.localPosition = Vector3.zero;

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.characterSize = 0.15f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 24;
            tm.color = new Color(1, 1, 1, alpha);
            tm.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
        }

        private string GetTowerShortLabel(TowerData towerData)
        {
            return towerData.towerType switch
            {
                TowerType.Heavy => "重",
                TowerType.Vanguard => "先",
                TowerType.Striker => "突",
                TowerType.Caster => "术",
                TowerType.Sniper => "狙",
                TowerType.Trapper => "陷",
                TowerType.Healer => "医",
                TowerType.Support => "辅",
                TowerType.Shifter => "换",
                TowerType.Summoner => "召",
                _ => towerData.towerName.Length > 0 ? towerData.towerName[..1] : "?"
            };
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            return false;
        }

        private bool WasPrimaryClickPressed()
        {
            if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
            if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            return false;
        }

        private bool WasCancelPressed()
        {
            if (Mouse.current != null) return Mouse.current.rightButton.wasPressedThisFrame;
            if (Keyboard.current != null) return Keyboard.current.escapeKey.wasPressedThisFrame;
            return false;
        }
    }
}
