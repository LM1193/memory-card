# -*- coding: utf-8 -*-
"""生成实验报告正文的 block 清单，并分批写入学院模板 docx 的表格单元格。

用法：python tools/gen_report.py [--build-only]
分批写入时每批前重新查询单元格末尾坐标，避免坐标漂移。
"""
import io
import json
import os
import subprocess
import sys
import unicodedata

ROOT = r"E:/unitwork/Memory Card"
SHOTS = ROOT + "/docs/screenshots/"
SCRIPTS = ROOT + "/Assets/Scripts/"
EDSDK_DIR = r"E:/WorkBuddy/WorkBuddy/resources/app.asar.unpacked/resources/plugins/workbuddy-builtin/skills/workbuddy-local-office-edit"
FILE_ID = "D://实验二_记忆翻牌.docx"
PY = sys.executable

BODY_FONT = {"font_family": "宋体", "font_size": 10.5}
CODE_FONT = {"font_family": "Consolas", "font_size": 7.5}
SEC_FONT = {"font_family": "黑体", "font_size": 12, "bold": True}
CAP_FONT = {"font_family": "宋体", "font_size": 9}

# ---- 代码块排版：单元格可用宽度与软折行 ----
# 实测（PDF 边界框）：单元格左右边框 x=115.2 / 510.7，左右内边距各 5.4pt → 可用 384.7pt
CELL_WIDTH_PT = 384.7
CELL_MARGIN_PT = 5.4
CODE_ADV = 0.55 * CODE_FONT["font_size"]   # Consolas 单字符步进（em 的 0.55）


def _cw(ch):
    """单个字符的显示宽度（pt）：中韩全角按 1em，其余按 Consolas 步进。"""
    return CODE_FONT["font_size"] if unicodedata.east_asian_width(ch) in "WF" else CODE_ADV


def _w(s):
    return sum(_cw(c) for c in s)


def _hard_wrap(s, avail_pt):
    """把一段文本按可用宽度切行：优先在标点/空格后断开，尽量不切断英文单词。"""
    out = []
    while _w(s) > avail_pt:
        acc = 0.0
        cut = 0
        for i, ch in enumerate(s):
            acc += _cw(ch)
            if acc > avail_pt:
                break
            cut = i + 1
        if cut <= 0:
            cut = 1
        # 1) 回退到最近的天然断点（标点后）
        best = -1
        for i in range(cut, 0, -1):
            if s[i - 1] in " ，；、。）》】:：,;+":
                best = i
                break
        if best >= cut * 0.45:
            cut = best
        else:
            # 2) 退而求其次：不切断英文标识符
            j = cut
            while j > 0 and (s[j - 1].isalnum() or s[j - 1] in "_.$"):
                j -= 1
            if j >= cut * 0.45:
                cut = j
        out.append(s[:cut].rstrip())
        s = s[cut:].lstrip()
    out.append(s)
    return out or [""]


def _split_comment(line):
    """把行尾 // 注释切出来（跳过字符串内的 //）。"""
    in_str = False
    for i in range(len(line) - 1):
        c = line[i]
        if c == '"' and (i == 0 or line[i - 1] != "\\"):
            in_str = not in_str
        elif not in_str and c == "/" and line[i + 1] == "/":
            return line[:i], line[i:]
    return line, ""


