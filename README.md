# 记忆翻牌 Memory Card（Unity 2D）

基于 **Unity 2022.3 LTS + 内置渲染管线 + 世界坐标方案** 实现的休闲记忆配对小游戏。

玩法：3 行 × 4 列共 12 张牌，每次翻开两张，牌面相同则保持正面并计分，不同则 1.5 秒后自动翻回；6 对全部配对后计时停止并结算。

> **实现路线：路线 B（世界坐标）** —— 卡牌是场景中的 `SpriteRenderer` 物体，位置严格由课件第 7 页的公式算出，**不使用 UGUI 的 GridLayoutGroup**。这是为对齐课件算法而定的路线。
>
> 配套课件：`D:\第03章 记忆翻牌.pptx`（14 页）
> 素材来源：`D:\03-MemoryCard-Resource\03-MemoryCard-Resource\`（已复制到 `Assets/Art`）

---

## 1. 玩法

| 项 | 规则 |
|---|---|
| 牌池 | 6 种牌面（circle / crescent / diamond / heart / sanjiao / square）× 各 2 张 = 12 张 |
| 布局 | 3 行 × 4 列，位置由公式 `(x,y) = (offsetX*j + originalX, offsetY*i + originalY)` 计算 |
| 操作 | 鼠标左键单击卡牌（`OnMouseDown` + 2D 碰撞体） |
| 配对成功 | 两牌 `id` 相同 → 保持正面、锁定、得分 +1 |
| 配对失败 | 两牌 `id` 不同 → 停留 1.5 s → 自动翻回背面 |
| 结束 | 6 对全部配对 → 计时停止 → 胜利面板（用时 + 得分） |
| 重开 | 胜利面板中的 Restart 按钮 |

## 2. 操作

1. 打开 `Assets/Scenes/Main.unity`，点 Play。
2. 点 `Start` 开始计时并铺牌。
3. 单击两张卡牌配对。
4. 全部配对后点 `Restart` 再来一局。

## 3. 环境要求

| 项 | 要求 |
|---|---|
| Unity 版本 | **2022.3 LTS**（已确认） |
| 项目模板 | **2D (Built-In Render Pipeline)** —— 不要选 URP / HDRP |
| 必需模块 | 构建 WebGL 需 **WebGL Build Support**（Hub → Installs → 该版本齿轮 → Add Modules → 勾选） |
| 额外包 | 无，不依赖任何第三方包 |

## 4. 目录结构

```
MemoryCard/
├── Assets/
│   ├── Art/            10 个素材（卡背 / 6 种牌面 / Start 按钮 / 桌面 / 胜利图）
│   ├── Prefabs/        Card.prefab（需在编辑器内创建）
│   ├── Scenes/         Main.unity（需在编辑器内创建）
│   └── Scripts/        5 个核心脚本（ShuffleUtil / Card / BoardManager / GameManager / UIManager）
│                       + 2 个临时脚本（M1Preview.cs 用于 M1、AutoStart.cs 用于 M2–M3）
├── docs/
│   ├── Unity编辑器操作手册.md   ★ 逐步操作手册（M1–M7，含构建与发布）
│   ├── 需求冻结.md      冻结范围、砍功能清单、时间盒
│   ├── 验收清单.md      DoD 勾选表 + 7 条边界用例
│   └── 实验报告.md      实验报告模板（按节填写）
├── .gitignore
├── .gitattributes
└── README.md
```

## 5. 场景搭建（路线 B 完整步骤）

### 5.1 场景层级

```
Main (Scene)
├── Main Camera        Orthographic，Size = 4.5，Position (0, 2.5, -10)，Background 深绿
├── Background         SpriteRenderer = table_top，sortingOrder = -10
├── Board              空物体 + BoardManager.cs（Position 归零）
│   └── Card_0_0 … Card_2_3   运行时由脚本生成，共 12 个
├── Canvas             Screen Space - Overlay + Canvas Scaler（Scale With Screen Size / 1024×768 / Match 0.5）
│   ├── TopBar (TimeText / ScoreText)
│   ├── StartPanel (StartButton)
│   └── WinPanel (VictoryImage / ResultText / BestText / RestartButton)
└── GameManager        空物体 + GameManager.cs
```

### 5.2 关键参数（照填即可）

| 物体 | 参数 | 为什么 |
|---|---|---|
| Main Camera | Projection = **Orthographic**；Size = **4.5**；Position = **(0, 2.5, -10)** | 牌阵横向 x ∈ [-3.75, 3.75]（宽 7.5）、纵向 y ∈ [-1, 6]（高 7），中心在 y = 2.5。Size 4.5 → 视野高 9；16:9 宽 16、4:3 宽 12、1:1 宽 9，**均 ≥ 7.5，所以 4.5 对所有横屏与正方形分辨率都够用**。只有**竖屏 9:16**（视野宽仅 5.06）不足，需把 Size 提到 **7** |
| Main Camera | Z **必须为负**，卡牌 Z 为 0 | 否则 2D 射线点不到卡牌碰撞体，点击会失效 |
| Background | SpriteRenderer = `table_top`，Sorting Order = **-10**，缩放至铺满视野 | 层级低于卡牌，保证卡牌不被遮住 |
| Board | `rows=3, cols=4, originalX=-3, originalY=0, offsetX=2, offsetY=2.5`；`images` 按 circle→crescent→diamond→heart→sanjiao→square 顺序填 6 个；`backSprite` = `card_back`；`cardPrefab` = Card.prefab | 与课件第 7、9 页一致 |
| 面板与点击 | **StartPanel / WinPanel 的 Image 要保留 `Raycast Target` 勾选** | UGUI **不会**自动拦住世界物体的 `OnMouseDown`：点击面板会「穿透」到卡牌。保留 Raycast Target + 脚本里的 `EventSystem.current.IsPointerOverGameObject()` 判断（`Card.cs` 已实现），才能在面板显示时屏蔽卡牌点击。若取消勾选，点 Start 按钮会顺带翻开按钮下方那张牌 |

### 5.3 Card.prefab 规格

| 项 | 值 |
|---|---|
| 组件 | `SpriteRenderer` + `BoxCollider2D` + `Card.cs` |
| Transform | Position 归零、Scale (1,1,1)，**Pivot 由 Sprite 导入设置决定，统一 Center** |
| SpriteRenderer | sprite = `card_back`，Sorting Layer = Default，Order in Layer = 0 |
| BoxCollider2D | Size **(1.5, 2)**、Offset (0, 0)、**不要勾 Is Trigger** |
| Rigidbody2D | 不需要；如需添加请设 Body Type = Kinematic（脚本已兼容） |

> 卡牌世界尺寸 = 150 × 200 像素 ÷ PPU 100 = **1.5 × 2 世界单位**，这也是 Collider 尺寸与相机取景的基准。

> 小技巧：在 Scene 视图选中 `Board` 物体，会看到脚本用 Gizmo 画出的 12 个黄色落点框，用来对齐相机非常直观。

## 6. 素材导入设置（逐张设置）

| 设置项 | 值 |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | **Single** |
| Pixels Per Unit | **100** |
| Pivot | **Center**（所有卡牌必须一致） |
| Mesh Type | Tight |
| Compression | High Quality 或 None |
| Filter Mode | Bilinear |

> 6 个卡面（symbol）本身就是"米黄圆角卡 + 彩色符号"的完整牌面，**直接当整张 Sprite 使用，不要抠底**；只有圆角外缘是透明像素。

## 7. 构建

### 7.0 推荐用法：安全构建入口

> 直接用 `File → Build Settings → Build` 时，若编辑器**正在编译脚本**，构建会立即失败并报
> `Error building Player because scripts are compiling`。这不是工程错误，是时序问题。

菜单 `Tools → Memory Card → Build WebGL (Safe)`：

| 行为 | 说明 |
|---|---|
| 等待空闲 | 用 `delayCall` 非阻塞轮询，直到 `isCompiling / isUpdating / isPlaying` 全为 false |
| 自动切平台 | 当前平台不是 WebGL 时先切换（会触发资源重导入） |
| 自动退播放 | 处于 Play 模式时先停止（Play 模式不允许 Build） |
| 输出报告 | 结果 / 耗时 / 体积明细写入 `Logs/build-webgl.txt`，并弹出发送目录 |

等价的命令行入口（适合 CI）：

```bash
Unity.exe -quit -batchmode -projectPath "E:/unitwork/Memory Card" \
  -executeMethod MemoryCard.EditorTools.BuildTools.BuildWebGLBatch
