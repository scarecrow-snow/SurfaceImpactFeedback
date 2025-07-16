using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.Pool;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// エフェクトオブジェクトプールの基底クラス
    /// 共通処理を抽象化し、各エフェクトタイプの実装の重複を削減する
    /// IDisposableを実装してメモリリークを防止
    /// </summary>
    /// <typeparam name="T">プールするオブジェクトのコンポーネント型</typeparam>
    public abstract class EffectObjectPoolBase<T> : IEffectObjectPool where T : Component
    {
        protected readonly Transform parentTransform;
        /// <summary>
        /// エフェクトプレハブ
        /// </summary>
        protected readonly GameObject effectPrefab;
        
        /// <summary>
        /// オブジェクトプール
        /// </summary>
        protected readonly ObjectPool<T> objectPool;
        
        /// <summary>
        /// Dispose済みかどうかのフラグ
        /// </summary>
        protected bool disposed = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="effectPrefab">エフェクトプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        /// <exception cref="ArgumentNullException">effectPrefabがnullの場合</exception>
        protected EffectObjectPoolBase(Transform parentTransform,GameObject effectPrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
        {
            this.parentTransform = parentTransform;
            this.effectPrefab = effectPrefab ?? throw new ArgumentNullException(nameof(effectPrefab));

            // プレハブに必要なコンポーネントが存在するかチェック
            if (!effectPrefab.TryGetComponent<T>(out _))
            {
                throw new ArgumentException($"プレハブ '{effectPrefab.name}' に {typeof(T).Name} コンポーネントが見つかりません");
            }

            // ObjectPoolの初期化
            objectPool = new ObjectPool<T>(
                createFunc: CreateObject,
                actionOnGet: OnGetObject,
                actionOnRelease: OnReleaseObject,
                actionOnDestroy: DestroyObject,
                collectionCheck: collectionCheck,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        /// <summary>
        /// エフェクトを再生する
        /// </summary>
        /// <param name="parameters">エフェクトパラメータ</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト再生のタスク</returns>
        public virtual async UniTaskVoid PlayEffect(EffectParameters parameters, CancellationToken ct)
        {
            T effectObject = null;

            try
            {
                // プールからオブジェクトを取得
                effectObject = objectPool.Get();

                // オブジェクトの位置と向きを設定
                SetupTransform(effectObject, parameters);

                // 具体的なエフェクト処理を実行
                await PlayEffectCore(effectObject, ct);
                

            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合はログを出力
                SurfaceImpactFeedback.LogDebug($"エフェクト再生がキャンセルされました: {typeof(T).Name}", SurfaceImpactFeedbackLogCategory.Performance);
            }
            catch (Exception ex)
            {
                // その他のエラーをログに出力
                SurfaceImpactFeedback.LogError($"エフェクト再生中にエラーが発生しました: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
            }
            finally
            {
                // オブジェクトをプールに戻す
                if (effectObject != null)
                {
                    try
                    {
                        effectObject.transform.SetParent(parentTransform);
                        effectObject.gameObject.SetActive(false);
                        objectPool.Release(effectObject);
                    }
                    catch (Exception ex)
                    {
                        SurfaceImpactFeedback.LogError($"オブジェクトプールへの返却中にエラーが発生しました: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
                    }
                }
            }
        }

        /// <summary>
        /// オブジェクトを生成する（派生クラスでオーバーライド可能）
        /// </summary>
        /// <returns>生成されたオブジェクト</returns>
        protected virtual T CreateObject()
        {
            var obj = GameObject.Instantiate(effectPrefab).GetComponent<T>();
            obj.gameObject.SetActive(false);
            return obj;
        }

        /// <summary>
        /// オブジェクトを取得する際の処理（派生クラスでオーバーライド可能）
        /// </summary>
        /// <param name="obj">取得したオブジェクト</param>
        protected virtual void OnGetObject(T obj)
        {
            obj.gameObject.SetActive(true);
        }

        /// <summary>
        /// オブジェクトをプールに返す際の処理（派生クラスでオーバーライド可能）
        /// </summary>
        /// <param name="obj">返却するオブジェクト</param>
        protected virtual void OnReleaseObject(T obj)
        {
            obj.gameObject.SetActive(false);
        }

        /// <summary>
        /// オブジェクトを破棄する際の処理（派生クラスでオーバーライド可能）
        /// </summary>
        /// <param name="obj">破棄するオブジェクト</param>
        protected virtual void DestroyObject(T obj)
        {
            if (obj != null)
            {
                GameObject.Destroy(obj.gameObject);
            }
        }

        /// <summary>
        /// エフェクトオブジェクトの位置と向きを設定する
        /// </summary>
        /// <param name="effectObject">エフェクトオブジェクト</param>
        /// <param name="parameters">エフェクトパラメータ</param>
        protected virtual void SetupTransform(T effectObject, EffectParameters parameters)
        {
            var transform = effectObject.transform;
            transform.position = parameters.Position;
            transform.forward = parameters.Forward;
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + parameters.Offset);
            transform.SetParent(parentTransform);
        }

        /// <summary>
        /// 具体的なエフェクト処理を実行する（派生クラスで実装）
        /// </summary>
        /// <param name="effectObject">エフェクトオブジェクト</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト処理のタスク</returns>
        protected abstract UniTask PlayEffectCore(T effectObject, CancellationToken ct);

        /// <summary>
        /// リソースを破棄する
        /// UnityのObjectPoolとプール内のオブジェクトを適切に破棄
        /// </summary>
        public virtual void Dispose()
        {
            if (disposed) return;
            
            try
            {
                // ObjectPoolの破棄 - プール内のすべてのオブジェクトを破棄
                objectPool?.Dispose();
                SurfaceImpactFeedback.LogInfo($"{GetType().Name} - プール破棄完了: {effectPrefab?.name}", SurfaceImpactFeedbackLogCategory.Pool);
            }
            catch (Exception ex)
            {
                SurfaceImpactFeedback.LogError($"{GetType().Name} - プール破棄中にエラーが発生: {ex.Message}", SurfaceImpactFeedbackLogCategory.Pool);
            }
            finally
            {
                disposed = true;
            }
        }

        /// <summary>
        /// ファイナライザ - Disposeの呼び忘れ防止
        /// </summary>
        ~EffectObjectPoolBase()
        {
            if (!disposed)
            {
                SurfaceImpactFeedback.LogWarning($"{GetType().Name} - Dispose()が呼ばれずにファイナライザが実行されました。メモリリークの可能性があります。", SurfaceImpactFeedbackLogCategory.Pool);
                Dispose();
            }
        }
    }
}