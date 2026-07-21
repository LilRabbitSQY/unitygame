using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
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

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
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

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                TryPlace(cellPos, cellCenter);
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
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
            sr.sprite = tower.icon;
            sr.color = new Color(0, 1, 0, 0.5f);
            sr.sortingOrder = 10;

            if (sr.sprite == null)
            {
                sr.sprite = CreatePlaceholderSprite();
                sr.color = new Color(tower.towerColor.r, tower.towerColor.g, tower.towerColor.b, 0.5f);
            }
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

            occupiedCells.Add(cellPos);
            CancelPlacement();
        }

        private GameObject CreateDefaultTower(Vector3 position)
        {
            var go = new GameObject("Tower_" + selectedTower.towerName);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePlaceholderSprite();
            sr.color = selectedTower.towerColor;
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * 0.7f;

            return go;
        }

        private Sprite CreatePlaceholderSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
