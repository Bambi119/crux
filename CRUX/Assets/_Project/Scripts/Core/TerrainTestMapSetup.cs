using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;

namespace Crux.Core
{
    /// <summary>
    /// P1 지형 시스템 테스트용 맵 배치 — 12×12, 지형 다양, 셀 틴트 시각화.
    /// 기획 레퍼런스: docs/12_enemy_ai.md §4 지형 시스템
    /// </summary>
    /// <remarks>
    /// GridMapSetup을 상속해 SpawnFloorTiles/SpawnPlayer/SpawnEnemies/SpawnCovers/SpawnBioCore를
    /// 지형 레이아웃에 맞게 오버라이드. GridManager의 width/height는 12×12로 설정되어 있어야 함.
    /// </remarks>
    public class TerrainTestMapSetup : GridMapSetup
    {
        // 지형 레이아웃 — 행(y) 0부터 시작, 각 문자가 한 셀
        // 범례:
        //   . = Open (개활지)
        //   R = Road (도로)
        //   M = Mud (진창)
        //   W = Woods (수풀)
        //   X = Rubble (파편)
        //   C = Crater (탄흔)
        //   H = Hill (언덕 +3)
        //   B = Building (통과 불가, LOS 차단)
        //   E = ElevatedBuilding (고지 건물, 보병/드론만)
        //   ~ = Water (지상 불가)
        //
        // 12×12 배치 (y=0 하단 플레이어, y=11 상단 적진)
        private static readonly string[] LayoutRows = new[]
        {
            "..RR........", // y=0  플레이어 진영 (도로 2셀)
            "..RR........", // y=1
            "....W.W.....", // y=2  수풀 경엄폐
            "...BB...XX..", // y=3  건물 + 파편
            "..BB.....X..", // y=4
            "....MM..H...", // y=5  진창 + 언덕
            "....MM..H...", // y=6
            "..W...~~....", // y=7  물 웅덩이
            "...C.~~..C..", // y=8  탄흔 + 물
            "....XX.BB...", // y=9  파편 + 적진 건물
            ".E.....BB.E.", // y=10 고지 건물 2채 (보병 매복 지점)
            "............", // y=11 적진 개활지
        };

        // 플레이어 스폰 (12×12 중앙 하단)
        private static readonly Vector2Int PlayerSpawn = new Vector2Int(5, 0);

        // 적 스폰 위치들
        private static readonly Vector2Int[] EnemySpawns = new[]
        {
            new Vector2Int(2, 11),
            new Vector2Int(5, 11),
            new Vector2Int(9, 11),
            new Vector2Int(3, 9),
            new Vector2Int(8, 9),
        };

        public override void Setup(GridManager grid)
        {
            this.grid = grid;

            ApplyTerrainLayout();
            SpawnFloorTiles();
            SpawnPlayer();
            SpawnEnemies();
            SpawnCovers();
            SpawnBioCore();
        }

        /// <summary>LayoutRows → GridCell.Terrain 적용 — SpawnFloorTiles 전에 호출</summary>
        /// <remarks>LayoutRows[0] = 그리드 y=0 = 화면 하단 = 플레이어 진영. 직접 매핑.</remarks>
        private void ApplyTerrainLayout()
        {
            int rows = LayoutRows.Length;
            int cols = LayoutRows[0].Length;
            for (int y = 0; y < rows && y < grid.Height; y++)
            {
                string row = LayoutRows[y];
                for (int x = 0; x < cols && x < grid.Width; x++)
                {
                    char c = row[x];
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell == null) continue;
                    cell.Terrain = CharToTerrain(c);

                    // 통과 불가 지형은 CellType.Impassable도 함께 설정 (기존 코드 호환)
                    if (cell.Terrain == TerrainType.Building || cell.Terrain == TerrainType.Water)
                    {
                        cell.Type = CellType.Impassable;
                    }
                }
            }
        }

        private static TerrainType CharToTerrain(char c) => c switch
        {
            '.' => TerrainType.Open,
            'R' => TerrainType.Road,
            'M' => TerrainType.Mud,
            'W' => TerrainType.Woods,
            'X' => TerrainType.Rubble,
            'C' => TerrainType.Crater,
            'H' => TerrainType.Hill,
            'B' => TerrainType.Building,
            'E' => TerrainType.ElevatedBuilding,
            '~' => TerrainType.Water,
            _ => TerrainType.Open
        };

