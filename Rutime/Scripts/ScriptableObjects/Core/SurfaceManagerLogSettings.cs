using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// SurfaceImpactFeedbackのログ制御設定用ScriptableObject
    /// プロジェクト設定として管理し、ビルド環境別の制御を可能にする
    /// </summary>
    [CreateAssetMenu(menuName = "Surface Impact Feedback/Log Settings", fileName = "SurfaceManagerLogSettings")]
    public class SurfaceManagerLogSettings : ScriptableObject
    {
        [Header("基本ログ設定")]
        [Tooltip("出力するログの最小レベル")]
        public SurfaceImpactFeedbackLogLevel logLevel = SurfaceImpactFeedbackLogLevel.Warning;
        
        [Tooltip("出力するログのカテゴリ（複数選択可能）")]
        public SurfaceImpactFeedbackLogCategory logCategories = SurfaceImpactFeedbackLogCategory.All;
        
        [Header("実行時制御")]
        [Tooltip("実行時にログレベルの変更を許可するか")]
        public bool enableRuntimeLogLevelChange = true;
        
        [Tooltip("実行時にログカテゴリの変更を許可するか")]
        public bool enableRuntimeCategoryChange = true;
        
        [Header("詳細設定")]
        [Tooltip("ログにタイムスタンプを含めるか")]
        public bool logTimestamp = false;
        
        [Tooltip("エラーログにスタックトレースを含めるか")]
        public bool logStackTrace = false;
        
        [Tooltip("ログ出力の前にプレフィックスを付けるか")]
        public bool useLogPrefix = true;
        
        [Header("パフォーマンス監視設定")]
        [Tooltip("メモリチェックを行うエフェクト処理回数の間隔")]
        [Range(100, 10000)]
        public int memoryCheckInterval = 1000;
        
        [Tooltip("メモリ使用量警告のしきい値（MB）")]
        [Range(100, 2000)]
        public long memoryWarningThreshold = 500;
        
        [Tooltip("メモリチェック機能を有効にするか")]
        public bool enableMemoryCheck = true;
        
        [Header("開発者設定")]
        [Tooltip("詳細ログ（Verbose）を有効にするか")]
        public bool enableVerboseLogging = false;
        
        [Tooltip("パフォーマンスログを有効にするか")]
        public bool enablePerformanceLogging = true;
        
        [Tooltip("キャッシュ統計ログを有効にするか")]
        public bool enableCacheStatistics = true;
        
        /// <summary>
        /// 設定の妥当性をチェックする
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        public bool ValidateSettings()
        {
            // ログレベルが有効範囲内かチェック
            if (logLevel < SurfaceImpactFeedbackLogLevel.None || logLevel > SurfaceImpactFeedbackLogLevel.Verbose)
            {
                Debug.LogError($"[SurfaceImpactFeedbackLogSettings] 無効なログレベル: {logLevel}");
                return false;
            }
            
            // メモリチェック間隔が有効範囲内かチェック
            if (memoryCheckInterval < 100 || memoryCheckInterval > 10000)
            {
                Debug.LogError($"[SurfaceImpactFeedbackLogSettings] 無効なメモリチェック間隔: {memoryCheckInterval}");
                return false;
            }
            
            // メモリ警告しきい値が有効範囲内かチェック
            if (memoryWarningThreshold < 100 || memoryWarningThreshold > 2000)
            {
                Debug.LogError($"[SurfaceImpactFeedbackLogSettings] 無効なメモリ警告しきい値: {memoryWarningThreshold}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 設定を文字列として取得（デバッグ用）
        /// </summary>
        /// <returns>設定の詳細情報</returns>
        public override string ToString()
        {
            return $"SurfaceImpactFeedbackLogSettings:\n" +
                   $"  LogLevel: {logLevel}\n" +
                   $"  LogCategories: {logCategories}\n" +
                   $"  RuntimeChange: Level={enableRuntimeLogLevelChange}, Category={enableRuntimeCategoryChange}\n" +
                   $"  Details: Timestamp={logTimestamp}, StackTrace={logStackTrace}, Prefix={useLogPrefix}\n" +
                   $"  Performance: MemoryCheck={enableMemoryCheck}, Interval={memoryCheckInterval}, Threshold={memoryWarningThreshold}MB\n" +
                   $"  Developer: Verbose={enableVerboseLogging}, Performance={enablePerformanceLogging}, Cache={enableCacheStatistics}";
        }
        
        /// <summary>
        /// デフォルト設定を作成する
        /// </summary>
        /// <param name="environment">環境名（Development, Production, Testing）</param>
        /// <returns>環境に適した設定</returns>
        public static SurfaceManagerLogSettings CreateDefaultSettings(string environment = "Development")
        {
            var settings = CreateInstance<SurfaceManagerLogSettings>();
            
            switch (environment.ToLower())
            {
                case "development":
                    settings.logLevel = SurfaceImpactFeedbackLogLevel.Debug;
                    settings.logCategories = SurfaceImpactFeedbackLogCategory.All;
                    settings.enableVerboseLogging = true;
                    settings.enableMemoryCheck = true;
                    settings.logTimestamp = true;
                    settings.logStackTrace = true;
                    break;
                    
                case "production":
                    settings.logLevel = SurfaceImpactFeedbackLogLevel.Error;
                    settings.logCategories = SurfaceImpactFeedbackLogCategory.None;
                    settings.enableRuntimeLogLevelChange = false;
                    settings.enableRuntimeCategoryChange = false;
                    settings.enableVerboseLogging = false;
                    settings.enableMemoryCheck = false;
                    settings.logTimestamp = false;
                    settings.logStackTrace = false;
                    break;
                    
                case "testing":
                    settings.logLevel = SurfaceImpactFeedbackLogLevel.Warning;
                    settings.logCategories = SurfaceImpactFeedbackLogCategory.Memory | SurfaceImpactFeedbackLogCategory.Performance;
                    settings.enableMemoryCheck = true;
                    settings.enablePerformanceLogging = true;
                    settings.logTimestamp = false;
                    settings.logStackTrace = false;
                    break;
                    
                default:
                    // デフォルトはWarningレベル
                    settings.logLevel = SurfaceImpactFeedbackLogLevel.Warning;
                    settings.logCategories = SurfaceImpactFeedbackLogCategory.All;
                    break;
            }
            
            return settings;
        }
    }
}