using System.Collections.Generic;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// SurfaceImpactFeedbackシステムの設定を管理するScriptableObject
    /// Pure C#クラス化に伴い、全ての設定項目を外部化
    /// </summary>
    [CreateAssetMenu(menuName = "Surface Impact Feedback/Configuration", fileName = "SurfaceImpactConfiguration")]
    public class SurfaceImpactConfiguration : ScriptableObject
    {
        [Header("サーフェス設定")]
        [Tooltip("登録されたサーフェスタイプのリスト")]
        public List<SurfaceType> surfaces = new();
        
        [Tooltip("マッチするサーフェスタイプが見つからない場合に使用されるデフォルトサーフェス")]
        public Surface defaultSurface;

        [Header("システム設定")]
        [Tooltip("Renderer検索時に除外するレイヤーマスク（UIやエフェクト専用オブジェクトなど）")]
        public LayerMask excludedLayers = 0;
        
        [Header("ログ設定")]
        [Tooltip("ログ制御設定用ScriptableObject")]
        public SurfaceManagerLogSettings logSettings;

        /// <summary>
        /// 設定の妥当性をチェックする
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        public bool ValidateConfiguration()
        {
            // デフォルトサーフェスが設定されているかチェック
            if (defaultSurface == null)
            {
                Debug.LogError($"[SurfaceImpactConfiguration] デフォルトサーフェスが設定されていません: {name}");
                return false;
            }

            // サーフェス設定の妥当性チェック
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surfaceType = surfaces[i];
                if (surfaceType.Albedo == null)
                {
                    Debug.LogWarning($"[SurfaceImpactConfiguration] サーフェス{i}のAlbedoテクスチャがnullです: {name}");
                }
                if (surfaceType.Surface == null)
                {
                    Debug.LogWarning($"[SurfaceImpactConfiguration] サーフェス{i}のSurfaceがnullです: {name}");
                }
            }

            // ログ設定の妥当性チェック
            if (logSettings != null && !logSettings.ValidateSettings())
            {
                Debug.LogWarning($"[SurfaceImpactConfiguration] ログ設定が無効です: {name}");
            }

            return true;
        }

        /// <summary>
        /// 設定を文字列として取得（デバッグ用）
        /// </summary>
        /// <returns>設定の詳細情報</returns>
        public override string ToString()
        {
            return $"SurfaceImpactConfiguration:\n" +
                   $"  Surfaces: {surfaces?.Count ?? 0} 個\n" +
                   $"  DefaultSurface: {(defaultSurface != null ? defaultSurface.name : "null")}\n" +
                   $"  ExcludedLayers: {excludedLayers}\n" +
                   $"  LogSettings: {(logSettings != null ? logSettings.name : "null")}";
        }

        /// <summary>
        /// デフォルト設定を作成する
        /// </summary>
        /// <param name="environment">環境名（Development, Production, Testing）</param>
        /// <returns>環境に適した設定</returns>
        public static SurfaceImpactConfiguration CreateDefaultConfiguration(string environment = "Development")
        {
            var config = CreateInstance<SurfaceImpactConfiguration>();
            config.name = $"SurfaceImpactConfiguration_{environment}";
            
            // 環境別の設定
            switch (environment.ToLower())
            {
                case "development":
                    config.excludedLayers = LayerMask.GetMask("UI", "Debug");
                    config.logSettings = SurfaceManagerLogSettings.CreateDefaultSettings("Development");
                    break;
                    
                case "production":
                    config.excludedLayers = LayerMask.GetMask("UI");
                    config.logSettings = SurfaceManagerLogSettings.CreateDefaultSettings("Production");
                    break;
                    
                case "testing":
                    config.excludedLayers = LayerMask.GetMask("UI", "Debug", "Test");
                    config.logSettings = SurfaceManagerLogSettings.CreateDefaultSettings("Testing");
                    break;
                    
                default:
                    config.excludedLayers = LayerMask.GetMask("UI");
                    break;
            }
            
            return config;
        }

        /// <summary>
        /// 設定をコピーする
        /// </summary>
        /// <param name="source">コピー元の設定</param>
        /// <returns>コピーされた設定</returns>
        public static SurfaceImpactConfiguration CopyFrom(SurfaceImpactConfiguration source)
        {
            if (source == null) return null;

            var config = CreateInstance<SurfaceImpactConfiguration>();
            config.name = source.name + "_Copy";
            config.surfaces = new List<SurfaceType>(source.surfaces);
            config.defaultSurface = source.defaultSurface;
            config.excludedLayers = source.excludedLayers;
            config.logSettings = source.logSettings;

            return config;
        }
    }
}