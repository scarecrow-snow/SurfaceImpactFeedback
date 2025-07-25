# SurfaceImpactFeedback

Unity環境でのサーフェス固有のインパクト（衝撃）エフェクトを管理するシステムです。異なるサーフェス（地面、金属、木材など）に対して、個別のパーティクルエフェクト、デカール、音声エフェクトを自動的に適用します。

## 主要機能

- **サーフェス検出**: テクスチャやマテリアルに基づいてサーフェスタイプを自動判定
- **エフェクト管理**: パーティクルエフェクト、デカール、音声エフェクトの統合管理
- **オブジェクトプーリング**: パフォーマンス最適化のためのエフェクトプーリングシステム
- **ファクトリーパターン**: 異なるエフェクトタイプの生成にファクトリーパターンを採用
- **ストラテジーパターン**: テクスチャ検出にストラテジーパターンを適用

## 技術スタック

- **Unity**: 6000.0.42f1
- **レンダリングパイプライン**: Universal Render Pipeline (URP) 17.0.4
- **主要ライブラリ**:
  - UniTask 2.5.10 (非同期処理)
  - R3 1.3.0 (Reactive Extensions)
  - ObservableCollections (コレクション監視)
  - LitMotion (アニメーション)
  - Burst (高性能計算)

## クイックスタート

### 1. 基本的な使用方法

```csharp
// インパクト処理の実行
SurfaceImpactFeedback.HandleImpact(
    hitObject,      // ヒットしたGameObject
    hitPoint,       // ヒットポイント
    hitNormal,      // ヒット時の法線
    impactType,     // インパクトタイプ
    triangleIndex   // トライアングルインデックス（オプション）
);
```

### 2. マウスクリックでのインパクト実装例

```csharp
public class ClickImpact : MonoBehaviour
{
    [SerializeField] private SurfaceImpactFeedback.ImpactType impactType;
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                SurfaceImpactFeedback.HandleImpact(
                    hit.collider.gameObject, 
                    hit.point, 
                    hit.normal, 
                    impactType
                );
            }
        }
    }
}
```

### 3. デモシーンの実行

1. `Assets/Demo/DemoScene.unity`を開く
2. プレイモードでマウスクリックによるインパクトエフェクトを確認

## アーキテクチャ

### 核となるクラス

#### SurfaceImpactFeedback.cs
システムの中核となるシングルトンクラス
- `HandleImpact()`: インパクト処理の統括
- `GetTerrainTextureStrategy()`: Terrain用テクスチャストラテジー取得
- `GetRendererTextureStrategy()`: Renderer用テクスチャストラテジー取得
- `PlayEffects()`: エフェクトの再生処理

#### オブジェクトプーリングシステム
- `EffectObjectPoolBase<T>`: エフェクトプールの基底クラス
- `ParticleObjectPool`: パーティクルエフェクト専用プール
- `EffectObjectPool<T>`: 汎用エフェクトプール（任意のIEffect実装に対応）
- `EffectPoolFactory`: プールファクトリー（自動プール生成）

#### インターフェース抽象化
- `IEffect`: エフェクトの統一インターフェース
- `IEffectObjectPool`: プールインターフェース
- `IImpactSound`: 音声システムインターフェース

## 設計パターン

### 1. ファクトリーパターン
```csharp
// 異なるエフェクトタイプのオブジェクトプール生成
var pool = EffectPoolFactory.Create(spawnObjectEffect, isUseLimitedLifetime);
```

### 2. ストラテジーパターン
```csharp
// TerrainとRendererで異なるテクスチャ取得方法
var strategy = SurfaceImpactFeedback.GetTerrainTextureStrategy(terrain);
var textureIndex = strategy.GetTextureIndex(hitPoint, triangleIndex);
```

### 3. インターフェース抽象化
```csharp
// 任意のエフェクト実装に対応
public class CustomEffect : MonoBehaviour, IEffect
{
    public async UniTask PlayEffect(CancellationToken ct)
    {
        // カスタムエフェクト処理
        await CustomFadeLogic(ct);
    }
}
```

## 設定

### Surface ScriptableObject
サーフェスタイプごとの設定を管理
- **ImpactTypeEffects**: インパクトタイプごとのエフェクト設定

### SurfaceEffect ScriptableObject
エフェクトの詳細設定
- **SpawnObjectEffects**: オブジェクト生成エフェクト設定
- **PlayAudioEffects**: 音声エフェクト設定

### SurfaceImpactFeedback設定
- **Surfaces**: サーフェスタイプの設定リスト
- **DefaultSurface**: 未定義サーフェス用のデフォルト設定

## 拡張

### 新しいサーフェスの追加
1. `Surface`の新しいScriptableObjectを作成
2. `SurfaceEffect`でエフェクトを設定
3. `SurfaceImpactFeedback`のSurfacesリストに追加

### カスタムエフェクトの実装
```csharp
public class CustomEffect : MonoBehaviour, IEffect
{
    [SerializeField] private float duration = 5f;
    [SerializeField] private AnimationCurve fadeCurve;
    
    public async UniTask PlayEffect(CancellationToken ct)
    {
        // カスタムエフェクト処理
        await CustomFadeLogic(ct);
    }
    
    private async UniTask CustomFadeLogic(CancellationToken ct)
    {
        // 独自のフェード処理実装
        float elapsed = 0f;
        var renderer = GetComponent<Renderer>();
        var material = renderer.material;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = fadeCurve.Evaluate(elapsed / duration);
            material.color = new Color(
                material.color.r, 
                material.color.g, 
                material.color.b, 
                alpha
            );
            
            await UniTask.Yield(ct);
        }
    }
}
```

