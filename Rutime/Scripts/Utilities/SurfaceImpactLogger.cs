using UnityEngine;


namespace SCLib_SurfaceImpactFeedback.Utilities
{
    /// <summary>
    /// SurfaceImpactFeedbackのログレベル設定
    /// 出力するログの重要度を制御
    /// </summary>
    public enum SurfaceImpactFeedbackLogLevel
    {
        None = 0,       // ログ出力なし
        Error = 1,      // エラーのみ
        Warning = 2,    // 警告以上
        Info = 3,       // 情報以上
        Debug = 4,      // デバッグ情報
        Verbose = 5     // 詳細ログ
    }

    /// <summary>
    /// SurfaceImpactFeedbackのログカテゴリ設定
    /// 出力するログの種類を制御（フラグ列挙型）
    /// </summary>
    [System.Flags]
    public enum SurfaceImpactFeedbackLogCategory
    {
        None = 0,
        Memory = 1 << 0,        // メモリ統計・監視
        Pool = 1 << 1,          // プール作成・破棄
        Cache = 1 << 2,         // キャッシュ操作
        Performance = 1 << 3,   // パフォーマンス関連
        Lifecycle = 1 << 4,     // ライフサイクル（初期化・破棄）
        All = ~0                // すべてのカテゴリ
    }

    /// <summary>
    /// SurfaceImpactFeedback専用のログ制御システム
    /// 責任分離による保守性とパフォーマンスの向上を実現
    /// </summary>
    public static class SurfaceImpactLogger
    {
        #region フィールド・プロパティ

        /// <summary>
        /// ログ制御: 現在のログレベル設定
        /// </summary>
        private static SurfaceImpactFeedbackLogLevel logLevel = SurfaceImpactFeedbackLogLevel.Warning;

        /// <summary>
        /// ログ制御: 現在のログカテゴリ設定
        /// </summary>
        private static SurfaceImpactFeedbackLogCategory logCategories = SurfaceImpactFeedbackLogCategory.All;

        /// <summary>
        /// 現在使用中のログ設定（実行時制御用）
        /// </summary>
        private static SurfaceManagerLogSettings currentLogSettings;

        /// <summary>
        /// メモリ監視用: エフェクト処理回数の統計
        /// </summary>
        private static int totalEffectCount = 0;
        private static int poolCreationCount = 0;

        #endregion

        #region 初期化・設定管理

