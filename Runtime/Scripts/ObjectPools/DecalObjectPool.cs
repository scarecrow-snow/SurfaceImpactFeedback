using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using SCLib_SurfaceImpactFeedback.Utilities;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// 親オブジェクトのライフサイクルに対応したデカール用オブジェクトプール
    /// 動的な親オブジェクト設定と親の破棄監視により、安全なデカール管理を実現
    /// IEffectインターフェースを実装した任意のデカールコンポーネントに対応
    /// SimpleDecal、URP Decal Projector、HDRP Decal、カスタムデカール等を統一的に扱える
    /// </summary>
    /// <typeparam name="T">IEffectを実装したデカールComponentの型</typeparam>
    public class DynamicParentDecalObjectPool<T> : EffectObjectPoolBase<T> where T : Component, IEffect
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="decalPrefab">デカールプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        /// <exception cref="ArgumentException">プレハブにIEffectを実装したコンポーネントが存在しない場合</exception>
        public DynamicParentDecalObjectPool(Transform parentTransform, GameObject decalPrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
            : base(parentTransform, decalPrefab, collectionCheck, defaultCapacity, maxSize)
        {
            // IEffectインターフェースの実装確認
            if (!decalPrefab.TryGetComponent<IEffect>(out _))
            {
                throw new ArgumentException($"プレハブ '{decalPrefab.name}' に IEffect を実装したコンポーネントが見つかりません");
            }
        }

        /// <summary>
        /// デカールエフェクトの再生処理
        /// IEffectインターフェースを通じて統一的な方法でデカールを再生
        /// </summary>
        /// <param name="effect">IEffectを実装したデカールオブジェクト</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>デカール再生のタスク</returns>
        protected override async UniTask PlayEffectCore(T effect, CancellationToken ct)
        {
            // IEffectインターフェースを通じてエフェクトを再生
            await effect.PlayEffect(ct);
        }

        /// <summary>
        /// 親オブジェクトのライフサイクルに対応したデカールエフェクト再生
        /// 親オブジェクトが破棄された場合、エフェクトを安全に中断する
        /// </summary>
        /// <param name="parameters">エフェクトパラメータ（親オブジェクト含む）</param>
        /// <param name="ct">キャンセレーショントークン</param>
        public override async UniTaskVoid PlayEffect(EffectParameters parameters, CancellationToken ct)
        {
            T effectObject = null;

            try
            {
                // プールからオブジェクトを取得
                effectObject = objectPool.Get();

                // オブジェクトの位置と向きを設定
                SetupTransform(effectObject, parameters);

                // 親オブジェクトが存在する場合は、そのライフサイクルを監視（パフォーマンス最適化版）
                if (parameters.Parent != null && parameters.Parent.gameObject != null)
                {
                    var destroyTask = parameters.Parent.OnDestroyAsync();
                    var effectTask = PlayEffectCore(effectObject, ct);
                    
                    // 親の破棄とエフェクト完了のどちらかが先に終了するまで待機
                    await UniTask.WhenAny(destroyTask, effectTask);
                }
                else
                {
                    // 親オブジェクトが設定されていない場合は通常の再生
                    await PlayEffectCore(effectObject, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合はログを出力
                SurfaceImpactLogger.LogDebug($"デカールエフェクト再生がキャンセルされました: {typeof(T).Name}", SurfaceImpactFeedbackLogCategory.Performance);
            }
            catch (Exception ex)
            {
                // その他のエラーをログに出力（詳細なスタックトレース付き）
                SurfaceImpactLogger.LogError($"デカールエフェクト再生中にエラーが発生しました: {ex.Message}\nスタックトレース: {ex.StackTrace}\n内部例外: {ex.InnerException?.Message}", SurfaceImpactFeedbackLogCategory.Pool);
            }
            finally
            {
                // オブジェクトをプールに戻す
                if (effectObject != null)
                {
                    try
                    {
                        if (parentTransform != null && !parentTransform.Equals(null))
                        {
                            effectObject.transform.SetParent(parentTransform);
                        }
                        else
                        {
                            effectObject.transform.SetParent(null);
                        }
                        
                        effectObject.gameObject.SetActive(false);
                        objectPool.Release(effectObject);
                    }
                    catch (Exception ex)
                    {
                        SurfaceImpactLogger.LogError($"オブジェクトプールへの返却中にエラーが発生しました: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
                        GameObject.Destroy(effectObject.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// デカールオブジェクトのトランスフォーム設定
        /// 動的な親オブジェクト設定に対応
        /// </summary>
        /// <param name="effectObject">設定対象のデカールオブジェクト</param>
        /// <param name="parameters">エフェクトパラメータ</param>
        protected override void SetupTransform(T effectObject, EffectParameters parameters)
        {
            if (effectObject == null || effectObject.gameObject == null)
            {
                SurfaceImpactLogger.LogWarning("SetupTransform: effectObjectがnullまたは無効です", SurfaceImpactFeedbackLogCategory.Pool);
                return;
            }

            var transform = effectObject.transform;
            
            // 位置と向きを設定
            transform.position = parameters.Position;
            transform.forward = parameters.Forward;
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + parameters.Offset);
            
            // 親オブジェクトを設定（nullの場合はシステム親を使用）
            var targetParent = parameters.Parent != null && parameters.Parent.gameObject != null 
                ? parameters.Parent 
                : parentTransform;
                
            // 親がnullの場合も安全に処理
            if (targetParent != null && !targetParent.Equals(null))
            {
                transform.SetParent(targetParent);
            }
            else
            {
                transform.SetParent(null);
            }
            
            SurfaceImpactLogger.LogDebug($"デカールオブジェクトを親'{targetParent?.name}'に設定しました", SurfaceImpactFeedbackLogCategory.Pool);
        }

    }
    
    /// <summary>
    /// 後方互換性のためのDecalObjectPoolエイリアス
    /// 従来のコードとの互換性を保ちつつ、新機能への移行を容易にする
    /// </summary>
    /// <typeparam name="T">IEffectを実装したデカールComponentの型</typeparam>
    public class DecalObjectPool<T> : DynamicParentDecalObjectPool<T> where T : Component, IEffect
    {
        /// <summary>
        /// 後方互換性コンストラクタ
        /// </summary>
        public DecalObjectPool(Transform parentTransform, GameObject decalPrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
            : base(parentTransform, decalPrefab, collectionCheck, defaultCapacity, maxSize)
        {
        }
    }
}