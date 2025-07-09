using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// 汎用エフェクト用のオブジェクトプール
    /// IEffectインターフェースを実装した任意のエフェクトコンポーネントに対応
    /// SimpleDecal、URP Decal Projector、HDRP Decal、カスタムエフェクト等を統一的に扱える
    /// </summary>
    /// <typeparam name="T">IEffectを実装したComponentの型</typeparam>
    public class EffectObjectPool<T> : EffectObjectPoolBase<T> where T : Component, IEffect
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="decalPrefab">エフェクトプレハブ</param>
        /// <param name="collectionCheck">重複チェックを行うか</param>
        /// <param name="defaultCapacity">初期プールサイズ</param>
        /// <param name="maxSize">最大プールサイズ</param>
        /// <exception cref="ArgumentException">プレハブにIEffectを実装したコンポーネントが存在しない場合</exception>
        public EffectObjectPool(GameObject decalPrefab, bool collectionCheck = true, int defaultCapacity = 30, int maxSize = 50)
            : base(decalPrefab, collectionCheck, defaultCapacity, maxSize)
        {
            // IEffectインターフェースの実装確認
            if (!decalPrefab.TryGetComponent<IEffect>(out _))
            {
                throw new ArgumentException($"プレハブ '{decalPrefab.name}' に IEffect を実装したコンポーネントが見つかりません");
            }
        }

        /// <summary>
        /// エフェクトの再生処理
        /// IEffectインターフェースを通じて統一的な方法でエフェクトを再生
        /// </summary>
        /// <param name="effect">IEffectを実装したエフェクトオブジェクト</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト再生のタスク</returns>
        protected override async UniTask PlayEffectCore(T effect, CancellationToken ct)
        {
            // IEffectインターフェースを通じてエフェクトを再生
            await effect.PlayEffect(ct);
        }
    }
}