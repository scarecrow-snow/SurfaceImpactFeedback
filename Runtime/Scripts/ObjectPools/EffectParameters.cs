using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// エフェクト再生に必要なパラメータを格納する構造体
    /// エフェクトの位置、向き、回転オフセット、親オブジェクトを管理
    /// </summary>
    public struct EffectParameters
    {
        /// <summary>
        /// エフェクトの発生位置（世界座標）
        /// </summary>
        public Vector3 Position;
        
        /// <summary>
        /// エフェクトの向き（forward方向）
        /// </summary>
        public Vector3 Forward;
        
        /// <summary>
        /// エフェクトの回転オフセット
        /// </summary>
        public Vector3 Offset;
        
        /// <summary>
        /// エフェクトの親となるTransform
        /// </summary>
        public Transform Parent;
        
        /// <summary>
        /// エフェクトパラメータのコンストラクタ
        /// </summary>
        /// <param name="position">エフェクト発生位置</param>
        /// <param name="forward">エフェクトの向き</param>
        /// <param name="offset">回転オフセット</param>
        /// <param name="parent">親Transform</param>
        public EffectParameters(Vector3 position, Vector3 forward, Vector3 offset, Transform parent)
        {
            Position = position;
            Forward = forward;
            Offset = offset;
            Parent = parent;
        }
    }
}