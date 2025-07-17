using System.Collections.Generic;
using UnityEngine;
using SCLib_SurfaceImpactFeedback.Effects;
using SCLib_SurfaceImpactFeedback.TextureStrategy;
using ZLinq;


namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// SurfaceImpactFeedbackのログレベル設定
    /// 出力するログの重要度を制御
    /// </summary>
    public enum SurfaceImpactFeedbackLogLevel
    {
        None = 0,       // ログ出力なし
        Error = 1,      // エラーのみ
        Warning = 2,    // 警告以上
        Info = 3,       // 情報以上
        Debug = 4,      // デバッグ情報
        Verbose = 5     // 詳細ログ
    }

    /// <summary>
    /// SurfaceImpactFeedbackのログカテゴリ設定
    /// 出力するログの種類を制御（フラグ列挙型）
    /// </summary>
    [System.Flags]
    public enum SurfaceImpactFeedbackLogCategory
    {
        None = 0,
        Memory = 1 << 0,        // メモリ統計・監視
        Pool = 1 << 1,          // プール作成・破棄
        Cache = 1 << 2,         // キャッシュ操作
        Performance = 1 << 3,   // パフォーマンス関連
        Lifecycle = 1 << 4,     // ライフサイクル（初期化・破棄）
        All = ~0                // すべてのカテゴリ
    }
    /// <summary>
    /// サーフェス固有のインパクトエフェクトを管理するシングルトンクラス
    /// 
    /// このクラスは以下の機能を提供します：
    /// - テクスチャ検出に基づくサーフェスタイプの自動判定
    /// - パーティクルエフェクト、デカール、音声エフェクトの統合管理
    /// - オブジェクトプーリングによるパフォーマンス最適化
    /// - ストラテジーパターンによる柔軟なテクスチャ検出
    /// 
    /// 使用例：
    /// SurfaceImpactFeedback.instance.HandleImpact(hitObject, hitPoint, hitNormal, impactType);
    /// </summary>
    public class SurfaceImpactFeedback : PersistentSingleton<SurfaceImpactFeedback>
    {
        #region フィールド・プロパティ
        
        /// <summary>
        /// 音声エフェクト再生用のインターフェース
        /// Initialize()メソッドで外部から注入される
        /// </summary>
        IImpactSound impactSound;
        
        /// <summary>
        /// 音声システムを初期化する
        /// </summary>
        /// <param name="soundSystem">使用する音声システム</param>
        public static void Initialize(IImpactSound soundSystem)
        {
            // シングルトンインスタンスが存在しない場合はエラー
            if (instance == null)
            {
                Debug.LogError("SurfaceImpactFeedback instance is not initialized!");
                return;
            }
            instance.impactSound = soundSystem;
        }

        /// <summary>
        /// 登録されたサーフェスタイプのリスト
        /// Inspector上で設定される
        /// </summary>
        [SerializeField]
        private List<SurfaceType> Surfaces = new();
        
        /// <summary>
        /// マッチするサーフェスタイプが見つからない場合に使用されるデフォルトサーフェス
        /// </summary>
        [SerializeField]
        private Surface DefaultSurface;
        
        /// <summary>
        /// Renderer検索時に除外するレイヤーマスク
        /// UIやエフェクト専用オブジェクトなど、表面検出から除外したいレイヤーを指定
        /// </summary>
        [SerializeField, Tooltip("Renderer検索時に除外するレイヤーマスク")]
        private LayerMask excludedLayers = 0;

        [SerializeField,Tooltip("エフェクトの親となるTransform。nullの場合はRoot。SurfaceImpactFeedbackは指定しないこと")]
        private Transform effectsParent;

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
        /// メモリ監視用: エフェクト処理回数の統計
        /// </summary>
        private static int totalEffectCount = 0;
        private static int poolCreationCount = 0;
        
        /// <summary>
        /// ログ制御: 現在のログレベル設定
        /// </summary>
        private static SurfaceImpactFeedbackLogLevel logLevel = SurfaceImpactFeedbackLogLevel.Warning;
        
        /// <summary>
        /// ログ制御: 現在のログカテゴリ設定
        /// </summary>
        private static SurfaceImpactFeedbackLogCategory logCategories = SurfaceImpactFeedbackLogCategory.All;
        
        /// <summary>
        /// ログ設定用ScriptableObject
        /// Inspector上で設定するか、実行時に動的に設定
        /// </summary>
        [Header("ログ設定")]
        [SerializeField]
        private SurfaceManagerLogSettings logSettings;
        
        /// <summary>
        /// 現在使用中のログ設定（実行時制御用）
        /// </summary>
        private static SurfaceManagerLogSettings currentLogSettings;
        
        #endregion

        #region Unity ライフサイクル
        
        /// <summary>
        /// シングルトンパターンの実装
        /// 重複したインスタンスを自動的に破棄し、シーン遷移後も維持される
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // ログ設定を読み込み・適用
            InitializeLogSettings();

            // テクスチャからサーフェスへのマッピング辞書を初期化
            InitializeTextureSurfaceMap();
        }
        
        /// <summary>
        /// ログ設定を初期化・適用する
        /// ScriptableObjectから設定を読み込み、静的変数に設定
        /// </summary>
        private void InitializeLogSettings()
        {
            try
            {
                // ScriptableObjectが設定されている場合は読み込み
                if (logSettings != null)
                {
                    // 設定の妥当性をチェック
                    if (!logSettings.ValidateSettings())
                    {
                        Debug.LogError("[SurfaceImpactFeedback] ログ設定が無効です。デフォルト設定を使用します。");
                        ApplyDefaultLogSettings();
                        return;
                    }
                    
                    // 設定を適用
                    ApplyLogSettings(logSettings);
                    LogInfo($"ログ設定を読み込みました: {logSettings.name}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                    LogDebug($"設定詳細:\n{logSettings.ToString()}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                }
                else
                {
                    // ScriptableObjectが設定されていない場合はデフォルト設定
                    ApplyDefaultLogSettings();
                    LogInfo("ログ設定が指定されていません。デフォルト設定を使用します。", SurfaceImpactFeedbackLogCategory.Lifecycle);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SurfaceImpactFeedback] ログ設定の初期化中にエラーが発生: {ex.Message}");
                ApplyDefaultLogSettings();
            }
        }
        
        /// <summary>
        /// 指定されたログ設定を適用する
        /// </summary>
        /// <param name="settings">適用するログ設定</param>
        private static void ApplyLogSettings(SurfaceManagerLogSettings settings)
        {
            if (settings == null) return;
            
            currentLogSettings = settings;
            logLevel = settings.logLevel;
            logCategories = settings.logCategories;
            
            // パフォーマンス監視設定も更新
            if (settings.enableMemoryCheck)
            {
                // メモリチェック間隔の更新は実際のチェック処理で使用
            }
        }
        
        /// <summary>
        /// デフォルトのログ設定を適用する
        /// </summary>
        private static void ApplyDefaultLogSettings()
        {
            currentLogSettings = null;
            logLevel = SurfaceImpactFeedbackLogLevel.Warning;
            logCategories = SurfaceImpactFeedbackLogCategory.All;
        }
        
        /// <summary>
        /// テクスチャからサーフェスへのマッピング辞書を初期化
        /// O(1)の高速検索を実現するためのパフォーマンス最適化
        /// </summary>
        private void InitializeTextureSurfaceMap()
        {
            textureSurfaceMap.Clear();
            
            for (int i = 0; i < Surfaces.Count; i++)
            {
                var surfaceType = Surfaces[i];
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
            
            LogInfo($"テクスチャマッピング辞書を初期化しました: {textureSurfaceMap.Count} 個のエントリ", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }

        /// <summary>
        /// オブジェクト破棄時のリソース解放処理
        /// メモリリークを防ぐためキャッシュとプールをクリア
        /// </summary>
        void OnDestroy()
        {
            try
            {
                LogInfo("リソース解放を開始します", SurfaceImpactFeedbackLogCategory.Lifecycle);
                
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
                            LogError($"プール破棄中にエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
                        }
                    }
                    ObjectPools.Clear();
                    LogInfo($"{poolCount} 個のプールを破棄しました", SurfaceImpactFeedbackLogCategory.Pool);
                }

                // EffectPoolFactoryの静的キャッシュをクリア
                EffectPoolFactory.ClearCache();

                // その他のキャッシュをクリア
                terrainStrategyCache?.Clear();
                rendererStrategyCache?.Clear();
                textureSurfaceMap?.Clear();

                LogInfo("リソース解放が完了しました", SurfaceImpactFeedbackLogCategory.Lifecycle);
            }
            catch (System.Exception ex)
            {
                LogError($"リソース解放中に重大なエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Lifecycle);
            }
            finally
            {
                // 参照をnullに設定してGCを促進
                terrainStrategyCache = null;
                rendererStrategyCache = null;
                ObjectPools = null;
                textureSurfaceMap = null;
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
            return (excludedLayers.value & (1 << layer)) != 0;
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
        public void HandleImpact(GameObject HitObject, in Vector3 HitPoint, in Vector3 HitNormal, ImpactType Impact, int TriangleIndex = 0)
        {
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
                LogDebug($"No textures found at {HitPoint} on {HitObject.name}. Using default surface.", SurfaceImpactFeedbackLogCategory.Performance);
                PlayEffectsForSurface(HitObject, HitPoint, HitNormal, DefaultSurface, Impact);
                return;
            }
            
            // 取得した各テクスチャに対してエフェクト処理を実行
            for (int i = 0; i < textures.Count; i++)
            {
                var textureAlpha = textures[i];

                // テクスチャからサーフェスを高速検索（O(1)）
                Surface targetSurface = textureSurfaceMap.TryGetValue(textureAlpha.Texture, out var surface)
                    ? surface
                    : DefaultSurface;

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
                    pool = EffectPoolFactory.Create(effectsParent, spawnObjectEffect);
                    ObjectPools.Add(spawnObjectEffect.Prefab, pool);
                    poolCreationCount++;
                    
                    // メモリ監視: 新しいプール作成をログ出力
                    LogInfo($"新しいプールを作成: {spawnObjectEffect.Prefab.name} (総プール数: {ObjectPools.Count})", SurfaceImpactFeedbackLogCategory.Pool);
                }
                
                totalEffectCount++;
                
                
                // 定期的なメモリ監視
                PerformMemoryCheck();
                
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
                pool.PlayEffect(effectParameters, destroyCancellationToken).Forget();
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
        
        #region ログ制御システム
        
        /// <summary>
        /// ログレベルを設定する
        /// </summary>
        /// <param name="level">設定するログレベル</param>
        /// <param name="force">強制的に設定するか（実行時制御設定を無視）</param>
        public static void SetLogLevel(SurfaceImpactFeedbackLogLevel level, bool force = false)
        {
            // 実行時制御が無効で、強制フラグが立っていない場合は設定を拒否
            if (!force && currentLogSettings != null && !currentLogSettings.enableRuntimeLogLevelChange)
            {
                LogWarning($"実行時のログレベル変更が無効に設定されています。現在: {logLevel}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                return;
            }
            
            var oldLevel = logLevel;
            logLevel = level;
            LogInfo($"ログレベルを変更しました: {oldLevel} → {level}", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }
        
        /// <summary>
        /// ログカテゴリを設定する
        /// </summary>
        /// <param name="categories">設定するログカテゴリ（フラグ列挙型）</param>
        /// <param name="force">強制的に設定するか（実行時制御設定を無視）</param>
        public static void SetLogCategories(SurfaceImpactFeedbackLogCategory categories, bool force = false)
        {
            // 実行時制御が無効で、強制フラグが立っていない場合は設定を拒否
            if (!force && currentLogSettings != null && !currentLogSettings.enableRuntimeCategoryChange)
            {
                LogWarning($"実行時のログカテゴリ変更が無効に設定されています。現在: {logCategories}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                return;
            }
            
            var oldCategories = logCategories;
            logCategories = categories;
            LogInfo($"ログカテゴリを変更しました: {oldCategories} → {categories}", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }
        
        /// <summary>
        /// ログ設定をScriptableObjectから再読み込みする
        /// </summary>
        /// <param name="newSettings">新しいログ設定（nullの場合は現在の設定を再読み込み）</param>
        public static void ReloadLogSettings(SurfaceManagerLogSettings newSettings = null)
        {
            if (instance == null) return;
            
            if (newSettings != null)
            {
                instance.logSettings = newSettings;
            }
            
            instance.InitializeLogSettings();
        }
        
        /// <summary>
        /// 現在のログレベルを取得する
        /// </summary>
        /// <returns>現在のログレベル</returns>
        public static SurfaceImpactFeedbackLogLevel GetLogLevel()
        {
            return logLevel;
        }
        
        /// <summary>
        /// 現在のログカテゴリを取得する
        /// </summary>
        /// <returns>現在のログカテゴリ</returns>
        public static SurfaceImpactFeedbackLogCategory GetLogCategories()
        {
            return logCategories;
        }
        
        /// <summary>
        /// 指定されたレベルとカテゴリのログが出力対象かチェック
        /// </summary>
        /// <param name="level">チェックするレベル</param>
        /// <param name="category">チェックするカテゴリ</param>
        /// <returns>出力対象の場合true</returns>
        private static bool ShouldLog(SurfaceImpactFeedbackLogLevel level, SurfaceImpactFeedbackLogCategory category)
        {
            // ログレベルが無効の場合は出力しない
            if (logLevel == SurfaceImpactFeedbackLogLevel.None) return false;
            
            // レベルチェック: 現在のレベル以下のみ出力
            if (level > logLevel) return false;
            
            // カテゴリチェック: 指定されたカテゴリが有効か
            if ((logCategories & category) == 0) return false;
            
            return true;
        }
        
        /// <summary>
        /// エラーログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Error, category))
            {
                Debug.LogError($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }
        
        /// <summary>
        /// 警告ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Warning, category))
            {
                Debug.LogWarning($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }
        
        /// <summary>
        /// 情報ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogInfo(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Info, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }
        
        /// <summary>
        /// デバッグログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogDebug(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Debug, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }
        
        /// <summary>
        /// 詳細ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogVerbose(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Verbose, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}:Verbose] {message}");
            }
        }
        
        #endregion
        
        #region メモリ監視・デバッグ機能
        
        /// <summary>
        /// メモリ使用状況の統計情報を取得
        /// デバッグ・監視用
        /// </summary>
        /// <returns>メモリ使用状況の詳細情報</returns>
        public static string GetMemoryStatistics()
        {
            if (instance == null) return "SurfaceImpactFeedback が初期化されていません";
            
            var factoryStats = EffectPoolFactory.GetCacheStatistics();
            
            return $"=== SurfaceImpactFeedback メモリ統計 ===\n" +
                   $"総エフェクト処理回数: {totalEffectCount}\n" +
                   $"作成されたプール数: {poolCreationCount}\n" +
                   $"現在のプール数: {instance.ObjectPools?.Count ?? 0}\n" +
                   $"テクスチャマッピング数: {instance.textureSurfaceMap?.Count ?? 0}\n" +
                   $"Terrainキャッシュ数: {instance.terrainStrategyCache?.Count ?? 0}\n" +
                   $"Rendererキャッシュ数: {instance.rendererStrategyCache?.Count ?? 0}\n" +
                   $"{factoryStats}\n" +
                   $"現在のメモリ使用量: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024:F1} MB";
        }
        
        /// <summary>
        /// メモリ統計をデバッグログに出力
        /// 開発環境でのみ実行される
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogMemoryStatistics()
        {
            LogDebug(GetMemoryStatistics(), SurfaceImpactFeedbackLogCategory.Memory);
            EffectPoolFactory.LogCacheStatistics();
        }
        
        /// <summary>
        /// 定期的なメモリ監視機能
        /// 大量エフェクト使用時のメモリリーク早期発見用
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void PerformMemoryCheck()
        {
            // ログ設定でメモリチェックが無効の場合は処理をスキップ
            if (currentLogSettings != null && !currentLogSettings.enableMemoryCheck)
            {
                return;
            }
            
            // チェック間隔を設定から取得（デフォルトは1000）
            int checkInterval = currentLogSettings?.memoryCheckInterval ?? 1000;
            long warningThreshold = currentLogSettings?.memoryWarningThreshold ?? 500;
            
            if (totalEffectCount > 0 && totalEffectCount % checkInterval == 0)
            {
                LogWarning($"メモリチェック: {totalEffectCount} 回のエフェクト処理が完了", SurfaceImpactFeedbackLogCategory.Memory);
                
                // キャッシュ統計ログが有効な場合のみ出力
                if (currentLogSettings == null || currentLogSettings.enableCacheStatistics)
                {
                    LogMemoryStatistics();
                }
                
                // メモリ使用量が異常に多い場合の警告
                long memoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
                if (memoryMB > warningThreshold)
                {
                    LogError($"警告: メモリ使用量が異常に多くなっています ({memoryMB} MB / しきい値: {warningThreshold} MB)", SurfaceImpactFeedbackLogCategory.Memory);
                }
            }
        }
        
        /// <summary>
        /// 強制的なメモリクリーンアップ
        /// テスト・デバッグ用
        /// </summary>
        public static void ForceCleanup()
        {
            if (instance?.ObjectPools != null)
            {
                foreach (var pool in instance.ObjectPools.Values)
                {
                    pool?.Dispose();
                }
                instance.ObjectPools.Clear();
            }
            
            EffectPoolFactory.ClearCache();
            System.GC.Collect();
            
            LogInfo("強制クリーンアップを実行しました", SurfaceImpactFeedbackLogCategory.Memory);
        }
        
        #endregion
    }
}