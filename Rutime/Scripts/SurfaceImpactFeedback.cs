using System.Collections.Generic;
using UnityEngine;
using SCLib_SurfaceImpactFeedback.Effects;
using SCLib_SurfaceImpactFeedback.TextureStrategy;
using SCLib_SurfaceImpactFeedback.Utilities;
using ZLinq;
using System;
using System.Threading;


namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// サーフェス固有のインパクトエフェクトを管理するPure C#クラス
    /// 
    /// このクラスは以下の機能を提供します：
    /// - テクスチャ検出に基づくサーフェスタイプの自動判定
    /// - パーティクルエフェクト、デカール、音声エフェクトの統合管理
    /// - オブジェクトプーリングによるパフォーマンス最適化
    /// - ストラテジーパターンによる柔軟なテクスチャ検出
    /// - コンストラクタ注入による設定の外部化
    /// 
    /// 使用例：
    /// var config = Resources.Load<SurfaceImpactConfiguration>("Config");
    /// var feedback = new SurfaceImpactFeedback(soundSystem, config);
    /// feedback.HandleImpact(hitObject, hitPoint, hitNormal, impactType);
    /// </summary>
    public class SurfaceImpactFeedback : ISurfaceImpactFeedback, IDisposable
    {
        CancellationTokenSource cts;
        #region フィールド・プロパティ

        /// <summary>
        /// 音声エフェクト再生用のインターフェース
        /// Initialize()メソッドで外部から注入される
        /// </summary>
        readonly IImpactSound impactSound;
        readonly Transform parentTransform;

        /// <summary>
        /// SurfaceImpactFeedbackシステムを初期化する
        /// </summary>
        /// <param name="soundSystem">使用する音声システム</param>
        /// <param name="parent">エフェクトの親Transform（nullの場合はRoot）</param>
        /// <param name="configuration">システム設定</param>
        /// <exception cref="ArgumentNullException">必須パラメータがnullの場合</exception>
        public SurfaceImpactFeedback(SurfaceImpactConfiguration configuration, Transform parent, IImpactSound soundSystem)
        {
            impactSound = soundSystem;
            parentTransform = parent;

            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            cts = new CancellationTokenSource();

            // 設定の妥当性チェック
            if (!configuration.ValidateConfiguration())
            {
                throw new ArgumentException("設定が無効です", nameof(configuration));
            }

            // システムを初期化
            Initialize();
        }

        /// <summary>
        /// システム設定（コンストラクタで注入される）
        /// </summary>
        private readonly SurfaceImpactConfiguration configuration;

        /// <summary>
        /// エフェクトオブジェクトプールのキャッシュ
        /// プレハブごとにオブジェクトプールを管理し、パフォーマンスを最適化
        /// </summary>
        private Dictionary<GameObject, IEffectObjectPool> ObjectPools = new();

        /// <summary>
        /// テクスチャからサーフェスへの高速マッピング辞書
        /// O(1)の検索時間でパフォーマンスを最適化
        /// </summary>
        private Dictionary<Texture, Surface> textureSurfaceMap = new();

        /// <summary>
        /// 初期化完了フラグ
        /// </summary>
        private bool isInitialized = false;

        /// <summary>
        /// 破棄済みフラグ
        /// </summary>
        private bool disposed = false;

        #endregion

        #region 初期化・破棄処理

        /// <summary>
        /// システムを初期化する
        /// </summary>
        /// <exception cref="InvalidOperationException">既に初期化済みの場合</exception>
        private void Initialize()
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("SurfaceImpactFeedbackは既に初期化されています");
            }

            try
            {
                // ログ設定を読み込み・適用
                SurfaceImpactLogger.Initialize(configuration.logSettings);

                // テクスチャからサーフェスへのマッピング辞書を初期化
                InitializeTextureSurfaceMap();

                isInitialized = true;
                SurfaceImpactLogger.LogInfo("SurfaceImpactFeedbackシステムの初期化が完了しました", SurfaceImpactFeedbackLogCategory.Lifecycle);
            }
            catch (Exception ex)
            {
                SurfaceImpactLogger.LogError($"SurfaceImpactFeedbackシステムの初期化中にエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                throw;
            }
        }


        /// <summary>
        /// テクスチャからサーフェスへのマッピング辞書を初期化
        /// O(1)の高速検索を実現するためのパフォーマンス最適化
        /// </summary>
        private void InitializeTextureSurfaceMap()
        {
            textureSurfaceMap.Clear();

            for (int i = 0; i < configuration.surfaces.Count; i++)
            {
                var surfaceType = configuration.surfaces[i];
                if (surfaceType.Albedo != null && surfaceType.Surface != null)
                {
                    // 重複チェック - 同じテクスチャが複数のサーフェスに割り当てられている場合は警告
                    if (textureSurfaceMap.ContainsKey(surfaceType.Albedo))
                    {
                        Debug.LogWarning($"テクスチャ '{surfaceType.Albedo.name}' が複数のサーフェスに割り当てられています。最初の割り当てを使用します。");
                        continue;
                    }

                    textureSurfaceMap.Add(surfaceType.Albedo, surfaceType.Surface);
                }
            }

            SurfaceImpactLogger.LogInfo($"テクスチャマッピング辞書を初期化しました: {textureSurfaceMap.Count} 個のエントリ", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }

        /// <summary>
        /// リソース解放処理（IDisposableパターン）
        /// メモリリークを防ぐためキャッシュとプールをクリア
        /// </summary>
        private void DisposeCore()
        {
            try
            {
                SurfaceImpactLogger.LogInfo("リソース解放を開始します", SurfaceImpactFeedbackLogCategory.Lifecycle);

                // オブジェクトプールの破棄
                if (ObjectPools != null)
                {
                    int poolCount = ObjectPools.Count;
                    foreach (var pool in ObjectPools.Values)
                    {
                        try
                        {
                            pool?.Dispose();
                        }
                        catch (System.Exception ex)
                        {
                            SurfaceImpactLogger.LogError($"プール破棄中にエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
                        }
                    }
                    ObjectPools.Clear();
                    SurfaceImpactLogger.LogInfo($"{poolCount} 個のプールを破棄しました", SurfaceImpactFeedbackLogCategory.Pool);
                }

                // EffectPoolFactoryの静的キャッシュをクリア
                EffectPoolFactory.ClearCache();

                // その他のキャッシュをクリア
                terrainStrategyCache?.Clear();
                rendererStrategyCache?.Clear();
                textureSurfaceMap?.Clear();

                SurfaceImpactLogger.LogInfo("リソース解放が完了しました", SurfaceImpactFeedbackLogCategory.Lifecycle);
            }
            catch (System.Exception ex)
            {
                SurfaceImpactLogger.LogError($"リソース解放中に重大なエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Lifecycle);
            }
            finally
            {
                // 参照をnullに設定してGCを促進
                terrainStrategyCache = null;
                rendererStrategyCache = null;
                ObjectPools = null;
                textureSurfaceMap = null;

                disposed = true;
            }
        }

        #endregion

        #region テクスチャストラテジー管理

        /// <summary>
        /// Terrain用のテクスチャストラテジーキャッシュ
        /// 同じTerrainに対して複数回アクセスする際のパフォーマンス最適化
        /// </summary>
        private Dictionary<Terrain, TerrainTextureStrategy> terrainStrategyCache = new();

        /// <summary>
        /// Terrain用のテクスチャストラテジーを取得または作成する
        /// </summary>
        /// <param name="terrain">対象のTerrain</param>
        /// <returns>テクスチャストラテジー、terrainがnullの場合はnull</returns>
        private ITextureStrategy GetTerrainTextureStrategy(Terrain terrain)
        {
            if (terrain == null) return null;

            // キャッシュから既存のストラテジーを検索（パフォーマンス最適化）
            if (!terrainStrategyCache.TryGetValue(terrain, out var strategy))
            {
                // 新しいストラテジーを作成してキャッシュに追加
                strategy = new TerrainTextureStrategy(terrainStrategyCache, terrain);
                terrainStrategyCache.Add(terrain, strategy);
            }

            return strategy;
        }

        /// <summary>
        /// Renderer用のテクスチャストラテジーキャッシュ
        /// 同じRendererに対して複数回アクセスする際のパフォーマンス最適化
        /// </summary>
        private Dictionary<Renderer, ITextureStrategy> rendererStrategyCache = new();

        /// <summary>
        /// Renderer用のテクスチャストラテジーを取得または作成する
        /// </summary>
        /// <param name="renderer">対象のRenderer</param>
        /// <returns>テクスチャストラテジー、rendererがnullの場合はnull</returns>
        private ITextureStrategy GetRendererTextureStrategy(Renderer renderer)
        {
            if (renderer == null) return null;

            // キャッシュから既存のストラテジーを検索（パフォーマンス最適化）
            if (!rendererStrategyCache.TryGetValue(renderer, out var strategy))
            {
                // 新しいストラテジーを作成してキャッシュに追加
                strategy = new RendererTextureStrategy(rendererStrategyCache, renderer);
                rendererStrategyCache.Add(renderer, strategy);
            }

            return strategy;
        }

        /// <summary>
        /// 指定されたレイヤーが除外レイヤーマスクに含まれているかを判定する
        /// </summary>
        /// <param name="layer">判定するレイヤー</param>
        /// <returns>除外対象の場合true、そうでなければfalse</returns>
        private bool IsLayerExcluded(int layer)
        {
            return (configuration.excludedLayers.value & (1 << layer)) != 0;
        }

        #endregion

        #region インパクト処理
        /// <summary>
        /// インパクトエフェクトのメイン処理
        /// ヒットしたオブジェクトのテクスチャを解析し、適切なサーフェスエフェクトを実行する
        /// </summary>
        /// <param name="HitObject">ヒットしたGameObject</param>
        /// <param name="HitPoint">ヒットポイントの世界座標</param>
        /// <param name="HitNormal">ヒット面の法線ベクトル</param>
        /// <param name="Impact">インパクトタイプ（弾丸、爆発等）</param>
        /// <param name="TriangleIndex">メッシュの三角形インデックス（オプション）</param>
        /// <exception cref="ObjectDisposedException">既に破棄済みの場合</exception>
        public void HandleImpact(GameObject HitObject, in Vector3 HitPoint, in Vector3 HitNormal, ImpactType Impact, int TriangleIndex = 0)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SurfaceImpactFeedback), "既に破棄済みのSurfaceImpactFeedbackにアクセスしようとしました");
            }

            if (!isInitialized)
            {
                SurfaceImpactLogger.LogError("初期化が完了していないSurfaceImpactFeedbackにアクセスしようとしました", SurfaceImpactFeedbackLogCategory.Lifecycle);
                return;
            }

            // インパクト処理を実行
            Play(HitObject, HitPoint, HitNormal, Impact, TriangleIndex);
        }

        /// <summary>
        /// インパクトエフェクトのメイン処理
        /// ヒットしたオブジェクトのテクスチャを解析し、適切なサーフェスエフェクトを実行する
        /// </summary>
        /// <param name="HitObject">ヒットしたGameObject</param>
        /// <param name="HitPoint">ヒットポイントの世界座標</param>
        /// <param name="HitNormal">ヒット面の法線ベクトル</param>
        /// <param name="Impact">インパクトタイプ（弾丸、爆発等）</param>
        /// <param name="TriangleIndex">メッシュの三角形インデックス（オプション）</param>
        private void Play(GameObject HitObject, in Vector3 HitPoint, in Vector3 HitNormal, ImpactType Impact, int TriangleIndex = 0)
        {
            ITextureStrategy textureStrategy;

            // ヒットオブジェクトの種類に応じて適切なテクスチャストラテジーを選択
            if (HitObject.TryGetComponent(out Terrain terrain))
            {
                // Terrain用のストラテジーを取得（地形での衝突）
                textureStrategy = GetTerrainTextureStrategy(terrain);
            }
            else if (HitObject.TryGetComponent(out Renderer renderer))
            {
                // Renderer用のストラテジーを取得（メッシュオブジェクトでの衝突）
                textureStrategy = GetRendererTextureStrategy(renderer);
            }
            else
            {
                var renderers = HitObject.GetComponentsInChildren<Renderer>();

                if (renderers != null && renderers.Length > 0)
                {
                    Vector3 hitPosition = HitPoint;  // inパラメータをローカル変数にコピー
                    var closestRenderer = renderers
                        .AsValueEnumerable()
                        .Where(r => !IsLayerExcluded(r.gameObject.layer))
                        .OrderBy(r => (hitPosition - r.transform.position).sqrMagnitude)
                        .FirstOrDefault();

                    if (closestRenderer != null)
                    {
                        textureStrategy = GetRendererTextureStrategy(closestRenderer);
                        HitObject = closestRenderer.gameObject;
                    }
                    else
                    {
                        // 除外レイヤーフィルター後にRendererが見つからない場合
                        Debug.LogWarning($"{HitObject.name} の子オブジェクトに有効なRenderer（除外レイヤー以外）が見つかりません！除外レイヤー設定を確認してください。");
                        return;
                    }
                }
                else
                {
                    // 対応するコンポーネントが見つからない場合はwarning
                    Debug.LogWarning($"{HitObject.name} has no valid texture strategy!");
                    return;
                }
            }

            // 衝突地点のテクスチャ情報を取得
            var textures = textureStrategy.GetTextures(HitPoint, TriangleIndex);
            if (textures == null || textures.Count == 0)
            {
                // ヒット地点にテクスチャが見つからない場合はデフォルトサーフェスを使用
                SurfaceImpactLogger.LogDebug($"No textures found at {HitPoint} on {HitObject.name}. Using default surface.", SurfaceImpactFeedbackLogCategory.Performance);
                PlayEffectsForSurface(HitObject, HitPoint, HitNormal, configuration.defaultSurface, Impact);
                return;
            }

            // 取得した各テクスチャに対してエフェクト処理を実行
            for (int i = 0; i < textures.Count; i++)
            {
                var textureAlpha = textures[i];

                // テクスチャからサーフェスを高速検索（O(1)）
                Surface targetSurface = textureSurfaceMap.TryGetValue(textureAlpha.Texture, out var surface)
                    ? surface
                    : configuration.defaultSurface;

                // 決定したサーフェスに基づいてエフェクトを実行
                PlayEffectsForSurface(HitObject, HitPoint, HitNormal, targetSurface, Impact);
            }
        }

        /// <summary>
        /// 指定されたサーフェスに対応するエフェクトを実行する
        /// インパクトタイプに合致するエフェクト設定を検索し、該当するものを実行
        /// </summary>
        /// <param name="HitPoint">エフェクト発生ポイント</param>
        /// <param name="HitNormal">エフェクト発生面の法線</param>
        /// <param name="surface">実行対象のサーフェス設定</param>
        /// <param name="impactType">インパクトタイプ</param>
        private void PlayEffectsForSurface(GameObject obj, in Vector3 HitPoint, in Vector3 HitNormal, Surface surface, ImpactType impactType)
        {
            // サーフェスに登録されたエフェクト設定を順次チェック
            for (int i = 0; i < surface.ImpactTypeEffects.Count; i++)
            {
                var typeEffect = surface.ImpactTypeEffects[i];

                // インパクトタイプが一致する場合のみエフェクトを実行
                if (typeEffect.ImpactType == impactType)
                {
                    PlayEffects(obj, HitPoint, HitNormal, typeEffect.SurfaceEffect);
                }
            }
        }

        /// <summary>
        /// SurfaceEffectに定義されたエフェクトを具体的に実行する
        /// パーティクル・デカールエフェクトと音声エフェクトの両方を処理
        /// </summary>
        /// <param name="HitPoint">エフェクト発生ポイント</param>
        /// <param name="HitNormal">エフェクト発生面の法線</param>
        /// <param name="SurfaceEffect">実行するサーフェスエフェクト設定</param>
        private void PlayEffects(GameObject obj, in Vector3 HitPoint, in Vector3 HitNormal, SurfaceEffect SurfaceEffect)
        {
            // パーティクル・デカールエフェクトの処理
            for (int i = 0; i < SurfaceEffect.SpawnObjectEffects.Count; i++)
            {
                var spawnObjectEffect = SurfaceEffect.SpawnObjectEffects[i];

                // 確率によるエフェクト制御（パフォーマンス最適化）
                if (spawnObjectEffect.Probability <= UnityEngine.Random.value) continue;

                // オブジェクトプールを取得または新規作成（遅延初期化 + 1回のハッシュ検索で最適化）
                if (!ObjectPools.TryGetValue(spawnObjectEffect.Prefab, out var pool))
                {
                    pool = EffectPoolFactory.Create(parentTransform, spawnObjectEffect);
                    ObjectPools.Add(spawnObjectEffect.Prefab, pool);
                    SurfaceImpactLogger.IncrementPoolCreationCount();

                    // メモリ監視: 新しいプール作成をログ出力
                    SurfaceImpactLogger.LogInfo($"新しいプールを作成: {spawnObjectEffect.Prefab.name} (総プール数: {ObjectPools.Count})", SurfaceImpactFeedbackLogCategory.Pool);
                }

                SurfaceImpactLogger.IncrementEffectCount();


                // 定期的なメモリ監視
                SurfaceImpactLogger.PerformMemoryCheck(
                    ObjectPools?.Count ?? 0,
                    textureSurfaceMap?.Count ?? 0,
                    terrainStrategyCache?.Count ?? 0,
                    rendererStrategyCache?.Count ?? 0
                );

                // エフェクトの向きを法線ベクトルに設定
                Vector3 forward = HitNormal;
                Vector3 offset = Vector3.zero;

                // ランダム回転が有効な場合の回転値計算
                if (spawnObjectEffect.RandomizeRotation)
                {
                    offset = new Vector3(
                        UnityEngine.Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.x),
                        UnityEngine.Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.y),
                        UnityEngine.Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.z)
                    );
                }

                // エフェクトパラメータを作成してプールからエフェクトを取得・実行（z-fighting回避のため微小オフセット追加）
                var effectParameters = new EffectParameters(
                    HitPoint + HitNormal * 0.001f,
                    forward,
                    offset,
                    obj.transform
                );
                pool.PlayEffect(effectParameters, cts.Token).Forget();
            }

            // 音声エフェクトの処理
            for (int i = 0; i < SurfaceEffect.PlayAudioEffects.Count; i++)
            {
                var playAudioEffect = SurfaceEffect.PlayAudioEffects[i];

                // 登録された音声クリップからランダムに選択
                int clipCount = playAudioEffect.AudioClips.Count;
                AudioClip clip = playAudioEffect.AudioClips[UnityEngine.Random.Range(0, clipCount)];

                // 音声システムが初期化されている場合のみ再生
                impactSound?.Play(clip, playAudioEffect.audioMixerGroup, HitPoint);
            }
        }

        #endregion

        #region IDisposable実装

        /// <summary>
        /// リソース破棄の実装
        /// </summary>
        /// <param name="disposing">Dispose()から呼ばれた場合true、ファイナライザからの場合false</param>
        public void Dispose()
        {
            if (disposed) return;

            // マネージドリソースの破棄
            cts?.Cancel();
            cts?.Dispose();
            cts = null;

            // システムリソースの破棄
            DisposeCore();
        }

        ~SurfaceImpactFeedback()
        {
            if (!disposed)
            {
                SurfaceImpactLogger.LogWarning($"{GetType().Name} - Dispose()が呼ばれずにファイナライザが実行されました。メモリリークの可能性があります。", SurfaceImpactFeedbackLogCategory.Pool);
                Dispose();
            }
        }

        /// <summary>
        /// 強制的なメモリクリーンアップ
        /// テスト・デバッグ用
        /// </summary>
        public void ForceCleanup()
        {
            DisposeCore();
        }

        #endregion
    }
}