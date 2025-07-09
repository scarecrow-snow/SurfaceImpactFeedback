using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// 汎用デカールエフェクト用のオブジェクトプール
    /// IDecalインターフェースを実装した任意のデカールコンポーネントに対応
    /// SimpleDecal、URP Decal Projector、HDRP Decal、カスタムデカール等を統一的に扱える
    /// </summary>
    /// <typeparam name="T">IDecalを実装したComponentの型</typeparam>
    public class DecalObjectPool<T> : EffectObjectPoolBase<T> where T : Component, IDecal
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="decalPrefab">デカールプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        /// <exception cref="ArgumentException">プレハブにIDecalを実装したコンポーネントが存在しない場合</exception>
        public DecalObjectPool(GameObject decalPrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
            : base(decalPrefab, collectionCheck, defaultCapacity, maxSize)
        {
            // IDecalインターフェースの実装確認
            if (!decalPrefab.TryGetComponent<IDecal>(out _))
            {
                throw new ArgumentException($"プレハブ '{decalPrefab.name}' に IDecal を実装したコンポーネントが見つかりません");
            }
        }

        /// <summary>
        /// デカールエフェクトの再生処理
        /// IDecalインターフェースを通じて統一的な方法でエフェクトを再生
        /// </summary>
        /// <param name="decal">IDecalを実装したデカールオブジェクト</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>デカール再生のタスク</returns>
        protected override async UniTask PlayEffectCore(T decal, CancellationToken ct)
        {
            // IDecalインターフェースを通じてエフェクトを再生
            await decal.PlayDecalEffect(ct);
        }
    }
}