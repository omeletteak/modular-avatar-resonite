# Future Plans
- [ ] Batch build of multiple prefabs
  - [x] Create initial files
  - [ ] **UI Construction**
    - [ ] Define UI layout in UXML (`ResoniteBatchBuildUI.uxml`)
    - [ ] Style UI with USS (`ResoniteBatchBuildUI.uss`)
    - [ ] Implement UI logic in C# (`ResoniteBatchBuildUI.cs`)
  - [ ] **Core Logic (Controller)**
    - [ ] Implement `ExecuteBatchBuild` method in `BatchBuildController.cs`
    - [ ] Loop through selected prefabs
    - [ ] Call existing single-build logic for each prefab
    - [ ] Implement progress reporting (`EditorUtility.DisplayProgressBar`)
    - [ ] Implement error handling for individual prefab builds
    - [ ] Implement finalization/cleanup
  - [x] **DLL Management / Compatibility (Crucial)**
    - **Problem:** Unity Editor cannot directly reference `.net9.0` DLLs (e.g., `Puppeteer.dll`) used for the Resonite back-end process.
    - **Solution:** The project uses a two-part design. A `netstandard2.1` compatible project (`ResoPuppetSchema`) provides the API definitions and data structures (via Protobuf) for Unity. A separate `.net9.0` project (`Puppeteer`) runs as an external process and is communicated with via gRPC.
    - **Implementation Details & Troubleshooting:**
      - The `ResoPuppetSchema` project and its dependencies (gRPC, Protobuf) are the *only* DLLs that should be placed in the `Managed` folder visible to Unity.
      - The `Puppeteer` executable and its `.net9.0` dependencies are placed in the `ResoPuppet~` directory and are launched by the editor script. They must not be in the `Managed` folder.
      - **Fork-specific fix:** The package name was hardcoded in `ResoniteBuildUI.cs` and `RPCClientController.cs`. This was updated to use the forked package name (`omelette_ak.nadena.dev.modular-avatar.resonite`) to ensure correct path resolution for UI assets and the `ResoPuppet~` launcher directory.
      - **Build process:** A clean `Managed` folder is created by specifically building `ResoPuppetSchema.csproj` and copying its output. The full solution build (`ResoniteHook.sln`) generates the `.net9.0` components.
      - **Synchronization with Test Project:** To apply changes from this source repository to a local Unity test project, the following steps are required:
        1. Run `dotnet build "Resonite~/ResoniteHook/ResoPuppetSchema/ResoPuppetSchema.csproj"` to build the Unity-compatible DLLs.
        2. Run `./DevTools~/SyncToUnity.ps1` to build the `.net9.0` components and create the `ResoPuppet~` directory.
        3. Clear the `Managed` folder in the test project (`rm -rf .../Managed/*`).
        4. Copy the clean `Managed` folder contents: `cp -r Resonite~/ResoniteHook/ResoPuppetSchema/bin/* YourTestProject/Packages/omelette_ak.nadena.dev.modular-avatar.resonite/Managed/`
        5. Copy the `ResoPuppet~` folder: `cp -r ResoPuppet~ YourTestProject/Packages/omelette_ak.nadena.dev.modular-avatar.resonite/`
        6. Copy any other changed source files (e.g., from `Editor` or `Runtime`) as needed.
  - [x] **Integration with Existing Build Process (via Inter-Process Communication)**
    - `RPCClientController.cs` uses types from the `netstandard2.1` API Stub DLL (`ResoPuppetSchema`).
    - It handles communication using `proto` definitions and `GrpcDotNetNamedPipes`, without directly referencing `.net9.0` types.
    - The single-prefab build is initiated from `ResoniteBuildUI.cs`, which calls `BuildController.cs` to manage the build process.
- [ ] Ability to attach thumbnails

