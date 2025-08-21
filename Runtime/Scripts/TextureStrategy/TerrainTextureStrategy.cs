using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;

namespace SCLib_SurfaceImpactFeedback.TextureStrategy
{
    /// <summary>
    /// Terrainオブジェクト用のテクスチャ取得ストラテジー
    /// 
    /// Terrainの複数テクスチャレイヤーから、指定位置のテクスチャ情報を取得します。
    /// アルファマップを使用してブレンドされたテクスチャの重みを計算し、
    /// 重みが0より大きいテクスチャのみを返します。
    /// 
    /// UniTaskを使用した自動リソース管理により、
    /// Terrainオブジェクトが破棄された際に自動的にキャッシュからクリーンアップされます。
    /// </summary>
    public class TerrainTextureStrategy : ITextureStrategy
    {
        /// <summary>
        /// 対象のTerrainオブジェクト
        /// </summary>
        private readonly Terrain _terrain;
        
        /// <summary>
        /// テクスチャ情報の作業用リスト（ガベージコレクション削減のため再利用）
        /// </summary>
        private readonly List<TextureAlpha> _workTextures;
        
        /// <summary>
        /// Terrainストラテジーキャッシュへの参照（自動クリーンアップ用）
        /// </summary>
        private readonly Dictionary<Terrain, TerrainTextureStrategy> _terrainStrategyCache;
        
        /// <summary>
        /// TerrainTextureStrategyの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="terrainStrategyCache">Terrainストラテジーキャッシュ</param>
        /// <param name="terrain">対象のTerrain</param>
        public TerrainTextureStrategy(Dictionary<Terrain, TerrainTextureStrategy> terrainStrategyCache, Terrain terrain)
        {
            _terrain = terrain;
            _workTextures = new List<TextureAlpha>();
            _terrainStrategyCache = terrainStrategyCache;

            // Terrainオブジェクトが破棄されたら自動でキャッシュクリーンアップ
            DestroyAsync().Forget();
        }

        /// <summary>
        /// 指定位置のTerrainテクスチャ情報を取得します
        /// 
        /// Terrainのアルファマップから各テクスチャレイヤーの重みを計算し、
        /// 最も重みの高いテクスチャのみを返します（エフェクト重複防止）。
        /// </summary>
        /// <param name="hitPoint">ヒットポイントの世界座標</param>
        /// <param name="triangleIndex">使用されません（Terrain用）</param>
        /// <returns>最大重みを持つテクスチャのリスト（最大1個）</returns>
        public List<TextureAlpha> GetTextures(Vector3 hitPoint, int triangleIndex = 0)
        {
            // 世界座標をTerrain相対座標に変換
            Vector3 terrainPosition = hitPoint - _terrain.transform.position;
            
            // Terrain相対座標を正規化座標（0-1）に変換
            Vector3 splatMapPosition = new Vector3(
                terrainPosition.x / _terrain.terrainData.size.x,
                0,
                terrainPosition.z / _terrain.terrainData.size.z
            );

            // 正規化座標をアルファマップのピクセル座標に変換
            int x = Mathf.FloorToInt(splatMapPosition.x * _terrain.terrainData.alphamapWidth);
            int z = Mathf.FloorToInt(splatMapPosition.z * _terrain.terrainData.alphamapHeight);

            // 該当ピクセルのアルファマップを取得（1x1ピクセル）
            float[,,] alphaMap = _terrain.terrainData.GetAlphamaps(x, z, 1, 1);

            // 結果リストをクリア（リスト再利用でGC削減）
            _workTextures.Clear();
            
            // 最大重みのテクスチャを見つける（エフェクト重複を防ぐため）
            float maxWeight = 0f;
            int maxWeightIndex = -1;
            
            for (int i = 0; i < alphaMap.Length; i++)
            {
                float weight = alphaMap[0, 0, i];
                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    maxWeightIndex = i;
                }
            }
            
            // 最大重みのテクスチャのみをリストに追加（重複エフェクト防止）
            if (maxWeightIndex >= 0 && maxWeight > 0)
            {
                _workTextures.Add(new TextureAlpha(maxWeight, _terrain.terrainData.terrainLayers[maxWeightIndex].diffuseTexture));
            }

            return _workTextures;
        }

        /// <summary>
        /// Terrainオブジェクトの破棄を監視し、自動的にキャッシュをクリーンアップします
        /// </summary>
        private async UniTaskVoid DestroyAsync()
        {
            // Terrainオブジェクトの破棄を待機
            await _terrain.gameObject.OnDestroyAsync();
            
            // キャッシュから自身を削除してメモリリークを防止
            _terrainStrategyCache.Remove(_terrain);
        }
    }
} 