```

### 7.0.1 工程自检

菜单 `Tools → Memory Card → Validate Project`，一次性检查 15 项配置（缺失脚本、管理器序列化空引用、
牌面素材完整度、牌数奇偶、Card 预制体组件、构建场景列表、WebGL 模块、精灵 PPU、相机取景覆盖），
报告写入 `Logs/validate-report.txt`。**构建前建议先跑一次**。

### 7.1 WebGL

1. `File → Build Settings → WebGL → Switch Platform`
2. **Player Settings** 建议：Color Space = Gamma；Publishing Settings → Compression Format = **Gzip**（若托管平台不支持 Gzip 再换 Disabled）
3. 点 `Build`，输出到工程外的目录（例如 `E:\work\Builds\WebGL`），**不要放进 Assets 内**
4. 本地预览：在该目录起一个静态服务器，例如
   ```bash
   cd /e/work/Builds/WebGL
   "C:/Users/li/.workbuddy/binaries/python/versions/3.13.12/python.exe" -m http.server 8000
   ```
   然后浏览器打开 `http://localhost:8000`。
   > 直接双击 `index.html` 会因浏览器安全策略加载失败（file:// 协议），这不是构建错误。

### 7.2 Windows

`File → Build Settings → PC, Mac & Linux Standalone → Windows x86_64 → Build`。

> **中文显示提醒**：Unity 内置 Arial 不含中文字形，WebGL 下会显示为方框。本项目 UI 只用数字与英文，规避该问题。