### 新しいエフェクトタイプの追加
1. `EffectType`列挙体を拡張
2. `EffectObjectPoolBase<T>`を継承した専用プールを作成
3. `EffectPoolFactory`に新しい生成ロジックを追加

## 対応可能なエフェクトシステム

- **SimpleDecal**: 基本的なスケールフェードアウト
- **URP Decal Projector**: Universal Render Pipelineの標準デカール
- **HDRP Decal**: High Definition Render Pipelineの高品質デカール
- **カスタムエフェクト**: ユーザー独自のエフェクト実装

### 後方互換性サポート

`DecalObjectPool<T>` クラスは `EffectObjectPool<T>` のエイリアスとして提供されており、既存のコードは引き続き動作します。ただし、新しいコードでは `EffectObjectPool<T>` の使用を推奨します。

```csharp
// 従来の使用方法（引き続き動作）
var pool = new DecalObjectPool<SimpleDecal>(prefab);

// 推奨される新しい使用方法
var pool = new EffectObjectPool<SimpleDecal>(prefab);
```

## パフォーマンス最適化

### メモリ管理
- **統一されたオブジェクトプーリング**: 効率的なメモリ再利用
- **ガベージコレクション削減**: エフェクトオブジェクトの使い回し
- **構造体パラメータ**: `EffectParameters`構造体でヒープアロケーション回避

### 非同期処理
- **UniTask統合**: 高性能な非同期処理でメインスレッドのブロック回避
- **CancellationToken対応**: 適切なキャンセル処理で無駄な処理を防止
- **並列エフェクト処理**: 複数エフェクトの同時実行サポート

### システム設計による最適化
- **基底クラス継承**: 共通処理の統一で実行効率向上
- **インターフェース抽象化**: 仮想関数呼び出しの最適化
- **ファクトリーパターン**: オブジェクト生成のオーバーヘッド削減
- **強化されたエラーハンドリング**: try-catch-finally構造で安全かつ効率的な処理

## プロジェクト構造

```
Assets/Surface Manager/
├── Scripts/                           # 核となるスクリプト群
│   ├── SurfaceImpactFeedback.cs       # システム統括クラス
│   ├── Components/                   # コンポーネント類
│   │   └── SimpleDecal.cs           # シンプルデカール実装
│   ├── Interfaces/                   # インターフェース定義
│   │   ├── IEffect.cs                # エフェクト統一インターフェース
│   │   └── IImpactSound.cs          # 音声システムインターフェース
│   ├── ObjectPools/                  # オブジェクトプール関連
│   │   ├── EffectParameters.cs      # エフェクトパラメータ構造体
│   │   ├── IEffectObjectPool.cs     # プールインターフェース
│   │   ├── EffectObjectPoolBase.cs  # プール基底クラス
│   │   ├── ParticleObjectPool.cs    # パーティクルプール
│   │   ├── EffectObjectPool.cs      # エフェクトプール（汎用化済み）
│   │   └── EffectPoolFactory.cs     # プールファクトリ
│   ├── ScriptableObjects/            # ScriptableObject関連
│   │   ├── Core/                    # 核となる設定
│   │   └── Effects/                 # エフェクト設定
│   └── TextureStrategy/              # テクスチャ検出ストラテジー
├── SurfaceImpactFeedback.prefab       # SufaceImpactFeedbackプレハブ
├── README.md                         # このファイル
├── LICENCE.md                        # ライセンス
└── package.json                      # パッケージ設定
```

## NuGetパッケージ

- Microsoft.Bcl.AsyncInterfaces 6.0.0
- Microsoft.Bcl.TimeProvider 8.0.0
- ObservableCollections.R3 3.3.4
- R3 1.3.0
- System.ComponentModel.Annotations 5.0.0
- System.Runtime.CompilerServices.Unsafe 6.0.0
- System.Threading.Channels 8.0.0

## リファクタリング履歴

### v2.1.0 - エフェクトシステムの汎用化 (2025年7月)

#### 主な改善点
- **インターフェース名の統一**: `IDecal` → `IEffect` に変更し、より汎用的な設計に
- **クラス名の統一**: `DecalObjectPool<T>` → `EffectObjectPool<T>` に変更
- **メソッド名の統一**: `PlayDecalEffect` → `PlayEffect` に変更
- **命名規則の統一**: すべてのエフェクト関連クラスで「Effect」を使用

#### 達成された成果
- **汎用性向上**: デカール以外のエフェクト（パーティクル、音声など）も統一的に扱える
- **一貫性向上**: すべてのエフェクトタイプで統一された命名規則
- **後方互換性**: `DecalObjectPool<T>` エイリアスで既存コードをサポート
- **保守性向上**: 統一されたインターフェースによりコードの理解が容易

### v2.0.0 - オブジェクトプール基盤の再設計 (2025年7月)

#### 主な改善点
- **EffectParameters構造体の導入**: エフェクトパラメータの統一管理
- **EffectObjectPoolBase<T>基底クラス作成**: 共通処理の抽象化と重複コード削減
- **エフェクトシステムの抽象化**: インターフェースにより任意のエフェクト実装に対応
- **エラーハンドリング強化**: try-catch-finally構造、CancellationToken適切な処理

#### 達成された成果
- **コード削減**: ParticleObjectPool 28%減、EffectObjectPool 44%減
- **柔軟性向上**: 任意のエフェクト実装（URP/HDRP/カスタム）に対応
- **保守性向上**: 新しいエフェクトタイプの追加が容易
- **後方互換性**: 既存コードを破壊せずに新機能を利用可能

## ライセンス

詳細は`LICENCE.md`を参照してください。

## サポート

バグ報告や機能要求は、プロジェクトのIssueトラッカーまでお願いします。