def _wrap_code_line(line):
    """代码行超过单元格宽度时软折行：续行缩进 +8，注释续行补 // 前缀。"""
    # 留 5pt 安全余量：按实测宽度排版时仍有个别行被 Word 自己折行
    limit = CELL_WIDTH_PT - CELL_MARGIN_PT * 2 - 5
    if _w(line) <= limit:
        return [line]
    indent = len(line) - len(line.lstrip(" "))
    base, cont = " " * indent, " " * (indent + 8)
    code_part, comment = _split_comment(line)
    out = []
    body = code_part.strip()
    if body:
        # 首行按 base 缩进排，续行按更宽的 cont 缩进，二次校验避免溢出
        segs = _hard_wrap(body, limit - _w(base))
        out.append(base + segs[0])
        for seg in segs[1:]:
            for s2 in _hard_wrap(seg, limit - _w(cont)):
                out.append(cont + s2)
    if comment:
        pre = cont if out else base
        # /// 文档注释的续行仍以 /// 开头，否则会被读成普通注释
        mark = "/// " if comment.startswith("///") else "// "
        for k, seg in enumerate(_hard_wrap(comment.strip(), limit - _w(pre))):
            out.append(pre + (seg if k == 0 else mark + seg))
    return out or [line]



def P(text, fmt=None, prop=None):
    args = {"text": text}
    if fmt:
        args["text_format"] = fmt
    if prop:
        args["paragraph_property"] = prop
    return {"block": "paragraph", "calls": [{"tool": "doc_insert_paragraph_with_text", "args": args}]}


def sec(t):
    return P(t, SEC_FONT, {"jc": "left", "spacing_before": 12, "spacing_after": 4,
                           "line_spacing": 1.25, "line_spacing_rule": 1})


def body(t):
    return P(t, BODY_FONT, {"jc": "both", "spacing_before": 0, "spacing_after": 0,
                            "line_spacing": 1.25, "line_spacing_rule": 1})


def code(t):
    # 先按单元格宽度软折行，再用 \v（Word 行内换行）拼回一段
    # 引擎会丢弃 \n，写入后是 <w:br w:type="textWrapping"/>
    lines = []
    for raw in t.replace("\t", "    ").split("\n"):
        lines.extend(_wrap_code_line(raw))
    return P("\v".join(lines), CODE_FONT, {"jc": "left", "spacing_before": 2, "spacing_after": 2,
                                           "line_spacing": 1.0, "line_spacing_rule": 1})


def img(path, w=440, h=248):
    return {"block": "paragraph", "calls": [{"tool": "doc_insert_paragraph_with_text", "args": {
        "runs": [{"type": "image", "image_path": path, "w": w, "h": h}],
        "paragraph_property": {"jc": "center", "spacing_before": 6, "spacing_after": 2}}}]}


def cap(t):
    return P(t, CAP_FONT, {"jc": "center", "spacing_before": 0, "spacing_after": 8})


def code_file(path, chunk=42):
    lines = io.open(path, encoding="utf-8").read().replace("\r\n", "\n").rstrip("\n").split("\n")
    out = []
    for i in range(0, len(lines), chunk):
        out.append(code("\n".join(lines[i:i + chunk])))
    return out


