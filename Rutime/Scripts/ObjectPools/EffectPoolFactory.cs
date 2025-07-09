using UnityEngine;
using SCLib_SurfaceImpactFeedback.Effects;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SCLib_SurfaceImpactFeedback
{
    public class EffectPoolFactory
    {
        /// <summary>
        /// コンポーネント型からプール型へのマッピングキャッシュ
        /// リフレクション処理を1回だけ実行し、結果を保存
        /// </summary>
        private static readonly Dictionary<Type, Type> poolTypeCache = new Dictionary<Type, Type>();
        
        /// <summary>
        /// 型ごとのコンストラクタデリゲートキャッシュ
        /// コンパイル済みデリゲートによる高速インスタンス生成
        /// </summary>
        private static readonly Dictionary<Type, Func<GameObject, bool, int, int, IEffectObjectPool>> constructorCache = 
            new Dictionary<Type, Func<GameObject, bool, int, int, IEffectObjectPool>>();
        
        /// <summary>
        /// キャッシュヒット率の統計情報
        /// </summary>
        private static int cacheHits = 0;
        private static int cacheMisses = 0;
        public static IEffectObjectPool Create(SpawnObjectEffect effect, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            if (effect.Prefab == null)
            {
                throw new ArgumentNullException(nameof(effect.Prefab));
            }

            return effect.effectType switch
            {
                EffectType.Particle => new ParticleObjectPool(effect.Prefab, collectionCheck, defaultCapacity, maxSize),
                EffectType.Decal => CreateDecalPool(effect.Prefab, collectionCheck, defaultCapacity, maxSize),
                _ => throw new ArgumentException($"未対応のエフェクトタイプです: {effect.effectType}")
            };
        }

        /// <summary>
        /// コンストラクタデリゲートを生成してキャッシュに保存
        /// Expression Treeを使用してコンパイル済みデリゲートを作成
        /// </summary>
        /// <param name="poolType">プール型</param>
        /// <returns>コンストラクタデリゲート</returns>
        private static Func<GameObject, bool, int, int, IEffectObjectPool> CreateConstructorDelegate(Type poolType)
        {
            // コンストラクタのパラメータ型を定義
            var constructorTypes = new[] { typeof(GameObject), typeof(bool), typeof(int), typeof(int) };
            var constructor = poolType.GetConstructor(constructorTypes);
            
            if (constructor == null)
            {
                throw new ArgumentException($"プール型 '{poolType.Name}' に適切なコンストラクタが見つかりません");
            }

            // Expression Treeでコンストラクタ呼び出しを作成
            var param1 = Expression.Parameter(typeof(GameObject), "prefab");
            var param2 = Expression.Parameter(typeof(bool), "collectionCheck");
            var param3 = Expression.Parameter(typeof(int), "defaultCapacity");
            var param4 = Expression.Parameter(typeof(int), "maxSize");
            
            var newExpression = Expression.New(constructor, param1, param2, param3, param4);
            var castExpression = Expression.Convert(newExpression, typeof(IEffectObjectPool));
            
            var lambda = Expression.Lambda<Func<GameObject, bool, int, int, IEffectObjectPool>>(
                castExpression, param1, param2, param3, param4);
            
            return lambda.Compile();
        }

        /// <summary>
        /// デカールプールを作成する（キャッシュ最適化版）
        /// プレハブのコンポーネントを自動検出して適切なプールを生成
        /// 2回目以降の同じ型は高速なキャッシュから取得
        /// </summary>
        /// <param name="decalPrefab">デカールプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        /// <returns>適切なデカールプール</returns>
        /// <exception cref="ArgumentException">IDecalを実装したコンポーネントが見つからない場合</exception>
        private static IEffectObjectPool CreateDecalPool(GameObject decalPrefab, bool collectionCheck, int defaultCapacity, int maxSize)
        {
            // IDecal実装をチェック
            if (decalPrefab.TryGetComponent<IDecal>(out var decalComponent))
            {
                var componentType = decalComponent.GetType();
                
                // キャッシュからコンストラクタデリゲートを取得
                if (constructorCache.TryGetValue(componentType, out var cachedConstructor))
                {
                    cacheHits++;
                    return cachedConstructor(decalPrefab, collectionCheck, defaultCapacity, maxSize);
                }
                
                // キャッシュミス - 新しいデリゲートを作成
                cacheMisses++;
                
                // プール型をキャッシュから取得または作成
                if (!poolTypeCache.TryGetValue(componentType, out var poolType))
                {
                    poolType = typeof(DecalObjectPool<>).MakeGenericType(componentType);
                    poolTypeCache[componentType] = poolType;
                }
                
                // コンストラクタデリゲートを作成してキャッシュに保存
                var constructorDelegate = CreateConstructorDelegate(poolType);
                constructorCache[componentType] = constructorDelegate;
                
                return constructorDelegate(decalPrefab, collectionCheck, defaultCapacity, maxSize);
            }

            throw new ArgumentException($"プレハブ '{decalPrefab.name}' に IDecal を実装したコンポーネントが見つかりません");
        }
        
        /// <summary>
        /// キャッシュ統計情報を取得
        /// パフォーマンス監視用
        /// </summary>
        /// <returns>キャッシュヒット率と統計情報</returns>
        public static string GetCacheStatistics()
        {
            int totalRequests = cacheHits + cacheMisses;
            float hitRate = totalRequests > 0 ? (float)cacheHits / totalRequests * 100f : 0f;
            
            return $"EffectPoolFactory Cache Stats - ヒット率: {hitRate:F1}% " +
                   $"(ヒット: {cacheHits}, ミス: {cacheMisses}, " +
                   $"キャッシュ済み型: {constructorCache.Count})";
        }
        
        /// <summary>
        /// 開発環境用: キャッシュ統計をデバッグログに出力
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogCacheStatistics()
        {
            if (cacheMisses > 0 || cacheHits > 0)
            {
                SurfaceImpactFeedback.LogDebug($"EffectPoolFactory - {GetCacheStatistics()}", SurfaceImpactFeedbackLogCategory.Cache);
            }
        }
        
        /// <summary>
        /// キャッシュをクリア（テスト用）
        /// </summary>
        public static void ClearCache()
        {
            poolTypeCache.Clear();
            constructorCache.Clear();
            cacheHits = 0;
            cacheMisses = 0;
            SurfaceImpactFeedback.LogInfo("EffectPoolFactory - キャッシュをクリアしました", SurfaceImpactFeedbackLogCategory.Cache);
        }
    }
} 