        /// <summary>
        /// ログ設定を初期化・適用する
        /// ScriptableObjectから設定を読み込み、静的変数に設定
        /// </summary>
        /// <param name="logSettings">使用するログ設定</param>
        public static void Initialize(SurfaceManagerLogSettings logSettings = null)
        {
            try
            {
                // ScriptableObjectが設定されている場合は読み込み
                if (logSettings != null)
                {
                    // 設定の妥当性をチェック
                    if (!logSettings.ValidateSettings())
                    {
                        Debug.LogError("[SurfaceImpactLogger] ログ設定が無効です。デフォルト設定を使用します。");
                        ApplyDefaultLogSettings();
                        return;
                    }

                    // 設定を適用
                    ApplyLogSettings(logSettings);
                    LogInfo($"ログ設定を読み込みました: {logSettings.name}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                    LogDebug($"設定詳細:\n{logSettings.ToString()}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                }
                else
                {
                    // ScriptableObjectが設定されていない場合はデフォルト設定
                    ApplyDefaultLogSettings();
                    LogInfo("ログ設定が指定されていません。デフォルト設定を使用します。", SurfaceImpactFeedbackLogCategory.Lifecycle);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SurfaceImpactLogger] ログ設定の初期化中にエラーが発生: {ex.Message}");
                ApplyDefaultLogSettings();
            }
        }

        /// <summary>
        /// 指定されたログ設定を適用する
        /// </summary>
        /// <param name="settings">適用するログ設定</param>
        private static void ApplyLogSettings(SurfaceManagerLogSettings settings)
        {
            if (settings == null) return;

            currentLogSettings = settings;
            logLevel = settings.logLevel;
            logCategories = settings.logCategories;

            // パフォーマンス監視設定も更新
            if (settings.enableMemoryCheck)
            {
                // メモリチェック間隔の更新は実際のチェック処理で使用
            }
        }

        /// <summary>
        /// デフォルトのログ設定を適用する
        /// </summary>
        private static void ApplyDefaultLogSettings()
        {
            currentLogSettings = null;
            logLevel = SurfaceImpactFeedbackLogLevel.Warning;
            logCategories = SurfaceImpactFeedbackLogCategory.All;
        }

        #endregion

        #region ログレベル・カテゴリ制御

        /// <summary>
        /// ログレベルを設定する
        /// </summary>
        /// <param name="level">設定するログレベル</param>
        /// <param name="force">強制的に設定するか（実行時制御設定を無視）</param>
        public static void SetLogLevel(SurfaceImpactFeedbackLogLevel level, bool force = false)
        {
            // 実行時制御が無効で、強制フラグが立っていない場合は設定を拒否
            if (!force && currentLogSettings != null && !currentLogSettings.enableRuntimeLogLevelChange)
            {
                LogWarning($"実行時のログレベル変更が無効に設定されています。現在: {logLevel}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                return;
            }

            var oldLevel = logLevel;
            logLevel = level;
            LogInfo($"ログレベルを変更しました: {oldLevel} → {level}", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }

        /// <summary>
        /// ログカテゴリを設定する
        /// </summary>
        /// <param name="categories">設定するログカテゴリ（フラグ列挙型）</param>
        /// <param name="force">強制的に設定するか（実行時制御設定を無視）</param>
        public static void SetLogCategories(SurfaceImpactFeedbackLogCategory categories, bool force = false)
        {
            // 実行時制御が無効で、強制フラグが立っていない場合は設定を拒否
            if (!force && currentLogSettings != null && !currentLogSettings.enableRuntimeCategoryChange)
            {
                LogWarning($"実行時のログカテゴリ変更が無効に設定されています。現在: {logCategories}", SurfaceImpactFeedbackLogCategory.Lifecycle);
                return;
            }

            var oldCategories = logCategories;
            logCategories = categories;
            LogInfo($"ログカテゴリを変更しました: {oldCategories} → {categories}", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }

        /// <summary>
        /// ログ設定をScriptableObjectから再読み込みする
        /// </summary>
        /// <param name="newSettings">新しいログ設定（nullの場合は現在の設定を再読み込み）</param>
        public static void ReloadLogSettings(SurfaceManagerLogSettings newSettings = null)
        {
            Initialize(newSettings);
        }

        /// <summary>
        /// 現在のログレベルを取得する
        /// </summary>
        /// <returns>現在のログレベル</returns>
        public static SurfaceImpactFeedbackLogLevel GetLogLevel()
        {
            return logLevel;
        }

        /// <summary>
        /// 現在のログカテゴリを取得する
        /// </summary>
        /// <returns>現在のログカテゴリ</returns>
        public static SurfaceImpactFeedbackLogCategory GetLogCategories()
        {
            return logCategories;
        }

        #endregion

        #region ログ出力メソッド

        /// <summary>
        /// 指定されたレベルとカテゴリのログが出力対象かチェック
        /// </summary>
        /// <param name="level">チェックするレベル</param>
        /// <param name="category">チェックするカテゴリ</param>
        /// <returns>出力対象の場合true</returns>
        private static bool ShouldLog(SurfaceImpactFeedbackLogLevel level, SurfaceImpactFeedbackLogCategory category)
        {
            // ログレベルが無効の場合は出力しない
            if (logLevel == SurfaceImpactFeedbackLogLevel.None) return false;

            // レベルチェック: 現在のレベル以下のみ出力
            if (level > logLevel) return false;

            // カテゴリチェック: 指定されたカテゴリが有効か
            if ((logCategories & category) == 0) return false;

            return true;
        }

        /// <summary>
        /// エラーログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Error, category))
            {
                Debug.LogError($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }

        /// <summary>
        /// 警告ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Warning, category))
            {
                Debug.LogWarning($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }

        /// <summary>
        /// 情報ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogInfo(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Info, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }

        /// <summary>
        /// デバッグログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogDebug(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Debug, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}] {message}");
            }
        }

        /// <summary>
        /// 詳細ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="category">ログカテゴリ</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogVerbose(string message, SurfaceImpactFeedbackLogCategory category = SurfaceImpactFeedbackLogCategory.All)
        {
            if (ShouldLog(SurfaceImpactFeedbackLogLevel.Verbose, category))
            {
                Debug.Log($"[SurfaceImpactFeedback:{category}:Verbose] {message}");
            }
        }

        #endregion

        #region メモリ監視・統計機能

        /// <summary>
        /// エフェクト処理カウントを増加
        /// </summary>
        public static void IncrementEffectCount()
        {
            totalEffectCount++;
        }

        /// <summary>
        /// プール作成カウントを増加
        /// </summary>
        public static void IncrementPoolCreationCount()
        {
            poolCreationCount++;
        }

        /// <summary>
        /// メモリ使用状況の統計情報を取得
        /// デバッグ・監視用
        /// </summary>
        /// <param name="poolCount">現在のプール数</param>
        /// <param name="textureMappingCount">テクスチャマッピング数</param>
        /// <param name="terrainCacheCount">Terrainキャッシュ数</param>
        /// <param name="rendererCacheCount">Rendererキャッシュ数</param>
        /// <returns>メモリ使用状況の詳細情報</returns>
        public static string GetMemoryStatistics(int poolCount = 0, int textureMappingCount = 0, int terrainCacheCount = 0, int rendererCacheCount = 0)
        {
            var factoryStats = EffectPoolFactory.GetCacheStatistics();

            return $"=== SurfaceImpactFeedback メモリ統計 ===\n" +
                   $"総エフェクト処理回数: {totalEffectCount}\n" +
                   $"作成されたプール数: {poolCreationCount}\n" +
                   $"現在のプール数: {poolCount}\n" +
                   $"テクスチャマッピング数: {textureMappingCount}\n" +
                   $"Terrainキャッシュ数: {terrainCacheCount}\n" +
                   $"Rendererキャッシュ数: {rendererCacheCount}\n" +
                   $"{factoryStats}\n" +
                   $"現在のメモリ使用量: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024:F1} MB";
        }

        /// <summary>
        /// メモリ統計をデバッグログに出力
        /// 開発環境でのみ実行される
        /// </summary>
        /// <param name="poolCount">現在のプール数</param>
        /// <param name="textureMappingCount">テクスチャマッピング数</param>
        /// <param name="terrainCacheCount">Terrainキャッシュ数</param>
        /// <param name="rendererCacheCount">Rendererキャッシュ数</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogMemoryStatistics(int poolCount = 0, int textureMappingCount = 0, int terrainCacheCount = 0, int rendererCacheCount = 0)
        {
            LogDebug(GetMemoryStatistics(poolCount, textureMappingCount, terrainCacheCount, rendererCacheCount), SurfaceImpactFeedbackLogCategory.Memory);
            EffectPoolFactory.LogCacheStatistics();
        }

        /// <summary>
        /// 定期的なメモリ監視機能
        /// 大量エフェクト使用時のメモリリーク早期発見用
        /// </summary>
        /// <param name="poolCount">現在のプール数</param>
        /// <param name="textureMappingCount">テクスチャマッピング数</param>
        /// <param name="terrainCacheCount">Terrainキャッシュ数</param>
        /// <param name="rendererCacheCount">Rendererキャッシュ数</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void PerformMemoryCheck(int poolCount = 0, int textureMappingCount = 0, int terrainCacheCount = 0, int rendererCacheCount = 0)
        {
            // ログ設定でメモリチェックが無効の場合は処理をスキップ
            if (currentLogSettings != null && !currentLogSettings.enableMemoryCheck)
            {
                return;
            }

            // チェック間隔を設定から取得（デフォルトは1000）
            int checkInterval = currentLogSettings?.memoryCheckInterval ?? 1000;
            long warningThreshold = currentLogSettings?.memoryWarningThreshold ?? 500;

            if (totalEffectCount > 0 && totalEffectCount % checkInterval == 0)
            {
                LogWarning($"メモリチェック: {totalEffectCount} 回のエフェクト処理が完了", SurfaceImpactFeedbackLogCategory.Memory);

                // キャッシュ統計ログが有効な場合のみ出力
                if (currentLogSettings == null || currentLogSettings.enableCacheStatistics)
                {
                    LogMemoryStatistics(poolCount, textureMappingCount, terrainCacheCount, rendererCacheCount);
                }

                // メモリ使用量が異常に多い場合の警告
                long memoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
                if (memoryMB > warningThreshold)
                {
                    LogError($"警告: メモリ使用量が異常に多くなっています ({memoryMB} MB / しきい値: {warningThreshold} MB)", SurfaceImpactFeedbackLogCategory.Memory);
                }
            }
        }

        /// <summary>
        /// ログシステムのリセット（テスト・デバッグ用）
        /// </summary>
        public static void Reset()
        {
            totalEffectCount = 0;
            poolCreationCount = 0;
            ApplyDefaultLogSettings();
            LogInfo("ログシステムをリセットしました", SurfaceImpactFeedbackLogCategory.Lifecycle);
        }

        #endregion
    }
}