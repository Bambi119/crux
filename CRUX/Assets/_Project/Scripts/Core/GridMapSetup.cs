using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
// HexFacet은 Crux.Grid에 정의됨

namespace Crux.Core
{
    /// <summary>초기 맵 배치 — 유닛, 엄폐물, 코어</summary>
    public class GridMapSetup : MonoBehaviour
    {
        [Header("데이터")]
        public TankDataSO playerTankData;
        public TankDataSO lightEnemyData;
        public TankDataSO heavyEnemyData;
        public AmmoDataSO playerAmmo;
        public AmmoDataSO enemyAmmo;

        [Header("스프라이트")]
        public Sprite playerHullSprite;
        public Sprite playerTurretSprite;
        public Sprite lightEnemySprite;
        public Sprite heavyEnemySprite;
        public Sprite coverSprite;
        public Sprite bioCoreSprite;
        public Sprite defenseTurretSprite;

        private GridManager grid;
        private GridTankUnit playerUnit;
        private List<GridTankUnit> enemyUnits = new();
        private List<GameObject> coverObjects = new();

        public GridTankUnit PlayerUnit => playerUnit;
        public List<GridTankUnit> EnemyUnits => enemyUnits;

        public void Setup(GridManager grid)
        {
            this.grid = grid;

            SpawnFloorTiles();
            SpawnPlayer();
            SpawnEnemies();
            SpawnCovers();
            SpawnBioCore();
        }

