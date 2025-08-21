using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// パーティクルエフェクト用のオブジェクトプール
    /// パーティクルシステムの再利用によりパフォーマンスを最適化
    /// </summary>
    public class ParticleObjectPool : EffectObjectPoolBase<ParticleSystem>
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="particlePrefab">パーティクルプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        public ParticleObjectPool(Transform parentTransform, GameObject particlePrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
            : base(parentTransform,particlePrefab, collectionCheck, defaultCapacity, maxSize)
        {
        }

        /// <summary>
        /// パーティクルをプールに返す際の処理
        /// パーティクルを停止してから基底クラスの処理を実行
        /// </summary>
        /// <param name="particle">返却するパーティクル</param>
        protected override void OnReleaseObject(ParticleSystem particle)
        {
            particle.Stop();
            base.OnReleaseObject(particle);
        }

        /// <summary>
        /// パーティクルエフェクトの再生処理
        /// </summary>
        /// <param name="particle">パーティクルシステム</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>パーティクル再生のタスク</returns>
        protected override async UniTask PlayEffectCore(ParticleSystem particle, CancellationToken ct)
        {
            // パーティクルを再生
            particle.Play();
            
            // 1フレーム待機
            await UniTask.Yield(ct);
            
            // パーティクルが生きている間は待機
            await UniTask.WaitWhile(() => particle.IsAlive(true), cancellationToken: ct);
        }
    }
}