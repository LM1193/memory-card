# Unity 编辑器逐步操作手册 ——《记忆翻牌》

**环境**：Unity **2022.3.62f3c1**（中国版）· 2D (Built-In Render Pipeline) · C#
**路线**：路线 B —— 世界坐标方案（卡牌为场景中的 SpriteRenderer，位置由课件第 7 页公式算出）
**素材源**：`E:\work\MemoryCard\Assets\Art`（10 张）
**工程实际路径**：`E:\unitwork\Memory Card` ← **以这个为准**。下文出现的 `E:\work` 一律替换为 `E:\unitwork`
**手册用法**：按 M1 → M7 顺序执行。每步只写"怎么点、填什么"；每个里程碑末尾有一段 ✔ 验证。

---

## 目录

- [M1 静态牌桌（新建工程 + 导入素材 + 摆 12 张卡背）](#m1-静态牌桌)
- [M2 可翻牌](#m2-可翻牌)
- [M3 完整一局可玩](#m3-完整一局可玩)
- [M4 闭环体验（Canvas + 面板 + 按钮）](#m4-闭环体验)
- [M5 稳定性验证](#m5-稳定性验证)
- [M6 构建与发布（WebGL / Windows / GitHub Pages）](#m6-构建与发布)
- [M7 实验报告截图](#m7-实验报告截图)
- [附A 临时脚本说明](#附a-临时脚本说明)
- [附B 中文界面菜单对照](#附b-中文界面菜单对照)
- [附C 坑清单](#附c-坑清单)

---

<a id="m1-静态牌桌"></a>
## M1 静态牌桌

**前置**：关闭 Unity 与 Unity Hub。

**0.** 把 `E:\work\MemoryCard` 重命名为 `E:\work\MemoryCard_docs`。
> 原因：Hub 新建工程要求目标目录为空，否则创建失败。

**1.** Unity Hub → 安装（Installs）→ `2022.3.62f3c1` 行 → 齿轮 ⚙ → 添加模块… → 展开「平台」→ 勾 **WebGL Build Support** → 继续/安装。

**2.** Unity Hub → 项目（Projects）→ 新建项目（New project）→ 标签 `2D` → 模板 `2D (Built-In Render Pipeline)` → 编辑器版本确认 `2022.3.62f3c1` → Project name = `MemoryCard`、Location = `E:\work` → 创建项目。

**3.** 项目（Project）窗口右键 `Assets` → 创建（Create）→ 文件夹（Folder），建 4 个：`Art`、`Scripts`、`Prefabs`、`Scenes`。

**4.** 把 `E:\work\MemoryCard_docs\Assets\Art` 的 10 张图拖到项目窗口的 `Assets/Art`；把 `MemoryCard_docs\Assets\Scripts` 的 7 个 `.cs` 拖到 `Assets/Scripts`。
> 也可用：右键目标文件夹 → 导入新资源…（Import New Asset…）。**7 个脚本必须全导**，它们互相依赖。

**5.** 项目窗口点 `Assets/Art` → `Ctrl+A` 全选 → 检查器（Inspector）设置：

| 设置项 | 值 |
|---|---|
| 纹理类型 Texture Type | `精灵(2D 和 UI) Sprite (2D and UI)` |
| 精灵模式 Sprite Mode | `单个 Single` |
| 每单位像素数 Pixels Per Unit | `100` |
| 轴心 Pivot | `中心 Center` |
| 网格类型 Mesh Type | `紧密 Tight` |
| 压缩 Compression | `高质量 High Quality` |
| 过滤模式 Filter Mode | `双线性 Bilinear` |
| 最大尺寸 Max Size | `1024` |
| Alpha Is Transparency | 勾选（`table_top` / `victory` 无 Alpha，可不勾） |

设完点右下 **应用 Apply**。

**6.** 双击打开 `Assets/Scenes/SampleScene` → 文件（File）→ 另存为（Save As…）→ 定位 `Assets/Scenes` → 文件名 `Main` → 保存。

**7.** 层级（Hierarchy）选 `Main Camera` → 检查器：`投影 Projection` = `正交 Orthographic`、`尺寸 Size` = `4.5`、`位置 Position` = X `0` / Y `2.5` / Z `-10`、`背景 Background` = `#2C7048`。

**8.** 层级右键 → 创建空对象（Create Empty）→ 改名 `Background` → 添加组件（Add Component）→ `精灵渲染器 Sprite Renderer` → `精灵 Sprite` ← 拖 `Assets/Art/table_top` → `排序图层 Sorting Layer` = `Default`、`图层顺序 Order in Layer` = `-10` → `位置 Position` = `(0, 2.5, 0)`、`缩放 Scale` = `(1.6, 1.6, 1)`。

**9.** 层级右键 → 创建空对象 → 改名 `Board` → `位置 Position` = `(0, 0, 0)` → `图层 Layer` 保持 `Default` → 添加组件 → `Board Manager`：
- `布局（课件第 7 页）`：`Rows`=`3`、`Cols`=`4`、`Original X`=`-3`、`Original Y`=`0`、`Offset X`=`2`、`Offset Y`=`2.5`（默认已对，核对即可）
- `Images` 6 个槽按序拖入：`circle-symbol`、`crescent-symbol`、`diamond-symbol`、`heart-symbol`、`sanjiao-symbol`、`square-symbol`
- `Back Sprite` ← `card_back`
- `Card Sorting Order` = `0`
- `Board Root` 留空
- `Card Prefab` 先留空

**10.** 层级右键 → 创建空对象 → 改名 `Card` → `位置 Position` = `(0,0,0)`、`缩放 Scale` = `(1,1,1)` → `图层 Layer` 保持 `Default` → 添加组件：
- `精灵渲染器 Sprite Renderer` → `精灵 Sprite` ← `card_back`
- `盒型碰撞体 2D Box Collider 2D` → `尺寸 Size` = `1.5, 2`；`偏移 Offset` = `0, 0`；`是触发器 Is Trigger` **不勾**
- `Card`（脚本）

然后把层级里的 `Card` 拖到项目窗口的 `Assets/Prefabs`。

**11.** 层级选 `Board` → `Board Manager` → `Card Prefab` 槽 ← 从**项目窗口**拖入 `Assets/Prefabs/Card.prefab`。

**12.** 层级选 `Board` → 添加组件 → `M1 Preview` → 点播放 ▶。
> 若"添加组件"里搜不到 `M1 Preview`：说明 `M1Preview.cs` 没进工程。把它复制到 `<工程>\Assets\Scripts\`，切回 Unity 等编译转圈结束即可。

### ✔ M1 验证
- 游戏（Game）视图：绿色桌面 + **3 行 × 4 列共 12 张红卡背**，间距均匀
- 层级里 `Board` 下出现 `Card_0_0` … `Card_2_3`
- 选中 `Board` 时，场景（Scene）视图出现 **12 个黄色线框**，与卡牌逐一重合
- 控制台（Console）无红字
> 说明：播放模式下卡牌是**运行时生成**的，停止播放后 `Card_*` 会全部消失，属正常现象。

---

<a id="m2-可翻牌"></a>
## M2 可翻牌

**1.** 层级右键 → 创建空对象 → 改名 `GameManager` → 添加组件 → `Game Manager`。

**2.** 把层级里的 `Board` 拖到 `Game Manager` 的 `Board` 槽（`UI` 槽先留空）。

**3.** 层级选 `Board` → 在 `M1 Preview` 组件右上角 `⋮` → 移除组件（Remove Component）。

**4.** 层级选 `GameManager` → 添加组件 → `Auto Start`。

**5.** 点播放 ▶。

**6.** 点一张卡背 → 变成正面；再点第二张 → 也变正面；在 1.5 秒内点第三张 → 无反应。

**7.** 播放中在层级里选任一 `Card_x_y` → 检查器 `Card` 组件的 `state` 字段显示 `Hidden` / `Revealed` / `Matched`。

### ✔ M2 验证
- 点牌能翻面；同屏最多 2 张正面；1.5 秒内第 3 张无效
- 控制台无 `NullReferenceException`

---

<a id="m3-完整一局可玩"></a>
## M3 完整一局可玩

**1.** 层级选 `GameManager` → `参数` → `Flip Back Delay` = `1.5`。

**2.** 播放 → 点两张**不同**牌面 → 两张保持正面约 1.5 秒 → 自动翻回背面。

**3.** 点两张**相同**牌面 → 保持正面、锁定（再点无反应），控制台打印 `得分=1`。

**4.** 继续配对，直到 6 对全部配完 → 计时停止（控制台不再刷新）。

**5.** 洗牌验证：停止播放 → 重新播放 3 次，观察每次牌面分布都不同。

**6.** 重开验证：播放中按 `R` 键 → 重新洗牌、得分与用时清零。

### ✔ M3 验证
- 失败对 1.5 秒后翻回；成功对锁定且加分；6 对配完停表
- 控制台依次出现 `得分=1` … `得分=6`
- 按 `R` 能重开，无残留翻回

---

<a id="m4-闭环体验"></a>
## M4 闭环体验

**1.** 层级右键 → `UI` → `画布 Canvas`。检查层级里是否有 `EventSystem`，没有则：`游戏对象 GameObject` → `UI` → `事件系统 Event System`。

**2.** 选 `Canvas` → 检查器 `画布缩放器 Canvas Scaler`：`UI 缩放模式` = `随屏幕大小缩放 Scale With Screen Size`、`参考分辨率` = `1024 × 768`、`屏幕匹配模式` = `匹配宽高 Match Width Or Height`、`Match` = `0.5`。

**3.** `Canvas` 右键 → `UI` → `旧版 Legacy` → `文本 Text` → 改名 `TimeText` → `文本` 内容填 `0.0`、字号 `30`、颜色白；锚点拖到左上角。

**4.** `Canvas` 右键 → `UI` → `旧版 Legacy` → `文本 Text` → 改名 `ScoreText` → 内容 `SCORE 0`，放在 `TimeText` 下方。

**5.** `Canvas` 右键 → `UI` → `面板 Panel` → 改名 `StartPanel` → `Image` 的 `颜色 Color` = 黑、`A` = `180`（半透明）。

**6.** `StartPanel` 右键 → `UI` → `旧版 Legacy` → `按钮 Button` → 改名 `StartButton`：
- 子物体 `Text` 内容改为 `START`
- `Image` → `源图像 Source Image` ← `start-button`
- `Rect Transform` 尺寸设为 `128 × 128`

**7.** `Canvas` 右键 → `UI` → `面板 Panel` → 改名 `WinPanel` → `Image` 的 `颜色 Color` = 黑、`A` = `200`。

**8.** `WinPanel` 右键 → `UI` → `图像 Image` → 改名 `VictoryImage` → `源图像 Source Image` ← `victory`；尺寸 `400 × 400`。

**9.** `WinPanel` 右键 → `UI` → `旧版 Legacy` → `文本 Text` → 改名 `ResultText` → 内容 `TIME 0.0s  SCORE 0`。

**10.** `WinPanel` 右键 → `UI` → `旧版 Legacy` → `文本 Text` → 改名 `BestText` → 内容 `BEST 0.0s`。

**11.** `WinPanel` 右键 → `UI` → `旧版 Legacy` → `按钮 Button` → 改名 `RestartButton` → 子 `Text` 内容改为 `RESTART`。

**12.** 选 `Canvas` → 添加组件 → `UI Manager` → 按字段逐个拖入：

| UIManager 字段 | 拖入对象 |
|---|---|
| `Time Text` | `TimeText` |
| `Score Text` | `ScoreText` |
| `Result Text` | `ResultText` |
| `Best Text` | `BestText` |
| `Start Panel` | `StartPanel` |
| `Win Panel` | `WinPanel` |
| `Start Button` | `StartButton` |
| `Restart Button` | `RestartButton` |

**13.** 层级选 `GameManager` → `Game Manager` → `UI` 槽 ← 拖 `Canvas`。

**14.** 层级选 `GameManager` → 移除 `Auto Start` 组件；在项目窗口删除 `Assets/Scripts/AutoStart.cs`。

**15.** 点播放 ▶ → 点 `START` → 玩到 6 对全配对 → 点 `RESTART`。

### ✔ M4 验证
- 进入播放先看到开始面板
- 点 `START`：面板消失、计时开始走、卡牌可点
- 全配对后：胜利面板显示 `victory` 图 + `TIME` + `SCORE` + `BEST`
- 点 `RESTART` 能重开
- **不要**在 `StartButton` 的 `On Click()` 里再加一遍绑定（`UIManager` 已用代码绑定，加了会一局开两次）

---

<a id="m5-稳定性验证"></a>
## M5 稳定性验证

**1.** 游戏（Game）视图左上角 `Aspect` 下拉 → 选 `16:9 1280×720`；再加一项 `4:3 1024×768` 切换对比。

**2.** 逐条执行边界用例：

| 用例 | 操作 | 期望 |
|---|---|---|
| T1 | 快速双击同一张背面牌 | 只翻一次 |
| T2 | 1.5 秒内点三张不同牌 | 第三张无反应 |
| T3 | 点已配对的牌 | 无反应、分数不变 |
| T4 | 失败对翻回前立刻点 `RESTART` | 无残留翻回 |
| T5 | 配到 3 对时点 `RESTART` | 分数、计时、牌面全部复位 |
| T6 | 开始面板显示时点面板区域的卡牌 | 无反应（防穿透守卫生效） |
| T7 | 每次只翻一张，逐张看牌面 | 12 张牌面都正确 |

**3.** 控制台（Console）→ 点 `Clear` 清空 → 完整跑一局 → 确认无红字、无警告堆积。

### ✔ M5 验证
- 16:9 与 4:3 下 12 张牌与 UI 都在视野内、不溢出
- T1–T7 全部符合期望
- 控制台干净

---

<a id="m6-构建与发布"></a>
## M6 构建与发布

### 6.1 WebGL 构建

**1.** 文件（File）→ 生成设置（Build Settings…）→ 左侧选 `WebGL` → 点 `切换平台 Switch Platform`（等进度条结束）。

**2.** `生成中的场景 Scenes In Build` → 点 `添加已打开的场景 Add Open Scenes`，确认 `Assets/Scenes/Main` 打勾。

**3.** 点左下 `播放器设置 Player Settings…` → `播放器 Player`：
- `其他设置 Other Settings`：`颜色空间 Color Space` = `Gamma`、`脚本后端 Scripting Backend` = `IL2CPP`、`API 兼容级别 Api Compatibility Level` = `.NET Standard 2.1`、`托管剥离级别 Managed Stripping Level` = `Minimal`
- `发布设置 Publishing Settings`：`压缩格式 Compression Format` = `Gzip`、勾选 `解压缩回退 Decompression Fallback`

**4.** 回生成设置 → 点 `生成 Build` → 输出目录填 `E:\work\Builds\WebGL`（**不要放在 Assets 内**）。

**5.** 本地预览：在 `E:\work\Builds\WebGL` 目录起静态服务器，浏览器打开该地址。
> 直接双击 `index.html` 会白屏（`file://` 安全策略），属正常，不是构建失败。

### 6.2 Windows 构建

**6.** 生成设置 → `PC, Mac & Linux Standalone` → `切换平台` → `目标平台` = `Windows`、`架构` = `x86_64` → `生成 Build` 到 `E:\work\Builds\Windows`。

**7.** 双击 `MemoryCard.exe` 跑一局验证。

### 6.3 GitHub Pages 部署

**8.** GitHub 网页 → 右上 `+` → `New repository` → 名称 `memory-card` → 选 `Public` → **不勾** `Add a README file` → `Create repository`。

**9.** 本地 Git Bash 进工程目录依次执行：

```bash
cd /e/work/MemoryCard
git init
git add .
git commit -m "chore: 初始化记忆翻牌工程"
git branch -M main
git remote add origin https://github.com/<你的用户名>/memory-card.git
git push -u origin main
```

**10.** 在工程根建 `webgl` 目录，把 `E:\work\Builds\WebGL\` 里的**全部内容**（`index.html`、`Build/`、`TemplateData/`）拷进去。

**11.** 确认 `.gitignore` 末尾有这两行（模板已加好）：

```
!webgl/Build/
!webgl/Build/**
```

**12.** 提交构建产物：

```bash
git add webgl
git commit -m "build: 添加 WebGL 产物"
git push
```

**13.** 网页：仓库 → `Settings` → 左侧 `Pages` → `Source` = `Deploy from a branch` → `Branch` = `main`、目录 = `/ (root)` → `Save`。

**14.** 等 1–2 分钟，页面顶部出现绿色提示 `Your site is live at …`，访问：

```
https://<你的用户名>.github.io/memory-card/webgl/
```

**15.** 用隐身窗口打开该链接，跑完一局验证。

**16.** 把链接与玩法说明写进仓库 `README.md` → `git add .` → `git commit -m "docs: 更新链接"` → `git push`。

**17.** 以后更新版本：重新 Build → 覆盖 `webgl/` → `git add . && git commit -m "build: 更新 WebGL" && git push`（Pages 自动重新发布）。

### ✔ M6 验证
- 本地 WebGL 能玩；Windows exe 能玩
- Pages 链接在隐身窗口可访问并能玩完整一局
- 线上首屏加载在校园网 10 秒内出画面

---

<a id="m7-实验报告截图"></a>
## M7 实验报告截图

| 截图 | 内容 | 采集时机 | 建议规格 |
|---|---|---|---|
| 1 | 卡牌状态表现 | M3：点开两张不同牌、尚未翻回时 | 游戏视图 1280×720 |
| 2 | 场景视图牌阵 + 12 个黄色落点框 | M1：选中 `Board` | 场景视图最大化 |
| 3 | 关键脚本代码 | 编辑器里打开 `GameManager.cs` 的 `CheckMatch` 段 | 代码清晰可读 |
| 4 | 开始界面（START 按钮） | M4：播放后未点开始前 | 游戏视图 1280×720 |
| 5 | 翻开两张不同牌面、未翻回 | M5 执行 T2/T7 时抓拍 | 游戏视图 1280×720 |
| 6 | 配对成功保持正面 | M5：配对同一牌面后 | 游戏视图 1280×720 |
| 7 | 胜利结算界面 | M5：打完一局 | 游戏视图 1280×720 |
| 8 | 浏览器中运行的 WebGL | M6：打开 Pages 链接后 | 浏览器全屏，**带地址栏** |

按 `docs/实验报告.md` 的各节把截图与结论填进去，替换所有 `【待填】`。

---

<a id="附a-临时脚本说明"></a>
## 附A 临时脚本说明

| 脚本 | 用于 | 何时删除 |
|---|---|---|
| `M1Preview.cs` | M1：让 Play 直接生成 12 张卡背（因为此时还没有 GameManager） | M2 第 3 步移除组件，可保留文件 |
| `AutoStart.cs` | M2/M3：自动开局、按 `R` 重开、控制台打印得分 | M4 第 14 步移除组件并删除文件 |

两个脚本都带 `[Tooltip]` 开关，可在检查器里关闭自动行为。

---

<a id="附b-中文界面菜单对照"></a>
## 附B 中文界面菜单对照

| 英文 | 中文 |
|---|---|
| Hierarchy / Project / Inspector / Scene / Game / Console | 层级 / 项目 / 检查器 / 场景 / 游戏 / 控制台 |
| GameObject / Component / Assets | 游戏对象 / 组件 / 资源 |
| Create Empty / Folder | 创建空对象 / 文件夹 |
| Add Component / Remove Component | 添加组件 / 移除组件 |
| Transform → Position / Rotation / Scale | 变换 → 位置 / 旋转 / 缩放 |
| Sprite Renderer / Sprite | 精灵渲染器 / 精灵 |
| Box Collider 2D / Size / Offset / Is Trigger | 盒型碰撞体 2D / 尺寸 / 偏移 / 是触发器 |
| Sorting Layer / Order in Layer | 排序图层 / 图层顺序 |
| Texture Type / Sprite Mode / Pixels Per Unit / Pivot | 纹理类型 / 精灵模式 / 每单位像素数 / 轴心 |
| Mesh Type / Compression / Filter Mode / Max Size / Apply | 网格类型 / 压缩 / 过滤模式 / 最大尺寸 / 应用 |
| Camera → Projection / Size / Background | 摄像机 → 投影 / 尺寸 / 背景 |
| Canvas / Canvas Scaler / EventSystem | 画布 / 画布缩放器 / 事件系统 |
| UI → Legacy → Text / Button / Panel / Image | UI → 旧版 → 文本 / 按钮 / 面板 / 图像 |
| Build Settings / Player Settings / Switch Platform | 生成设置 / 播放器设置 / 切换平台 |
| Color Space / Compression Format / Decompression Fallback | 颜色空间 / 压缩格式 / 解压缩回退 |

---

<a id="附c-坑清单"></a>
## 附C 坑清单

| # | 现象 | 原因 | 修复 |
|---|---|---|---|
| 0 | Hub 建工程失败 | `E:\work\MemoryCard` 已存在且非空 | 先改名为 `MemoryCard_docs`，建好工程后再合并回来 |
| 1 | 卡牌点了没反应 | 相机非正交 / 卡牌无 Collider2D / 卡牌在 `Ignore Raycast` 层 | 相机 `正交` + `Z=-10`；卡牌加 `Box Collider 2D`（`Size 1.5,2`、不勾 Trigger）；图层用 `Default` |
| 2 | 点 UI 顺带翻开下方卡牌 | UGUI 不会拦截世界物体的 `OnMouseDown`，点击会穿透 | 面板 Image **保留** `Raycast Target`；`Card.cs` 已内置 `EventSystem.current.IsPointerOverGameObject()` 守卫 |
| 3 | 卡牌模糊 | 导入参数不对 | `PPU=100`、`Compression=高质量`、`Max Size ≥ 原图`、`Pivot=中心` |
| 4 | 背景盖住卡牌 | 层级顺序错 | 背景 `Order in Layer = -10` < 卡牌 `0` |
| 5 | 运行报 `Missing` / 生成不出卡 | `Card Prefab` 绑成了场景实例 | 必须绑**项目窗口**里的 `Assets/Prefabs/Card.prefab` |
| 6 | WebGL 双击白屏 | `file://` 协议限制 / `Build/` 未提交 | 用静态服务器预览；`.gitignore` 加 `!webgl/Build/` 与 `!webgl/Build/**` |
| 7 | 界面文字是方框 | 内置 Arial 不含中文 | 本工程 UI 只用数字与英文；要中文须换 TextMeshPro + 中文字体图集 |
| 8 | 按钮点不动 | 场景里没有 `EventSystem` | `游戏对象 → UI → 事件系统` |
| 9 | 一局被开了两次 | 按钮同时做了代码绑定与 Inspector 绑定 | 只用代码绑定（`UIManager` 已写），`On Click()` 留空 |
| 10 | 「添加组件」搜不到 `M1 Preview` / 点 ▶ 没有任何卡牌 | `M1Preview.cs` 没被导入工程（只存在于文档目录） | 把 `M1Preview.cs`、`AutoStart.cs` 复制到 `<工程>\Assets\Scripts\`，切回 Unity 等编译结束 |
| 11 | 播放后 Console 无 `[M1Preview]` 日志 | 组件没挂上 / 挂错了物体 | 组件必须挂在带 `BoardManager` 的那个 `Board` 物体上 |
| 12 | 有日志但看不到卡牌 | `Images` / `Back Sprite` / `Card Prefab` 没绑定；或被背景挡住 | 绑定三项；背景 `Order in Layer = -10` |
| 13 | 场景关掉再打开，`Board` 等物体全没了 | 改完没 `Ctrl+S` 保存场景 | 每次改完场景按 `Ctrl+S` |
| 14 | 打开 Unity 后一片空白，"做的东西不见了" | `层级` 顶部显示的是 `SampleScene`，即打开了**空场景** | 打开 `Assets/Main.unity`，见附E |
| 15 | 开始按钮上出现"重影"文字（`StaStart` 之类） | `start-button.png` 素材**自带 Start 字样**，你又给按钮加了子物体 `Text` | 删掉 `StartButton` 下的 `Text` 子物体（M4 那步可跳过） |
| 16 | 按钮上文字看不见（白底白字） | `RestartButton` 用的是内置白色 `UISprite`，字也设成白色 | 文字改深色（`0.13,0.13,0.13`），或用彩色底图 |
| 17 | 面板遮罩太浅，卡牌透出来干扰文字 | 面板 `Image` 用的是内置 `Background` 精灵（有圆角、偏亮） | 把 `Source Image` 清空 → 变纯色块，`Color` 设 `(0.04,0.05,0.04)`、`A=0.90` |

---

<a id="附d-点播放没反应排查"></a>
## 附D "点 ▶ 没有 12 张卡背"排查顺序

按顺序做，命中哪一条就停在哪一条：

1. **切到 Unity 窗口 → 等右下角转圈结束**（Unity 只在获得焦点时才刷新导入的资源）。没反应就 `Ctrl+R`。
2. **`Ctrl+Shift+C` 打开控制台 → 有红字？** 把红字整段发出来。
3. **层级里有没有 `Board` 物体？** 没有 → 回 M1 第 9 步新建。
4. **`Board` 上有没有 `Board Manager` 组件？** 没有 → 添加组件搜 `Board Manager`。
5. **`Board Manager` 的绑定**：`Images` 6 个槽（circle / crescent / diamond / heart / sanjiao / square，顺序不能错）、`Back Sprite` = `card_back`、`Card Prefab` = `Assets/Prefabs/Card.prefab`。
6. **`Board` 上有没有 `M1 Preview` 组件？** 搜不到 → 见坑 10。
7. **`Main Camera`**：`正交`、`Size 4.5`、`Position (0, 2.5, -10)`。
8. 点 ▶。**控制台应打印**：`[M1Preview] 已生成 12 张卡牌（3 行 × 4 列），洗牌=True`。
   - 没有这条日志 → 第 6 步没生效（组件没挂上）
   - 有日志、Game 视图还是空的 → 第 5 步或相机问题

---

<a id="附e-画面空白排查"></a>
## 附E "做的东西不见了 / 打开后一片空白"排查顺序

**根因**：你打开的不是搭好的那个场景。本工程里有**两个** `.unity` 场景文件：

| 场景文件 | 里面有什么 | 说明 |
|---|---|---|
| `Assets/Main.unity` | 6 个根物体：`Main Camera`、`Background`、`Board`、`GameManager`、`Canvas`（含 StartPanel / StartButton / TimeText / ScoreText / WinPanel / victory / ResultText / BestText / RestartButton）、`EventSystem` | **你的主场景，全部成果都在这里** |
| `Assets/Scenes/SampleScene.unity` | 只有 1 个 `Main Camera` | 新建工程自带的空场景，没有任何成果 |

**判断方法**：看 `层级(Hierarchy)` 最上面那一行的名字。
- 显示 `SampleScene` → 你在**空场景**里，什么都看不到
- 显示 `Main` → 对，是这个

**切回去**：

1. `Ctrl+O`（`文件 → 打开场景`）
2. 定位到 **`Assets/Main.unity`** —— 注意它在 `Assets` **根目录**，不在 `Scenes` 文件夹里
   - 更快的做法：`项目` 窗口左侧点 `Assets` → 右侧双击 `Main`
3. 弹出"是否保存当前场景？" → 点 **`不保存 Don't Save`**（`SampleScene` 是空的，没有东西会丢）
4. 层级里应立刻出现上表那 6 个根物体

**防复发**：

5. `Ctrl+Shift+B` 打开 `生成设置` → 把 `Assets/Main.unity` 加进 `构建中的场景` 并排在第 1 位，把 `SampleScene` 的勾去掉（这一步同时修掉 P0-1：现在 Build 出来会是黑屏）
6. `项目` 窗口里右键 `Scenes/SampleScene` → `删除`，以后就不会再点错

> 补充：`层级` 标题栏出现星号（`SampleScene*`）= 当前场景有**未保存**的改动。切场景前先把要留的按 `Ctrl+S` 存了。

---

<a id="附f-修构建场景列表"></a>
## 附F 一次性收尾：改构建场景列表 + 删空场景

**目的**：让 Build 出来的是真场景（否则黑屏），并且以后不会再点错空场景。**只做一次。**

**做之前确认**：`层级` 最上面那行显示的是 `Main`（即 `Assets/Main.unity` 已打开）。不是就先按附E 切回来。

### 第 1 步 · 打开生成设置

`Ctrl+Shift+B`（或 `文件 → 生成设置`）。

现在列表里只有一行：`Assets/Scenes/SampleScene.unity`（就是那个空场景）。

### 第 2 步 · 把 Main.unity 加进列表

点列表左下方那个按钮 **`添加已打开的场景 Add Open Scenes`**。

按钮点不动（灰的）？说明当前场景没保存过 → 先在层级里点一下 → `Ctrl+S` → 再点。

> 也可以直接从 `项目` 窗口把 `Main` **拖**到场景列表里，效果相同。

列表里应出现第二行 `Assets/Main.unity`。

### 第 3 步 · 把 Main.unity 排到第 1 位

**第 1 个场景＝游戏启动时加载的场景**，必须是 `Main.unity`。

按住 `Main.unity` 那一行**最左边的小把手**（六点图标）往上拖到第一行。

### 第 4 步 · 移除 SampleScene

选中 `Assets/Scenes/SampleScene.unity` 那一行 → 按 `Delete` 键（或右键 → `移除 Remove`）。

**结果**：列表里只剩 `Assets/Main.unity`，前面有勾，排在第 1 位。

### 第 5 步 · 关掉生成设置窗口 → `Ctrl+S` 保存场景

### 第 6 步 · 删掉空场景文件

1. `项目` 窗口 → 左侧点开 `Assets` → 点 `Scenes` 文件夹
2. 选中 `SampleScene` → 按 `Delete`
3. 弹「是否删除该资源？」→ 点 `删除 Delete`

删完 `Scenes` 文件夹是空的，留着没影响（以后放场景用）。

### 第 7 步 · 顺手把主场景挪进 Scenes 文件夹（可选，规范）

`项目` 窗口里把根目录的 `Main` **拖到** `Scenes` 文件夹上。Unity 会自动更新引用和构建列表里的路径，不用手动改。

### ✔ 验证

1. 再按一次 `Ctrl+Shift+B`
2. 列表应只有一行 `Assets/Main.unity`（若做了第 7 步则显示 `Assets/Scenes/Main.unity`），有勾、第 1 位
3. `项目` 窗口里已经找不到 `SampleScene`

**易错点**：

| 现象 | 原因 | 处理 |
|---|---|---|
| `添加已打开的场景` 是灰的 | 当前场景未保存 | `Ctrl+S` 后再点 |
| 删了 SampleScene 后生成设置里留了一行黄色/红色 | 先删了文件才改顺序 | 选中那行按 `Delete` 移除即可 |
| 拖不动顺序 | 没抓住最左边的小把手 | 按住六点图标那一列拖动 |

---

<a id="附g-webgl模块假已安装"></a>
## 附G "Hub 显示 WebGL 已安装，其实没装"

**判断（不用打开 Unity，看文件就行）**

在资源管理器里进到
`E:\Unity Hub\2022.3.62f3c1\Editor\Data\PlaybackEngines\`

- 只有 `windowsstandalonesupport` → **WebGL 没装**
- 多了 `WebGLSupport` → WebGL 装好了

**为什么 Hub 会说"已安装"**：Hub 读的是安装目录下的 `E:\Unity Hub\2022.3.62f3c1\modules.json` 里记的**勾选清单**，不是磁盘文件。那份清单里 webgl 是 `selected:true`，所以 Hub 打勾 —— 但文件从没落盘。同一个清单里 `documentation` 也是 `selected:true`，而 `Editor\Data\Documentation` 目录其实不存在，同理。

**修复（本地已有安装器，不用重新下载）**

> ⚠️ **必须先完全退出 Unity 编辑器和 Unity Hub** —— 文件被占用会让模块装不完整。这大概率就是当初没装上的原因。

1. Unity 里 `Ctrl+S` 存场景 → 关掉 Unity → 关掉 Unity Hub
2. 到 `E:\Unity Hub\` → 双击 **`UnitySetup-WebGL-Support-for-Editor-2022.3.62f3c1.exe`**（560 MB，已下载好）
3. 若问编辑器目录 → 选 **`E:\Unity Hub\2022.3.62f3c1\Editor`**（含 `Unity.exe` 的那一层）
4. 装完回到附G开头那一步验证：`PlaybackEngines` 下应出现 `WebGLSupport`
5. 重开 Unity → `文件 → 生成设置` → 平台列表里 `WebGL` 可选中 → 进 M6

**Hub 里那条勾是灰的（改不了）**：Hub 不支持"移除模块"，所以只能走上面的手动安装器。

**安装器说"找不到 Unity"**：只能卸载该编辑器版本后重装，重装时勾上 `WebGL Build Support`（要重下 3.7 GB）。

**安装器文件坏了**：重新下载
`https://download.unitychina.cn/download_unity/1623fc0bbb97/TargetSupportInstaller/UnitySetup-WebGL-Support-for-Editor-2022.3.62f3c1.exe`

**⚠️ 装完一定要复核装到了哪个编辑器**：安装器会读注册表里的 `Location x64`，可能装进**独立版**编辑器（`E:\Unity Hub\Editor`）而不是 Hub 管理版（`E:\Unity Hub\2022.3.62f3c1\Editor`）——后者才是项目实际用的。

对比这两行，看 `WebGLSupport` 出现在哪边：

```
E:\Unity Hub\Editor\Data\PlaybackEngines\                        ← 独立版（不一定是项目用的）
E:\Unity Hub\2022.3.62f3c1\Editor\Data\PlaybackEngines\          ← Hub 管理版（项目用的这个）
```

**若项目用的那边没有**：把已经装好的整个 `WebGLSupport` 文件夹复制过去即可（本工程已于 2026-10-16 这样修好，12380 个文件 / 2.6 GB，复制后两侧文件数与体积一致）。
