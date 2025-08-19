using System;

using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// シンプルなスケールフェードアウト機能を持つデカール
    /// IEffectインターフェースを実装し、他のエフェクトシステムとの互換性を提供
    /// </summary>
    public class SimpleDecal : MonoBehaviour, IEffect
    {
        [SerializeField] float visibleDuration = 3f;
        [SerializeField] float fadeDuration = 2f;

        Vector3 initialScale;

        void Awake()
        {
            initialScale = transform.localScale;
        }

        void OnEnable()
        {
            transform.localScale = initialScale;
        }

        /// <summary>
        /// エフェクトを再生する（IEffectインターフェース実装）
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト再生のタスク</returns>
        public async UniTask PlayEffect(CancellationToken ct)
        {
            await FadeOutDecal(ct);
        }

        /// <summary>
        /// デカールのフェードアウト処理
        /// 指定時間表示後、スケールをゼロにしてフェードアウトする
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>フェードアウト処理のタスク</returns>
        public async UniTask FadeOutDecal(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(visibleDuration), cancellationToken: ct);
            await LMotion.Create(transform.localScale, Vector3.zero, fadeDuration)
                .BindToLocalScale(transform)
                .AddTo(gameObject)
                .ToUniTask(ct);
        }
    }
}
