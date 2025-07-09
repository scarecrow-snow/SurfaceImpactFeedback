using UnityEngine;
using System.Collections.Generic;

namespace SCLib_SurfaceImpactFeedback.TextureStrategy
{
    /// <summary>
    /// テクスチャ取得のストラテジーパターンインターフェース
    /// 
    /// 異なるオブジェクトタイプ（Terrain、Renderer）に対して、
    /// 統一されたテクスチャ取得方法を提供します。
    /// 
    /// 実装クラス：
    /// - TerrainTextureStrategy: Terrainオブジェクト用
    /// - RendererTextureStrategy: MeshRenderer/SkinnedMeshRenderer用
    /// </summary>
    public interface ITextureStrategy
    {
        /// <summary>
        /// 指定された位置のテクスチャ情報を取得する
        /// </summary>
        /// <param name="hitPoint">ヒットポイントの世界座標</param>
        /// <param name="triangleIndex">メッシュの三角形インデックス（オプション）</param>
        /// <returns>テクスチャとアルファ値のリスト</returns>
        List<TextureAlpha> GetTextures(Vector3 hitPoint, int triangleIndex = 0);
    }
} 