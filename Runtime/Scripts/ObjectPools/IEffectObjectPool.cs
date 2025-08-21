using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// エフェクトオブジェクトプールのインターフェース
    /// 異なるエフェクトタイプに対する統一的な操作を提供する
    /// メモリリーク防止のためIDisposableを実装
    /// </summary>
    public interface IEffectObjectPool : IDisposable
    {
        /// <summary>
        /// エフェクトを再生する
        /// </summary>
        /// <param name="parameters">エフェクトパラメータ</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>エフェクト再生のタスク</returns>
        UniTaskVoid PlayEffect(EffectParameters parameters, CancellationToken ct);
    }
}