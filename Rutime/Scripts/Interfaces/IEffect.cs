using System.Threading;
using Cysharp.Threading.Tasks;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// エフェクトの統一インターフェース
    /// 異なるエフェクト実装（デカール、パーティクル、音声等）を
    /// 統一的に扱うための抽象化を提供する
    /// </summary>
    public interface IEffect
    {
        /// <summary>
        /// エフェクトを再生する
        /// 各エフェクト実装に応じた処理（フェードアウト、アニメーション、音声再生等）を実行
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト再生のタスク</returns>
        UniTask PlayEffect(CancellationToken ct);
    }
}