        private void SpawnFloorTiles()
        {
            var floorSprite = TankSpriteGenerator.CreateFloorTile();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var obj = new GameObject($"Floor_{x}_{y}");
                    obj.transform.position = grid.GridToWorld(new Vector2Int(x, y));
                    obj.transform.SetParent(transform);

                    var sr = obj.AddComponent<SpriteRenderer>();
                    sr.sprite = floorSprite;
                    sr.sortingOrder = -2;
                    // Hex 스프라이트는 반지름 0.5 기준 1 unit 폭으로 생성됨.
                    // 실제 hex 반지름 = CellSize 이므로 2배 스케일 필요.
                    obj.transform.localScale = Vector3.one * GameConstants.CellSize * 2f;
                }
            }
        }

        private void SpawnPlayer()
        {
            playerUnit = CreateUnit(new Vector2Int(4, 1), playerTankData, playerAmmo,
                                     PlayerSide.Player,
                                     playerHullSprite != null ? playerHullSprite : TankSpriteGenerator.CreatePlayerHull(),
                                     "로시난테",
                                     playerTurretSprite); // 포탑 스프라이트
        }

        private void SpawnEnemies()
        {
            enemyUnits.Add(CreateUnit(new Vector2Int(2, 7), lightEnemyData, enemyAmmo,
                                       PlayerSide.Enemy,
                                       lightEnemySprite != null ? lightEnemySprite : TankSpriteGenerator.CreateLightEnemy(),
                                       "경량 드론", spriteRotOffset: -90f));

            enemyUnits.Add(CreateUnit(new Vector2Int(6, 7), heavyEnemyData, enemyAmmo,
                                       PlayerSide.Enemy,
                                       heavyEnemySprite != null ? heavyEnemySprite : TankSpriteGenerator.CreateHeavyEnemy(),
                                       "중장갑 전차", spriteRotOffset: -90f));

            enemyUnits.Add(CreateUnit(new Vector2Int(4, 8), heavyEnemyData, enemyAmmo,
                                       PlayerSide.Enemy,
                                       heavyEnemySprite != null ? heavyEnemySprite : TankSpriteGenerator.CreateHeavyEnemy(),
                                       "코어 수호 전차", spriteRotOffset: -90f));
        }

        private void SpawnCovers()
        {
            // 엄폐물 배치 데이터: (위치, 이름, 크기, HP, 엄폐율, 방호면)
            // facets: 막아주는 변의 비트 플래그 (그 방향에서 오는 공격을 막음)
            var coverDefs = new (Vector2Int pos, string name, CoverSize size, float hp, float rate, HexFacet facets)[]
            {
                // 플레이어 진영 앞 (전방 3행 부근) — 북쪽에서 오는 공격 차단
                (new(3, 3), "잔해 더미",   CoverSize.Small,  40f,  0.4f,  HexFacet.N),
                (new(5, 3), "잔해 더미",   CoverSize.Small,  40f,  0.4f,  HexFacet.N),
                // 전장 중간 — 북쪽 + 대각 2면 (넓은 방호)
                (new(1, 5), "콘크리트 벽", CoverSize.Medium, 80f,  0.65f, HexFacet.N | HexFacet.NE | HexFacet.NW),
                (new(6, 5), "콘크리트 벽", CoverSize.Medium, 80f,  0.65f, HexFacet.N | HexFacet.NE | HexFacet.NW),
                // 적 진영 앞 (6행) — 남쪽에서 오는 공격(플레이어 쪽)을 차단, 3면
                (new(3, 6), "강화 장벽",   CoverSize.Large,  120f, 0.9f,  HexFacet.S | HexFacet.SE | HexFacet.SW),
                (new(5, 6), "강화 장벽",   CoverSize.Large,  120f, 0.9f,  HexFacet.S | HexFacet.SE | HexFacet.SW),
            };

            foreach (var def in coverDefs)
            {
                var obj = new GameObject($"Cover_{def.name}");
                obj.transform.position = grid.GridToWorld(def.pos);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = TankSpriteGenerator.CreateCoverTile(def.size, def.facets);
                sr.sortingOrder = 2;
                obj.transform.localScale = Vector3.one * GameConstants.CellSize * 2f;

                var coverObj = obj.AddComponent<GridCoverObject>();
                coverObj.Initialize(def.name, def.size, def.hp, def.rate, def.facets, sr.sprite);

                var cell = grid.GetCell(def.pos);
                if (cell != null)
                {
                    cell.Type = CellType.Cover;
                    cell.Cover = coverObj;
                }

                var capturedPos = def.pos;
                coverObj.OnDestroyed += (destroyed) =>
                {
                    var c = grid.GetCell(capturedPos);
                    if (c != null)
                    {
                        c.Type = CellType.Empty;
                        c.Cover = null;
                    }
                };

                coverObjects.Add(obj);
            }
        }

        private void SpawnBioCore()
        {
            var pos = new Vector2Int(4, 9);
            var obj = new GameObject("BioCore");
            obj.transform.position = grid.GridToWorld(pos);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = bioCoreSprite != null ? bioCoreSprite : TankSpriteGenerator.CreateBioCore();
            sr.sortingOrder = 3;
            obj.transform.localScale = Vector3.one * 0.8f;

            var cell = grid.GetCell(pos);
            if (cell != null) cell.Type = CellType.Impassable;
        }

        private GridTankUnit CreateUnit(Vector2Int pos, TankDataSO data, AmmoDataSO ammo,
                                         PlayerSide side, Sprite hullSprite, string name,
                                         Sprite turretSprite = null, float spriteRotOffset = 0f)
        {
            var obj = new GameObject(name);

            // 스프라이트 컨테이너 (스프라이트 기본 방향 보정용)
            // spriteRotOffset: 스프라이트를 → 방향으로 맞추기 위한 오프셋
            // 예: ↓ 스프라이트 → -90° 회전하면 → 방향
            var spriteContainer = new GameObject("SpriteContainer");
            spriteContainer.transform.SetParent(obj.transform);
            spriteContainer.transform.localPosition = Vector3.zero;
            spriteContainer.transform.localRotation = Quaternion.Euler(0, 0, spriteRotOffset);

            // 차체 스프라이트 (컨테이너 안에)
            var sr = spriteContainer.AddComponent<SpriteRenderer>();
            sr.sprite = hullSprite;
            sr.sortingOrder = 10;

            // 포탑 (있으면 차체 위에 올림)
            if (turretSprite != null)
            {
                var turretObj = new GameObject("Turret");
                turretObj.transform.SetParent(obj.transform);
                turretObj.transform.localPosition = Vector3.zero;
                turretObj.transform.localRotation = Quaternion.Euler(0, 0, spriteRotOffset);
                var turretSr = turretObj.AddComponent<SpriteRenderer>();
                turretSr.sprite = turretSprite;
                turretSr.sortingOrder = 11;
            }

            var col = obj.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.8f;

            var sideId = obj.AddComponent<SideIdentifier>();
            sideId.SetSide(side);

            var unit = obj.AddComponent<GridTankUnit>();
            unit.Initialize(grid, pos, data, ammo, side);

            // 모듈 상태 오버레이
            var overlay = obj.AddComponent<UnitStatusOverlay>();
            overlay.Initialize(unit);

            // 화염 오버레이
            var fireOverlay = obj.AddComponent<FireOverlay>();
            fireOverlay.Initialize(unit);

            return unit;
        }
    }
}