# 今後の計画
- [ ] 複数prefabの一括ビルド
  - [x] 必要な空ファイルを作成
  - [ ] **UIの構築**
    - [ ] UXMLでUIレイアウトを定義 (`ResoniteBatchBuildUI.uxml`)
    - [ ] USSでUIを装飾 (`ResoniteBatchBuildUI.uss`)
    - [ ] C#でUIロジックを実装 (`ResoniteBatchBuildUI.cs`)
  - [ ] **ビルドの実行管理（コアロジック）**
    - [ ] `BatchBuildController.cs` に `ExecuteBatchBuild` メソッドを実装
    - [ ] 選択されたPrefabをループ処理
    - [ ] 各Prefabに対して既存の単一ビルドロジックを呼び出し
    - [ ] 進捗報告を実装 (`EditorUtility.DisplayProgressBar`)
    - [ ] 個々のPrefabビルドのエラーハンドリングを実装
    - [ ] 最終処理/クリーンアップを実装
  - [x] **DLL管理 / 互換性 (重要)**
    - **問題:** Unity Editorは、Resoniteのバックエンドプロセスで使用される`.net9.0`のDLL（例: `Puppeteer.dll`）を直接参照できない。
    - **解決策:** プロジェクトは2部構成を採用。`netstandard2.1`互換プロジェクト(`ResoPuppetSchema`)が、Unity向けのAPI定義とデータ構造(Protobuf経由)を提供。独立した`.net9.0`プロジェクト(`Puppeteer`)が外部プロセスとして実行され、gRPCを介して通信する。
    - **実装詳細とトラブルシューティング:**
      - `ResoPuppetSchema`プロジェクトとその依存関係(gRPC, Protobuf)のみが、Unityから見える`Managed`フォルダに配置されるべきDLLである。
      - `Puppeteer`実行可能ファイルとその`.net9.0`依存関係は`ResoPuppet~`ディレクトリに配置され、エディタスクリプトによって起動される。これらは`Managed`フォルダに含めてはならない。
      - **フォーク版特有の修正:** パッケージ名が`ResoniteBuildUI.cs`と`RPCClientController.cs`にハードコードされていた。これをフォーク版のパッケージ名(`omelette_ak.nadena.dev.modular-avatar.resonite`)に更新し、UIアセットと`ResoPuppet~`ランチャーディレクトリへのパス解決を正しくした。
      - **ビルドプロセス:** クリーンな`Managed`フォルダは、`ResoPuppetSchema.csproj`を個別にビルドし、その出力をコピーすることで作成される。ソリューション全体のビルド(`ResoniteHook.sln`)は`.net9.0`コンポーネントを生成する。
      - **テストプロジェクトへの変更の反映手順:** このソースリポジトリでの変更をローカルのUnityテストプロジェクトに適用するには、以下の手順が必要となる：
        1. `dotnet build "Resonite~/ResoniteHook/ResoPuppetSchema/ResoPuppetSchema.csproj"` を実行し、Unity互換のDLLをビルドする。
        2. `./DevTools~/SyncToUnity.ps1` を実行し、`.net9.0`コンポーネントをビルドして`ResoPuppet~`ディレクトリを作成する。
        3. テストプロジェクトの`Managed`フォルダをクリーンにする (`rm -rf .../Managed/*`)。
        4. クリーンな`Managed`フォルダの中身をコピーする: `cp -r Resonite~/ResoniteHook/ResoPuppetSchema/bin/* YourTestProject/Packages/omelette_ak.nadena.dev.modular-avatar.resonite/Managed/`
        5. `ResoPuppet~`フォルダをコピーする: `cp -r ResoPuppet~ YourTestProject/Packages/omelette_ak.nadena.dev.modular-avatar.resonite/`
        6. その他、変更されたソースファイル（例: `Editor`や`Runtime`ディレクトリ内のファイル）を必要に応じてコピーする。
  - [x] **既存ビルド処理との連携（プロセス間通信経由）**
    - `RPCClientController.cs`は`netstandard2.1`のAPIスタブDLL(`ResoPuppetSchema`)の型を使用する。
    - `proto`定義と`GrpcDotNetNamedPipes`を使った通信を処理し、`.net9.0`の型を直接参照しない。
    - 単一Prefabのビルドは`ResoniteBuildUI.cs`から開始され、`BuildController.cs`がビルドプロセスを管理する。
- [ ] サムネイルを添付できるようにする