        /// <summary>플로어 타일 생성 + 지형별 셀 틴트 + 다크 변형 적용 (TD-06)</summary>
        /// <remarks>
        /// multiply 틴트만으로는 어두운 지형(Crater·Water·Building 등) 명도 표현이 약함.
        /// TankSpriteGenerator.FloorDarkenForTerrain 으로 dark1~dark3 단계를 선택해
        /// 스프라이트 픽셀 자체를 감쇠한 뒤 TintColor 를 곱한다.
        /// </remarks>
        protected override void SpawnFloorTiles()
        {
            // 지형 타입별 다크 스프라이트 캐시 (같은 darken 값은 재사용)
            var spriteCache = new System.Collections.Generic.Dictionary<float, Sprite>();

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cellPos = new Vector2Int(x, y);
                    var cell = grid.GetCell(cellPos);
                    var terrain = cell != null ? cell.Terrain : TerrainType.Open;

                    float darken = TankSpriteGenerator.FloorDarkenForTerrain(terrain);
                    if (!spriteCache.TryGetValue(darken, out var sprite))
                    {
                        sprite = TankSpriteGenerator.CreateFloorTile(darken);
                        spriteCache[darken] = sprite;
                    }

                    var obj = new GameObject($"Floor_{x}_{y}_{TerrainData.Label(terrain)}");
                    obj.transform.position = grid.GridToWorld(cellPos);
                    obj.transform.SetParent(transform);

                    var sr = obj.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = -2;
                    sr.color = TerrainData.TintColor(terrain);
                    obj.transform.localScale = Vector3.one * GameConstants.CellSize * 2f;
                }
            }
        }

        protected override void SpawnPlayer()
        {
            playerUnit = CreateUnit(PlayerSpawn, playerTankData, playerAmmo,
                                     PlayerSide.Player,
                                     playerHullSprite != null ? playerHullSprite : TankSpriteGenerator.CreatePlayerHull(),
                                     "로시난테",
                                     playerTurretSprite);
        }

        protected override void SpawnEnemies()
        {
            for (int i = 0; i < EnemySpawns.Length; i++)
            {
                var pos = EnemySpawns[i];
                // 스폰 위치가 지상 통과 가능한지 확인 (건물/물 회피)
                var cell = grid.GetCell(pos);
                if (cell == null || !TerrainData.GroundPassable(cell.Terrain)) continue;

                bool heavy = i % 2 == 0;
                var data = heavy ? heavyEnemyData : lightEnemyData;
                var sprite = heavy
                    ? (heavyEnemySprite != null ? heavyEnemySprite : TankSpriteGenerator.CreateHeavyEnemy())
                    : (lightEnemySprite != null ? lightEnemySprite : TankSpriteGenerator.CreateLightEnemy());
                string name = heavy ? $"중장갑 전차 {i + 1}" : $"경량 드론 {i + 1}";

                enemyUnits.Add(CreateUnit(pos, data, enemyAmmo,
                                           PlayerSide.Enemy, sprite, name, spriteRotOffset: -90f));
            }
        }

        /// <summary>엄폐물은 지형 테스트 맵에서는 최소화 — 지형 자체가 대부분의 엄폐를 담당</summary>
        protected override void SpawnCovers()
        {
            // 중앙부 전선에 소수의 엄폐물 배치 (지형 엄폐와 겹치지 않는 위치)
            var coverDefs = new (Vector2Int pos, string name, CoverSize size, float hp, float rate, HexFacet facets)[]
            {
                (new(1, 5),  "콘크리트 벽", CoverSize.Medium, 80f, 0.65f, HexFacet.N | HexFacet.NE),
                (new(10, 5), "콘크리트 벽", CoverSize.Medium, 80f, 0.65f, HexFacet.N | HexFacet.NW),
                (new(0, 7),  "잔해 더미",   CoverSize.Small,  40f, 0.4f,  HexFacet.N),
                (new(11, 7), "잔해 더미",   CoverSize.Small,  40f, 0.4f,  HexFacet.N),
            };

            foreach (var def in coverDefs)
            {
                var cell = grid.GetCell(def.pos);
                if (cell == null || cell.Type == CellType.Impassable) continue;
                if (!TerrainData.GroundPassable(cell.Terrain)) continue;

                var obj = new GameObject($"Cover_{def.name}");
                obj.transform.position = grid.GridToWorld(def.pos);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = TankSpriteGenerator.CreateCoverTile(def.size, def.facets);
                sr.sortingOrder = 2;
                obj.transform.localScale = Vector3.one * GameConstants.CellSize * 2f;

                var coverObj = obj.AddComponent<GridCoverObject>();
                coverObj.Initialize(def.name, def.size, def.hp, def.rate, def.facets, sr.sprite);

                cell.Type = CellType.Cover;
                cell.Cover = coverObj;

                var capturedPos = def.pos;
                coverObj.OnDestroyed += (destroyed) =>
                {
                    var c = grid.GetCell(capturedPos);
                    if (c != null)
                    {
                        c.Type = CellType.Empty;
                        c.Cover = null;
                        // HE 피격 흔적 — 탄흔으로 지형 변경 (동적 파괴, 기획 §4.5)
                        c.Terrain = TerrainType.Crater;
                    }
                };

                coverObjects.Add(obj);
            }
        }

        /// <summary>바이오 코어 — 테스트 맵에서는 맵 상단 중앙에 배치</summary>
        protected override void SpawnBioCore()
        {
            var pos = new Vector2Int(5, grid.Height - 1);
            var obj = new GameObject("BioCore");
            obj.transform.position = grid.GridToWorld(pos);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = bioCoreSprite != null ? bioCoreSprite : TankSpriteGenerator.CreateBioCore();
            sr.sortingOrder = 3;
            obj.transform.localScale = Vector3.one * 0.8f;

            var cell = grid.GetCell(pos);
            if (cell != null) cell.Type = CellType.Impassable;
        }
    }
}
