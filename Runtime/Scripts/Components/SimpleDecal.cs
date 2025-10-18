using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// シンプルなスケールフェードアウト機能を持つデカール
    /// IEffectインターフェースを実装し、他のエフェクトシステムとの互換性を提供
    /// LitMotionに依存せず、UniTaskベースの軽量なフェードアウトを実装
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
        /// 指定時間表示後、スケールを線形補間でゼロにしてフェードアウトする
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>フェードアウト処理のタスク</returns>
        public async UniTask FadeOutDecal(CancellationToken ct)
        {
            // 指定時間表示を維持
            await UniTask.Delay(TimeSpan.FromSeconds(visibleDuration), cancellationToken: ct);

            // フェードアウトアニメーション
            var startScale = transform.localScale;
            var elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                ct.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 最終的に確実にゼロにする
            transform.localScale = Vector3.zero;
        }
    }
}
