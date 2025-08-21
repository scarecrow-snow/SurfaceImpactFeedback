using UnityEngine;

namespace SCLib_SurfaceImpactFeedback
{
    /// <summary>
    /// シーン間で永続化されるシングルトンの基底クラス
    /// このクラスを継承することで、シーン切り替え時にも破棄されないシングルトンを作成できます
    /// DontDestroyOnLoadを自動的に適用し、重複インスタンスを防ぎます
    /// </summary>
    /// <typeparam name="T">シングルトンにする型。MonoBehaviourを継承している必要があります</typeparam>
    /// <example>
    /// 使用例:
    /// <code>
    /// public class GameManager : PersistentSingleton&lt;GameManager&gt; {
    ///     public void DoSomething() {
    ///         // ゲーム管理の処理
    ///     }
    /// }
    /// 
    /// // 他のクラスからの使用
    /// GameManager.Instance.DoSomething();
    /// </code>
    /// </example>
    public class PersistentSingleton<T> : MonoBehaviour where T : Component
    {
        /// <summary>
        /// Awake時にGameObjectを自動的に親から切り離すかどうか
        /// trueの場合、シーン階層の最上位に移動し、DontDestroyOnLoadが適用されます
        /// </summary>
        [Tooltip("Awake時にGameObjectを親から自動的に切り離すかどうか")]
        public bool AutoUnparentOnAwake = true;

        /// <summary>
        /// シングルトンインスタンスの実際の参照
        /// </summary>
        protected static T instance;

        /// <summary>
        /// インスタンスが存在するかどうかを確認します
        /// null チェックを安全に行うためのプロパティです
        /// </summary>
        /// <returns>インスタンスが存在する場合true、そうでなければfalse</returns>
        public static bool HasInstance => instance != null;

        /// <summary>
        /// インスタンスを安全に取得します
        /// インスタンスが存在しない場合はnullを返し、自動生成は行いません
        /// </summary>
        /// <returns>存在する場合はインスタンス、そうでなければnull</returns>
        public static T TryGetInstance() => HasInstance ? instance : null;

        /// <summary>
        /// シングルトンインスタンスを取得します
        /// インスタンスが存在しない場合は、シーン内から検索し、
        /// それでも見つからない場合は新しいGameObjectを作成してコンポーネントを追加します
        /// </summary>
        /// <returns>シングルトンインスタンス</returns>
        /// <remarks>
        /// このプロパティは遅延初期化を実装しており、最初のアクセス時にインスタンスを作成します
        /// 自動生成されたGameObjectは "{型名} Auto-Generated" という名前になります
        /// </remarks>
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    // シーン内から既存のインスタンスを検索
                    instance = FindAnyObjectByType<T>();
                    if (instance == null)
                    {
                        // 見つからない場合は新しく作成
                        var go = new GameObject(typeof(T).Name + " Auto-Generated");
                        instance = go.AddComponent<T>();
                    }
                }

                return instance;
            }
        }

        /// <summary>
        /// MonoBehaviourのAwakeメソッド
        /// 継承先でAwakeをオーバーライドする場合は、必ずbase.Awake()を呼び出してください
        /// </summary>
        /// <remarks>
        /// このメソッドは自動的にInitializeSingleton()を呼び出し、
        /// シングルトンの初期化を行います
        /// </remarks>
        protected virtual void Awake()
        {
            InitializeSingleton();
        }

        /// <summary>
        /// シングルトンの初期化処理を行います
        /// 重複インスタンスの防止とDontDestroyOnLoadの適用を行います
        /// </summary>
        /// <remarks>
        /// 実行時のみ動作し、エディタモードでは何も行いません
        /// AutoUnparentOnAwakeがtrueの場合、GameObjectを親から切り離します
        /// 既にインスタンスが存在する場合、重複するGameObjectを破棄します
        /// </remarks>
        protected virtual void InitializeSingleton()
        {
            // エディタモードでは何もしない
            if (!Application.isPlaying) return;

            // 自動的に親から切り離す設定の場合
            if (AutoUnparentOnAwake)
            {
                transform.SetParent(null);
            }

            // 最初のインスタンスの場合
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject); // シーン切り替え時に破棄されないように設定
            }
            else
            {
                // 重複インスタンスの場合は破棄
                if (instance != this)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}