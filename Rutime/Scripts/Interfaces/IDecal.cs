using System.Threading;
using Cysharp.Threading.Tasks;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// デカールエフェクトの統一インターフェース
    /// 異なるデカール実装（SimpleDecal、URP Decal Projector、HDRP Decal等）を
    /// 統一的に扱うための抽象化を提供する
    /// </summary>
    public interface IDecal
    {
        /// <summary>
        /// デカールエフェクトを再生する
        /// 各デカール実装に応じたエフェクト処理（フェードアウト、アニメーション等）を実行
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>デカールエフェクト再生のタスク</returns>
        UniTask PlayDecalEffect(CancellationToken ct);
    }
}