# ---------------------------------------------------------------- 正文：第一、二章
def blocks_part1():
    b = []
    b.append(sec("一、实验环境与工程结构"))
    b.append(body("本实验在 Windows 11 + Unity 2022.3.62f3c1 LTS（内置渲染管线）下完成，全部逻辑用 C# 实现，"
                  "构建目标为 Windows x64。工程名 MemoryCard，主场景 Assets/Main.unity 是唯一的运行场景，"
                  "且已加入 Build Settings 的场景列表。"))
    b.append(body("场景中的根物体共 7 个，职责划分如下："))
    b.append(body("· Main Camera：正交相机，Orthographic Size = 5.8，Position = (0, 2.9, -10)，负责把整副牌完整框入画面；"))
    b.append(body("· Background：背景 SpriteRenderer，用牌桌贴图铺满视野，渲染层级低于卡牌；"))
    b.append(body("· Board：挂 BoardManager，作为卡牌父节点，只负责按公式摆放卡牌；"))
    b.append(body("· GameManager：流程控制，负责计时、翻牌、配对判定、计分与胜负；"))
    b.append(body("· Canvas：挂 UIManager，屏幕空间 Overlay 画布，承载 TIME / SCORE / MOVES 三个读数、开始面板、胜利面板与 RESTART 按钮；"))
    b.append(body("· EventSystem：UI 事件宿主，同时用于判断「点击是否落在 UI 上」；"))
    b.append(body("· Music：挂 MusicPlayer 与独立 AudioSource，专管循环背景音乐。"))
    b.append(body("脚本共 7 个（Assets/Scripts）：BoardManager（牌阵生成与洗牌入口）、Card（单张卡牌的状态与翻面）、"
                  "GameManager（主流程）、UIManager（界面刷新）、Sfx（程序化短音效）、MusicPlayer（背景音乐循环）、"
                  "ShuffleUtil（洗牌静态工具）。另有 3 个编辑器辅助脚本（BuildTools 一键安全构建、ProjectValidator 工程自检、"
                  "ShotTools 截图工具）只用于开发调试，不参与游戏逻辑。"))

    b.append(sec("二、关键设计：坐标公式、牌数与取景"))
    b.append(body("卡牌不是 UGUI 元素，而是场景里带 SpriteRenderer 的真实 2D 物体，位置完全由课件第 7 页的公式算出，"
                  "没有使用 GridLayoutGroup 这类自动布局组件："))
    b.append(code("(x, y) = (offsetX * j + originalX,  offsetY * i + originalY)\n"
                  "// i 为行号（0 起，自下而上）；j 为列号（0 起，自左向右）"))
    b.append(body("4×4 的实际参数为 rows = 4、cols = 4、originalX = -3、originalY = -1、offsetX = 2、offsetY = 2.5。"
                  "代入后 16 个落点的 x ∈ [-3, 3]、y ∈ [-1, 6.5]；卡牌世界尺寸 1.5 × 2（150 × 200 像素 ÷ PPU 100），"
                  "因此牌阵外框为 7.5 × 9.5。"))
    b.append(body("相机按外框反推：牌阵上沿 7.5、下沿 -2。取 Orthographic Size = 5.8、中心 y = 2.9，"
                  "视口 y ∈ [-2.9, 8.7]，上方留白 1.2、下方留白 0.9。上方多留的余量是给顶栏三行读数让位，"
                  "实测读数与第一排牌不再重叠。"))
    b.append(body("numbers 数组由 BuildNumbers() 用整数除法 k / 2 生成，4×4 时自动得到 "
                  "{0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7}；rows、cols 一改，牌数与图案对数同步变化，逻辑无需改动。"))
    b.append(body("本文 5 张运行截图全部截自直接双击运行 build_win\\MemoryCard.exe 的录屏，画面里只有游戏窗口，"
                  "不含任何编辑器面板。"))
    b.append(img(SHOTS + "21_运行截图_牌阵就绪.png", h=275))
    b.append(cap("图 1　exe 运行时的 4 × 4 牌阵（16 张牌、8 种图案各 2 张；TIME 0.3、SCORE 0、MOVES 0）"))

    b.append(sec("三、功能的实现（对应实验要求 8 条）"))
    b.append(P("（1）新建 Unity 2D 工程并搭建场景", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                                     "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("新建 2D 工程 MemoryCard，把 Memory 资源包中的牌面、卡背、牌桌、开始按钮与胜利插图导入 Assets/Art，"
                  "9 张卡牌贴图统一设为 Sprite（Single）、Pixels Per Unit = 100、勾选 Alpha Is Transparency。"
                  "按上表挂载脚本、建立 Canvas 与两个面板后进入运行，首先出现的是开始面板。"))
    b.append(img(SHOTS + "20_运行截图_开始界面.png", h=275))
    b.append(cap("图 2　exe 运行后的开始界面（顶栏三个读数已就位，点击中间 Start 开始）"))

    b.append(P("（2）用代码生成 4×4 = 16 张卡牌", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                                  "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("牌阵在 StartGame 时按「生成 numbers → 洗牌 → 铺牌」三步构建。铺牌是双重循环，外层行 i、内层列 j，"
                  "位置用公式算、图案用 numbers[i * cols + j] 取 images 下标，与课件第 7、11 页一致："))
    b.append(code("for (int i = 0; i < rows; i++) {\n"
                  "    for (int j = 0; j < cols; j++) {\n"
                  "        float x = offsetX * j + originalX;       // 课件第 7 页布局公式\n"
                  "        float y = offsetY * i + originalY;\n"
                  "        int index = _numbers[i * cols + j];      // 课件第 11 页取图案\n"
                  "        Card card = Instantiate(cardPrefab, boardRoot);\n"
                  "        card.name = $\"Card_{i}_{j}\";\n"
                  "        card.transform.position = new Vector3(x, y, 0f);\n"
                  "        card.Init(index, images[index], backSprite, cardSortingOrder, onCardClicked);\n"
                  "        _cards.Add(card);\n"
                  "    }\n"
                  "}"))
    b.append(body("16 张牌全部由数组驱动，没有一张是手动拖进场景的；重开一局时 ClearBoard() 先销毁旧卡再重建，不会残留。"))

    b.append(P("（3）洗牌算法", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                    "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("采用 Fisher–Yates 洗牌：从前往后遍历，每次在 [i, length) 区间取随机下标 r 并与 i 交换，"
                  "保证每种排列等概率。Random.Range 的整型重载上界是开区间，必须传 array.Length 才能覆盖最后一个元素，"
                  "这是最容易写错的一处。"))
    b.append(code("for (int i = 0; i < array.Length; i++) {\n"
                  "    int r = Random.Range(i, array.Length);   // 上界开区间，故传 Length\n"
                  "    T tmp = array[i]; array[i] = array[r]; array[r] = tmp;\n"
                  "}"))
    b.append(body("洗牌只打乱 numbers 数组，不动卡牌物体；洗牌发生在铺牌之前，所以每次运行、每次重开的图案分布都不同。"))

    b.append(P("（4）点击翻牌与配对判定", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                          "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("点击由 Card.OnMouseDown 接收（正交相机 + BoxCollider2D + 鼠标射线自动触发，不需要 UGUI Button）。"
                  "这里有一个必须处理的细节：UGUI 不会自动拦截世界物体的 OnMouseDown，点在面板或按钮上时事件会穿透到"
                  "面板下方的卡牌，所以要先判断指针是否停在 UI 上："))
    b.append(code("private void OnMouseDown() {\n"
                  "    if (EventSystem.current != null) {\n"
                  "        bool overUI = Input.touchCount > 0\n"
                  "            ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)\n"
                  "            : EventSystem.current.IsPointerOverGameObject();\n"
                  "        if (overUI) return;            // 点在 UI 上，不处理本次点击\n"
                  "    }\n"
                  "    OnClick();\n"
                  "}"))
    b.append(body("翻开两张后进入配对判定协程。id 相等则两张牌锁定为 Matched 并加分；不相等则播放失败音效、扣分，"
                  "再通过 yield return new WaitForSeconds(1.5f) 停 1.5 秒后翻回背面。判定期间 _isChecking 为真，"
                  "新的点击会被拒绝，避免连点把状态打乱。"))
    b.append(code("private IEnumerator CheckMatch() {\n"
                  "    _isChecking = true;\n"
                  "    if (_firstRevealed.id == _secondRevealed.id) {        // 课件第 13 页：id 相等即配对成功\n"
                  "        _firstRevealed.SetMatched(); _secondRevealed.SetMatched();\n"
                  "        Sfx.PlayMatch(); _score += scorePerMatch; _matchedPairs++;\n"
                  "        if (_matchedPairs >= board.PairCount) GameOver();\n"
                  "    } else {\n"
                  "        Sfx.PlayMismatch();\n"
                  "        _score = Mathf.Max(0, _score - penaltyPerMismatch);\n"
                  "        yield return new WaitForSeconds(flipBackDelay);    // 课件第 14 页：延时 1.5 秒\n"
                  "        _firstRevealed.Hide(); _secondRevealed.Hide();\n"
                  "    }\n"
                  "    _isChecking = false;\n"
                  "}"))
    b.append(img(SHOTS + "22_运行截图_配对成功.png", h=275))
    b.append(cap("图 3　配对成功：两张同图案牌保持正面，SCORE 由 0 增至 10（TIME 8.0、MOVES 3）"))
    b.append(img(SHOTS + "23_运行截图_配对失败.png", h=275))
    b.append(cap("图 4　配对失败：两张不同图案牌翻开后停留 1.5 秒再翻回，SCORE 由 20 扣至 18（TIME 12.0、MOVES 5）"))
    return b