## 8. GitHub Pages 部署步骤

**目标**：得到形如 `https://<你的用户名>.github.io/memory-card/` 的可玩链接。

1. **建仓库**：GitHub 网页 → New repository → 名称 `memory-card` → Public → 不勾 Add README → Create。
2. **本地初始化并推送**（Git Bash，在 `E:\work\MemoryCard` 下执行）：
   ```bash
   cd /e/work/MemoryCard
   git init
   git add .
   git commit -m "chore: 初始化记忆翻牌工程（脚本与文档）"
   git branch -M main
   git remote add origin https://github.com/<你的用户名>/memory-card.git
   git push -u origin main
   ```
   > `Library/ Temp/ Obj/ Build*/` 已被 `.gitignore` 排除，不会上传；`Assets/**/*.meta` 必须提交。
   > 若推送大文件报错或很慢，检查是否误提交了 `Build` 目录。
3. **准备 Pages 目录**：在仓库根下建 `docs/`（已存在）之外**不要**用 docs，而是新建 `webgl/` 目录，把 WebGL 构建产物（`index.html`、`Build/`、`TemplateData/`）整个拷进去：
   ```bash
   mkdir -p /e/work/MemoryCard/webgl
   cp -r /e/work/Builds/WebGL/* /e/work/MemoryCard/webgl/
   git add webgl
   git commit -m "build: 添加 WebGL 构建产物"
   git push
   ```
   > 注意：`.gitignore` 忽略了 `[Bb]uild/`，而 WebGL 产物里正好有一个 `Build/` 子目录，**必须为它开白名单**，在 `.gitignore` 末尾加：
   > ```
   > !webgl/Build/
   > !webgl/Build/**
   > ```
4. **开启 Pages**：仓库 → Settings → Pages → Source 选 **Deploy from a branch** → Branch 选 `main`、目录选 **/ (root)** → Save。
5. 等 1–2 分钟，访问 `https://<你的用户名>.github.io/memory-card/webgl/` 即为可玩链接。
   > 若只想给老师一个短链接，可用该完整 URL；也可把 `webgl/index.html` 改名为仓库首页，但保持 `webgl/` 子路径更清晰。
6. **更新 README**：把链接与操作说明写进仓库首页，方便老师与招聘方查看。
7. **后续更新**：重新 Build → 覆盖 `webgl/` → `git add . && git commit -m "build: 更新 WebGL" && git push`，Pages 会自动重新发布。

## 9. 与课件的对应关系

| 课件页 | 内容 | 对应实现 |
|---|---|---|
| 第 7 页 | 布局：3 行 4 列，`(x,y) = (offsetX*j + originalX, offsetY*i + originalY)`，originalX=-3、originalY=0、offsetX=2、offsetY=2.5 | `BoardManager.BuildBoard()` 中直接按公式计算 `transform.position`；可用 `GetPosition(i,j)` 复核 |
| 第 9 页 | `images`(6 种牌面) + `numbers` 初始 `{0,0,1,1,2,2,3,3,4,4,5,5}` | `BoardManager.BuildNumbers()` |
| 第 10 页 | 洗牌：i 遍历，r 取 [i, 末尾]，交换 | `ShuffleUtil.Shuffle<T>()` |
| 第 11 页 | 第 i 行第 j 列 → `Images[numbers[i*cols + j]]` | `BoardManager.BuildBoard()` |
| 第 12 页 | 卡牌三状态：未翻开 / 翻开 / 配对成功 | `Card.State` 枚举 |
| 第 13 页 | 配对判定：比较两牌 `id` | `GameManager.CheckMatch()` |
| 第 14 页 | 协同程序：`IEnumerator` + `WaitForSeconds(1.5f)` | `GameManager.CheckMatch()` |

## 10. Git 协作

本项目为**独立开发**，无需分支协作约定。仅保留以下习惯：

- 每个里程碑完成即提交一次，信息格式：`[M3] 洗牌+配对协程，可玩一局`
- 提交前确认 `Library/`、`Build*/` 未被纳入
- `Assets/` 下的 `.meta` 必须提交

## 11. 开发排期

**两周冲刺，里程碑驱动**（每个里程碑有硬工时上限，到点降级）。详见 `E:\work\记忆翻牌_Unity开发计划.md`。

| 里程碑 | 达标判据 | 上限 |
|---|---|---|
| M1 静态牌桌 | 绿色桌面 + 3×4 共 12 张红卡背，间距均匀 | 4 h |
| M2 可翻牌 | 单击卡牌翻到正面 | 4 h |
| M3 完整一局可玩 | 能打到 6 对全配对成功（**需求冻结**） | 5 h |
| M4 闭环体验 | 点 Start → 计时 → 胜利面板出用时与得分 | 4 h |
| M5 稳定性 | 验收清单功能项与质量项全通过 | 4 h |
| M6 在线可玩 | 链接可玩 + Windows 包 | 5 h |
| M7 实验报告 | 一页报告成稿 | 3 h |
