using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;

namespace SCLib_SurfaceImpactFeedback.TextureStrategy
{
    /// <summary>
    /// MeshRenderer/SkinnedMeshRenderer用のテクスチャ取得ストラテジー
    /// 
    /// メッシュオブジェクトから、指定された三角形インデックスに基づいて
    /// 対応するマテリアルのメインテクスチャを取得します。
    /// 
    /// サブメッシュが複数ある場合は三角形の所属を判定し、
    /// 該当するマテリアルのテクスチャを返します。
    /// サブメッシュが1つまたはメッシュが読み取り不可の場合は、
    /// 最初のマテリアルのテクスチャを使用します。
    /// 
    /// UniTaskによる自動リソース管理により、
    /// Rendererオブジェクトが破棄された際に自動的にキャッシュからクリーンアップされます。
    /// </summary>
    public class RendererTextureStrategy : ITextureStrategy
    {
        /// <summary>
        /// 対象のRendererオブジェクト
        /// </summary>
        private readonly Renderer _renderer;
        
        /// <summary>
        /// テクスチャ情報の作業用リスト（ガベージコレクション削減のため再利用）
        /// </summary>
        private readonly List<TextureAlpha> _workTextures;
        
        /// <summary>
        /// Rendererストラテジーキャッシュへの参照（自動クリーンアップ用）
        /// </summary>
        private readonly Dictionary<Renderer, ITextureStrategy> _dictionary;

        /// <summary>
        /// RendererTextureStrategyの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="dictionary">Rendererストラテジーキャッシュ</param>
        /// <param name="renderer">対象のRenderer</param>
        public RendererTextureStrategy(Dictionary<Renderer, ITextureStrategy> dictionary, Renderer renderer)
        {
            _renderer = renderer;
            _dictionary = dictionary;
            _workTextures = new List<TextureAlpha>();
            
            // Rendererオブジェクトが破棄されたら自動でキャッシュクリーンアップ
            DestroyAsync().Forget();
        }

        /// <summary>
        /// 指定された三角形インデックスに基づいてRendererのテクスチャ情報を取得します
        /// 
        /// MeshFilterまたはSkinnedMeshRendererからメッシュを取得し、
        /// 三角形インデックスに基づいて適切なマテリアルのテクスチャを特定します。
        /// </summary>
        /// <param name="hitPoint">使用されません（Renderer用）</param>
        /// <param name="triangleIndex">メッシュの三角形インデックス</param>
        /// <returns>該当するテクスチャのリスト</returns>
        public List<TextureAlpha> GetTextures(Vector3 hitPoint, int triangleIndex = 0)
        {
            // 結果リストをクリア（リスト再利用でGC削減）
            _workTextures.Clear();
            Texture texture = null;

            // MeshFilterからメッシュを取得を試行
            if (_renderer.TryGetComponent(out MeshFilter meshFilter))
            {
                texture = GetTextureFromMesh(meshFilter.mesh, triangleIndex, _renderer.sharedMaterials);
            }
            // SkinnedMeshRendererからメッシュを取得を試行
            else if (_renderer is SkinnedMeshRenderer smr)
            {
                texture = GetTextureFromMesh(smr.sharedMesh, triangleIndex, _renderer.sharedMaterials);
            }
            // 対応するコンポーネントが見つからない場合
            else
            {
                Debug.LogError($"{_renderer.name} has no MeshFilter or SkinnedMeshRenderer! Using default impact effect instead of texture-specific one because we'll be unable to find the correct texture!");
                return _workTextures;
            }

            // テクスチャが取得できた場合はリストに追加（アルファ値は1.0固定）
            if (texture != null)
            {
                _workTextures.Add(new TextureAlpha(1f, texture));
            }

            return _workTextures;
        }

        /// <summary>
        /// メッシュから三角形インデックスに基づいて適切なテクスチャを取得します
        /// 
        /// 複数のサブメッシュがある場合は三角形の所属を判定し、
        /// 該当するマテリアルのテクスチャを返します。
        /// </summary>
        /// <param name="mesh">対象のメッシュ</param>
        /// <param name="triangleIndex">三角形インデックス</param>
        /// <param name="materials">マテリアル配列</param>
        /// <returns>該当するテクスチャ、見つからない場合は最初のマテリアルのテクスチャ</returns>
        private Texture GetTextureFromMesh(Mesh mesh, int triangleIndex, Material[] materials)
        {
            // メッシュが読み取り可能で複数のサブメッシュがある場合の詳細判定
            if (mesh.isReadable && mesh.subMeshCount > 1)
            {
                // ヒットした三角形の頂点インデックスを取得
                int[] hitTriangleIndices = new int[]
                {
                    mesh.triangles[triangleIndex * 3],
                    mesh.triangles[triangleIndex * 3 + 1],
                    mesh.triangles[triangleIndex * 3 + 2]
                };

                // 各サブメッシュで該当する三角形を検索
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int[] submeshTriangles = mesh.GetTriangles(i);
                    
                    // サブメッシュの三角形を3つずつチェック
                    for (int j = 0; j < submeshTriangles.Length; j += 3)
                    {
                        // 三角形の頂点インデックスが一致するかチェック
                        if (submeshTriangles[j] == hitTriangleIndices[0]
                            && submeshTriangles[j + 1] == hitTriangleIndices[1]
                            && submeshTriangles[j + 2] == hitTriangleIndices[2])
                        {
                            // 一致した場合、該当するマテリアルのテクスチャを返す
                            return materials[i].mainTexture;
                        }
                    }
                }
            }

            // デフォルトケース：最初のマテリアルのテクスチャを返す
            return materials[0].mainTexture;
        }

        /// <summary>
        /// Rendererオブジェクトの破棄を監視し、自動的にキャッシュをクリーンアップします
        /// </summary>
        private async UniTaskVoid DestroyAsync()
        {
            // Rendererオブジェクトの破棄を待機
            await _renderer.gameObject.OnDestroyAsync();
            
            // キャッシュから自身を削除してメモリリークを防止
            _dictionary.Remove(_renderer);
        }
    }
} 