# ---------------------------------------------------------------- 正文：功能 5-8
def blocks_part2():
    b = []
    b.append(P("（5）计分与步数统计", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                       "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("计分规则做成可配置参数：配对成功 +10，配对失败 -2，并用 Mathf.Max(0, ...) 保底不为负；"
                  "步数在翻开第二张牌时 +1，即「一次配对尝试 = 1 步」。两个数值与用时一起实时刷新到顶栏，"
                  "全部配对完成后汇总到胜利面板。"))
    b.append(code("_score += scorePerMatch;                          // 成功 +10\n"
                  "_score = Mathf.Max(0, _score - penaltyPerMismatch);// 失败 -2，不为负\n"
                  "_moves++;                                         // 翻开第二张即计 1 步\n"
                  "if (ui != null) { ui.UpdateScore(_score); ui.UpdateMoves(_moves); }"))
    b.append(img(SHOTS + "24_运行截图_胜利界面.png", h=275))
    b.append(cap("图 5　全部配对完成：TIME 48.5 s、SCORE 60、MOVES 20，BEST 48.5 s 并标注 NEW!，下方为 RESTART 按钮"))

    b.append(P("（6）音效系统：循环背景音乐 + 翻牌音 + 配对成功音", SEC_FONT,
               {"jc": "left", "spacing_before": 10, "spacing_after": 2, "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("音频分成两条互不干扰的通道。背景音乐是导入的 20 秒无缝循环 wav，挂在 Music 物体的独立 AudioSource 上并勾选 Loop，"
                  "由 MusicPlayer 负责淡入与胜利时压低音量；翻牌、配对成功、配对失败、胜利四个短音由 Sfx 用正弦波 + 指数衰减包络"
                  "程序化合成，通过 PlayOneShot 播放。之所以分成两个 AudioSource，是因为 PlayOneShot 之间会互相打断，"
                  "若 BGM 与短音共用一个源，一次翻牌就可能把循环中的背景音乐掐掉。"))
    b.append(body("关于循环点：背景音乐按 96 BPM、8 小节（20.000 秒）合成，采样数 441000 能被采样率整除；"
                  "关键是把缓冲区末尾溢出 1.5 秒的尾音「绕回」开头叠加，相当于上一轮的余音仍在响，"
                  "因此循环点不需要淡入淡出。实测首尾样本差 |s[0] - s[N-1]| = 0.0073（满量程 1.0），人耳不可闻。"))
    b.append(body("音频三件套与实验要求的对应：背景音乐 bgm_loop.wav（AudioSource.loop = true）；翻牌音效 Sfx.PlayFlip()"
                  "（620→900 Hz，0.055 秒）；配对成功音效 Sfx.PlayMatch()（880→1320 Hz，0.16 秒）。"
                  "另外补了配对失败音（300→170 Hz）与胜利音（660→1320 Hz），让操作反馈更完整。"))
    b.append(code("// MusicPlayer.Awake：三项关键配置\n"
                  "source.clip = bgm;\n"
                  "source.loop = true;        // ← 循环播放背景音乐\n"
                  "source.spatialBlend = 0f;  // 纯 2D，不随距离衰减\n"
                  "source.Play();\n\n"
                  "// Sfx：程序化合成的短音，不依赖任何音频素材\n"
                  "_flip  = MakeTone(\"sfx_flip\",  620f,  900f, 0.055f, 0.9f);\n"
                  "_match = MakeTone(\"sfx_match\", 880f, 1320f, 0.16f,  1.0f);\n"
                  "_source.PlayOneShot(clip, _volume);"))

    b.append(P("（7）重新开始按钮", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                      "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("开始面板的 Start 与胜利面板的 RESTART 都调用 GameManager.StartGame()。该方法先 StopAllCoroutines() 并清空 "
                  "_firstRevealed、_secondRevealed、_isChecking，防止上一局的「翻回」协程在新一局里继续执行；"
                  "然后重置分数、步数、计时与胜负标记，再重新生成、洗牌、铺牌。因此任何时刻重开都不会出现"
                  "「刚重开就有两张牌自动翻回去」的现象。"))

    b.append(P("（8）导出 exe", SEC_FONT, {"jc": "left", "spacing_before": 10, "spacing_after": 2,
                                   "line_spacing": 1.25, "line_spacing_rule": 1}))
    b.append(body("构建目标设为 Windows x64，产物为 build_win/MemoryCard.exe（同目录包含 MemoryCard_Data 与 MonoBleedingEdge "
                  "运行时）。为避免「脚本还在编译时点 Build 导致构建失败」，工程里加了 BuildTools："
                  "先用 delayCall 轮询等待 isCompiling / isUpdating / isPlaying 三者全为假，再执行 BuildPipeline.BuildPlayer。"))
    b.append(body("构建记录：结果 Succeeded，耗时 2.7 秒，产出 149 个文件、共 74.26 MB，错误 0、警告 0。"
                  "体积主要由 UnityPlayer.dll（30.5 MB）与 Mono 运行时（7.7 MB）构成，背景音乐仅占 0.84 MB。"))
    return b


# ---------------------------------------------------------------- 完整源代码
def blocks_source():
    b = [sec("四、完整源代码")]
    b.append(body("以下 7 个脚本即工程 Assets/Scripts 下的全部运行时代码，注释按原样保留。"))
    files = [
        ("4.1  ShuffleUtil.cs —— Fisher–Yates 洗牌", "ShuffleUtil.cs"),
        ("4.2  BoardManager.cs —— 牌阵生成（课件第 7、9、10、11 页）", "BoardManager.cs"),
        ("4.3  Card.cs —— 单张卡牌的状态与翻面动画", "Card.cs"),
        ("4.4  GameManager.cs —— 主流程：计时、配对判定、计分、胜负", "GameManager.cs"),
        ("4.5  UIManager.cs —— 顶栏读数与两个面板", "UIManager.cs"),
        ("4.6  Sfx.cs —— 程序化短音效合成", "Sfx.cs"),
        ("4.7  MusicPlayer.cs —— 循环背景音乐", "MusicPlayer.cs"),
    ]
    for title, fn in files:
        b.append(P(title, SEC_FONT, {"jc": "left", "spacing_before": 12, "spacing_after": 3,
                                     "line_spacing": 1.25, "line_spacing_rule": 1}))
        b.extend(code_file(SCRIPTS + fn))
    return b


# ---------------------------------------------------------------- 自检
def blocks_check():
    b = [sec("五、自检与验收")]
    b.append(body("工程内置 ProjectValidator（菜单 Tools / Memory Card / Validate Project），一次性核对缺失脚本、"
                  "管理器唯一性、序列化引用是否为空、牌面数组完整性、预制体组件、构建场景、平台模块、贴图 PPU、"
                  "相机取景覆盖、音频配置等 20 项。最近一次运行结果：结论 PASS，通过 20 项、警告 0、失败 0。"))
    b.append(code("结论     : PASS   通过 20 / 警告 0 / 失败 0\n"
                  "✓ 场景 `Main` 无缺失脚本。\n"
                  "✓ GameManager / BoardManager / UIManager 存在且唯一。\n"
                  "✓ GameManager 的 3 个必需引用均已赋值。\n"
                  "✓ UIManager 的 9 个必需引用均已赋值。\n"
                  "✓ BoardManager.images 共 8 张，均已赋值。\n"
                  "✓ 牌阵 4 × 4 = 16 张，共 8 对。\n"
                  "✓ MusicPlayer 的 2 个必需引用均已赋值。\n"
                  "✓ 背景音乐 AudioSource 已勾选 Loop（循环播放）。\n"
                  "✓ BGM 导入设置正确：Assets/Audio/bgm_loop.wav，PCM / Decompress On Load，时长 20.0 s。\n"
                  "✓ Card 预制体 `Card` 组件齐全（Card + SpriteRenderer + BoxCollider2D）。\n"
                  "✓ Windows 构建模块可用（可导出 exe）。\n"
                  "✓ 9 张卡牌精灵 PPU 均为 100，与 1.5 × 2 世界尺寸约定一致。\n"
                  "✓ 相机取景覆盖牌阵：牌阵 7.50 × 9.50，视野 20.66 × 11.60（aspect 1.78）。"))
    b.append(body("验收时逐项对照实验要求：4×4 十六张、八种图案各两张、洗牌、点击翻牌、成功保持 / 失败延时翻回、"
                  "计分与步数、全部配对后结束、循环背景音乐 + 两种音效、重新开始按钮、导出 exe，均已实现并通过运行确认。"))
    return b


def blocks_summary():
    b = [body("本次实验从零完成了一个 4×4 的记忆翻牌游戏，8 条功能要求全部落地，工程自检 20 项全部通过。"
              "过程中遇到的问题与解决办法主要有以下几处。")]
    items = [
        "1. 改成 4 行 4 列后顶排卡牌被裁掉。按原 3 行布局的相机参数，视口上沿只到 7.0，而第 4 排会排到 7.5，直接出画。"
        "解决办法是先用 BoardManager.GetBoardWorldSize() 算出牌阵外框 7.5 × 9.5，再反推相机：Orthographic Size 提到 5.8、"
        "中心 y 提到 2.9，视口变为 y ∈ [-2.9, 8.7]，上下分别留白 1.2 与 0.9，并用 1280 × 720 实拍确认 16 张牌完整入镜。",
        "2. 点击开始面板时，下面的卡牌也被翻开。一开始怀疑是坐标算错，后来才确认 UGUI 不会拦截世界物体的 OnMouseDown，"
        "事件会穿透。解决办法是在 Card.OnMouseDown 里用 EventSystem.current.IsPointerOverGameObject() 判断指针是否停在 UI 上，"
        "是则直接返回；同时面板的 Image 必须保留 Raycast Target，否则这个判断会失效。",
        "3. 加了背景音乐后，一响翻牌音效背景音乐就断。原因是 BGM 与音效共用了 GameManager 上的同一个 AudioSource，"
        "PlayOneShot 之间互相打断。解决办法是把 BGM 挪到独立物体 Music 上单独一个 AudioSource，音效仍走 PlayOneShot，"
        "两条通道互不干扰，胜利时再把 BGM 压低到 35% 让胜利音突出。",
        "4. 背景音乐每次循环到接缝处有轻微「咔」声。原因是合成时最后一个音符的尾音被缓冲区末尾硬切。"
        "解决办法是把缓冲区末尾 1.5 秒的溢出声绕回开头叠加，使循环点成为连续信号；最终首尾样本差只有 0.0073，听不到接缝。",
        "5. 运行时静态引用偶尔变 null，表现为「点 Start 没反应」。原因是播放中若发生脚本重编译，Unity 会重载域并清空静态字段，"
        "但场景物体仍然存活。解决办法是把 GameManager.Instance 改成惰性自愈：为空时按需在场景里重新查找，域重载后仍能工作。",
        "6. 后台自动构建不生效。构建工具里的等待逻辑用 EditorApplication.delayCall 轮询，编辑器窗口在后台时该回调不执行，"
        "等待永远推进不下去。解决办法是改用同步调用构建，并在入口先检查编译、导入、播放三种状态。",
    ]
    for t in items:
        b.append(body(t))
    b.append(body("通过这次实验，对「数组驱动 + 公式定位」这种做法的好处体会比较深：牌数、图案种类、间距都只是参数，"
                  "改 4×4 只需要改 rows、cols 与 images 长度，核心逻辑一行没动。另外音频、UI、物理点击这些看起来独立的部分，"
                  "真正联调时问题往往出在它们的交界处——UI 与 3D 点击的穿透、两条音频通道的抢占，都是交界处的问题。"
                  "下一步计划给卡牌翻面加更细腻的动画曲线，并尝试把最高分纪录做成可切换难度。"))
    return b


BATCHES = [
    ("正文 1/5：环境、关键设计、功能 1-4", "row6", blocks_part1),
    ("正文 2/5：功能 5-8", "row6", blocks_part2),
    ("正文 3/5：完整源代码", "row6", blocks_source),
    ("正文 4/5：自检与验收", "row6", blocks_check),
    ("实验总结", "row7", blocks_summary),
]


def edsdk(tool, args, timeout=180):
    path = os.path.join(os.environ.get("TEMP", "."), "edsdk_args.json")
    io.open(path, "w", encoding="utf-8").write(json.dumps(args, ensure_ascii=False))
    p = subprocess.run([PY, "edsdk.py", "call", tool, "--json-file", path],
                       cwd=EDSDK_DIR, capture_output=True, text=True, encoding="utf-8", timeout=timeout)
    return p.stdout.strip() or p.stderr.strip()


def table_meta():
    """表格元信息。注意 table_id 会随文档重新导入而变化，必须每次现查，不能写死。"""
    out = edsdk("doc_list_tables", {"file_id": FILE_ID}, timeout=60)
    return json.loads(out.splitlines()[0])["tables"][0]


def cell_range(row, meta=None):
    meta = meta or table_meta()
    for c in meta["editable_cells"]:
        if c["row"] == row and c["col"] == 2:
            return c["start"], c["end"]
    raise RuntimeError("找不到单元格 %d,2" % row)


def cell_end(row):
    return cell_range(row)[1]


def main():
    build_only = "--build-only" in sys.argv
    only = None
    if "--only" in sys.argv:
        only = int(sys.argv[sys.argv.index("--only") + 1])
    if not build_only and only is None:
        # 清空两个待填写单元格，避免新旧内容叠加
        meta = table_meta()
        print("表格 table_id =", meta["table_id"])
        print(edsdk("doc_set_table_cells", {"file_id": FILE_ID, "table_id": meta["table_id"],
                                           "cells": [{"row": 6, "col": 2, "text": ""},
                                                     {"row": 7, "col": 2, "text": ""}]}))
        # 清空校验：单元格若仍有内容说明清空失败，继续写会造成重复，直接中止
        meta = table_meta()
        for r in (6, 7):
            s, e = cell_range(r, meta)
            if e - s > 60:
                raise SystemExit("清空失败：单元格 %d,2 仍有 %d 个字符，已中止" % (r, e - s))
        print("清空校验通过")
    for i, (name, row, fn) in enumerate(BATCHES):
        if only is not None and i != only:
            continue
        blocks = fn()
        idx = 6 if row == "row6" else 7
        pos = cell_end(idx)
        args = {"file_id": FILE_ID, "idx": pos, "blocks": blocks}
        print("== %s : %d 块, idx=%d" % (name, len(blocks), pos))
        print(edsdk("doc_batch_execute", args, timeout=600)[:600])


if __name__ == "__main__":
    main()
