using UnityEngine;
using System;

namespace SCLib_SurfaceImpactFeedback.TextureStrategy
{
    /// <summary>
    /// テクスチャとそのアルファ値（重み）を格納する読み取り専用構造体
    /// 
    /// Terrainの複数テクスチャブレンディングや、
    /// メッシュのテクスチャ情報を表現するために使用されます。
    /// 
    /// IEquatable&lt;T&gt;を実装し、効率的な比較とハッシュ化をサポートします。
    /// </summary>
    public readonly struct TextureAlpha : IEquatable<TextureAlpha>
    {
        /// <summary>
        /// テクスチャのアルファ値（0.0-1.0の範囲の重み）
        /// </summary>
        public float Alpha { get; }
        
        /// <summary>
        /// 対象のテクスチャオブジェクト
        /// </summary>
        public Texture Texture { get; }

        /// <summary>
        /// TextureAlphaの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="alpha">テクスチャのアルファ値（重み）</param>
        /// <param name="texture">対象のテクスチャ</param>
        public TextureAlpha(float alpha, Texture texture)
        {
            Alpha = alpha;
            Texture = texture;
        }

        /// <summary>
        /// 他のTextureAlphaインスタンスとの等価性を判定します
        /// </summary>
        /// <param name="other">比較対象のTextureAlpha</param>
        /// <returns>等価の場合true</returns>
        public bool Equals(TextureAlpha other) => Alpha == other.Alpha && Equals(Texture, other.Texture);
        
        /// <summary>
        /// オブジェクトとの等価性を判定します
        /// </summary>
        /// <param name="obj">比較対象のオブジェクト</param>
        /// <returns>等価の場合true</returns>
        public override bool Equals(object obj) => obj is TextureAlpha other && Equals(other);
        
        /// <summary>
        /// ハッシュコードを取得します
        /// </summary>
        /// <returns>ハッシュコード</returns>
        public override int GetHashCode() => HashCode.Combine(Alpha, Texture?.GetHashCode() ?? 0);
        
        /// <summary>
        /// 等価演算子
        /// </summary>
        public static bool operator ==(TextureAlpha left, TextureAlpha right) => left.Equals(right);
        
        /// <summary>
        /// 非等価演算子
        /// </summary>
        public static bool operator !=(TextureAlpha left, TextureAlpha right) => !(left == right);
    }
} 