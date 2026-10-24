# -*- coding: utf-8 -*-
"""对 D:\\实验二_记忆翻牌.docx 做只读体检（不修改文件）。"""
import re
import sys
import zipfile
from pathlib import Path

DOCX = Path(r"D:\实验二_记忆翻牌.docx")
ROOT = Path(r"E:\unitwork\Memory Card")

issues = []
warns = []
oks = []


def ok(msg):
    oks.append(msg)


def warn(msg):
    warns.append(msg)


def bad(msg):
    issues.append(msg)


# ---------- 1. 包结构 ----------
z = zipfile.ZipFile(DOCX)
bad_zip = z.testzip()
if bad_zip:
    bad(f"压缩包损坏：{bad_zip}")
else:
    ok(f"zip 结构完好，{len(z.namelist())} 个部件")

names = z.namelist()
doc_xml = z.read("word/document.xml").decode("utf-8")
app_xml = z.read("docProps/app.xml").decode("utf-8") if "docProps/app.xml" in names else ""

m = re.search(r"<Pages>(\d+)</Pages>", app_xml)
words = re.search(r"<Words>(\d+)</Words>", app_xml)
chars = re.search(r"<Characters>(\d+)</Characters>", app_xml)
pages = int(m.group(1)) if m else -1
ok(f"页数 {pages}，字数 {words.group(1) if words else '?'}，字符 {chars.group(1) if chars else '?'}")

media = [n for n in names if n.startswith("word/media/")]
ok(f"内嵌图片 {len(media)} 个：" + ", ".join(sorted(Path(x).name for x in media)))

# ---------- 2. 纯文本 ----------
# w:t 文本 + w:br 视作换行
tmp = doc_xml.replace('<w:br w:type="textWrapping"/>', "\n").replace("<w:br/>", "\n")
tmp = re.sub(r"</w:p>", "\n", tmp)
texts = re.findall(r"<w:t[^>]*>(.*?)</w:t>", tmp, re.S)
plain = "".join(texts)
for a, b in [("&amp;", "&"), ("&lt;", "<"), ("&gt;", ">"), ("&quot;", '"'), ("&apos;", "'")]:
    plain = plain.replace(a, b)

# 表格单元格分段（粗略：按 tc 切，便于判断哪个格子空）
tcs = re.findall(r"<w:tc>.*?</w:tc>", doc_xml, re.S)
cell_texts = []
for tc in tcs:
    t = "".join(re.findall(r"<w:t[^>]*>(.*?)</w:t>", tc, re.S))
    for a, b in [("&amp;", "&"), ("&lt;", "<"), ("&gt;", ">")]:
        t = t.replace(a, b)
    cell_texts.append(t.strip())
ok(f"表格单元格 {len(cell_texts)} 个")

# ---------- 3. 占位符残留 ----------
# 只查真正未填的标记；「略」「占位」等单字会误伤「忽略」「策略」「占位尺寸」等正常词
ph = ["学生填写", "待填写", "待补充", "此处填写", "请填写", "TODO", "TBD", "XXX", "lorem"]
hit = [p for p in ph if p in plain]
if hit:
    bad(f"残留占位符/未填标记：{hit}")
else:
    ok("无占位符残留（学生填写 / 待补充 / TODO 等）")

# ---------- 4. 空单元格 ----------
empty = [i for i, t in enumerate(cell_texts) if not t]
if empty:
    warn(f"空单元格 {len(empty)} 个（索引 {empty}，按 tc 顺序）")
else:
    ok("无空单元格")

# ---------- 5. 作业 8 条要求覆盖度 ----------
reqs = {
    "新建 Unity2D 工程": ["MemoryCard", "2D"],
    "导入素材包": ["素材", "导入"],
    "代码生成 16 张 4x4": ["4", "16"],
    "数组存位置/状态": ["数组"],
    "洗牌算法": ["洗牌", "Fisher"],
    "点击翻牌": ["翻牌", "OnMouseDown"],
    "配对判定": ["配对"],
    "延迟翻回": ["翻回", "WaitForSeconds"],
    "计分": ["计分", "Score"],
    "步数": ["步数", "Moves"],
    "胜利判定": ["胜利", "全部配对"],
    "循环 BGM": ["BGM", "循环"],
    "翻牌音效": ["音效"],
    "重开按钮": ["重开", "Restart"],
    "导出 exe": ["exe", "构建"],
}
for label, kws in reqs.items():
    miss = [k for k in kws if k not in plain]
    if len(miss) == len(kws):
        bad(f"要求「{label}」报告里完全没提到")
    elif miss:
        warn(f"要求「{label}」缺少关键词 {miss}")
    else:
        ok(f"要求「{label}」已覆盖")

# ---------- 6. 源码完整性 ----------
cs_files = sorted((ROOT / "Assets" / "Scripts").glob("*.cs"))
ed_files = sorted((ROOT / "Assets" / "Editor").glob("*.cs"))
all_cs = cs_files + ed_files
for f in all_cs:
    src = f.read_text(encoding="utf-8")
    # 报告里是否出现该脚本的类名与方法签名
    cls = re.search(r"class\s+(\w+)", src)
    cls_name = cls.group(1) if cls else f.stem
    if cls_name not in plain:
        bad(f"{f.name}（类 {cls_name}）未出现在报告中")
        continue
    lines = [l.strip() for l in src.splitlines() if l.strip() and not l.strip().startswith("//")]
    # 抽 3 条特征行，看报告里是否有
    probes = [l for l in lines if len(l) > 25][:400:137]
    missing = [p for p in probes if p.replace("{", "").replace("}", "").strip()[:40] not in plain]
    if missing:
        warn(f"{f.name} 有 {len(missing)}/{len(probes)} 条抽样代码行未在报告中找到")
    else:
        ok(f"{f.name} 源码抽样比对通过（{len(probes)} 条特征行）")

# 大括号配平（源文件本身）
for f in all_cs:
    src = f.read_text(encoding="utf-8")
    if src.count("{") != src.count("}"):
        bad(f"{f.name} 大括号不配平 {src.count('{')} vs {src.count('}')}")

# ---------- 7. 关键数值一致性 ----------
gm = (ROOT / "Assets" / "Scripts" / "GameManager.cs").read_text(encoding="utf-8")
bm = (ROOT / "Assets" / "Scripts" / "BoardManager.cs").read_text(encoding="utf-8")
for pat, label in [
    (r"scorePerMatch\s*=\s*(\d+)", "每配对加分"),
    (r"penaltyPerMismatch\s*=\s*(\d+)", "失配扣分"),
    (r"flipBackDelay\s*=\s*([\d.]+)f?", "翻回延时"),
]:
    mm = re.search(pat, gm)
    val = mm.group(1) if mm else "?"
    if val != "?" and val in plain:
        ok(f"{label} {val} 与代码一致且在报告中出现")
    elif val != "?":
        warn(f"{label} 代码为 {val}，报告中未找到该数值")

for pat, label in [(r"rows\s*=\s*(\d+)", "行数"), (r"cols\s*=\s*(\d+)", "列数")]:
    mm = re.search(pat, bm)
    ok(f"BoardManager {label} = {mm.group(1) if mm else '?'}")

fonts = set(re.findall(r'w:ascii="([^"]+)"', doc_xml))
ok("正文字体 ascii：" + ", ".join(sorted(fonts)[:8]))

# ---------- 8. 图片尺寸 ----------
ext = re.findall(r'<wp:extent cx="(\d+)" cy="(\d+)"', doc_xml)
if ext:
    sizes = sorted({(round(int(a) / 12700), round(int(b) / 12700)) for a, b in ext})
    ok(f"图片显示尺寸(pt) {sizes}")

# ---------- 9. 换行是否生效 ----------
nbr = doc_xml.count('w:type="textWrapping"')
if nbr < 100:
    bad(f"单元格内换行仅 {nbr} 处，代码可能被压成一行")
else:
    ok(f"单元格内换行 {nbr} 处（代码分行正常）")

# ---------- 10. 构建产物 ----------
exe = ROOT / "build_win" / "MemoryCard.exe"
if exe.exists():
    data = ROOT / "build_win" / "MemoryCard_Data"
    need = ["globalgamemanagers", "level0", "sharedassets0.assets"]
    miss = [n for n in need if not (data / n).exists()]
    if miss:
        bad(f"exe 数据目录缺少 {miss}")
    else:
        tot = sum(f.stat().st_size for f in (ROOT / "build_win").rglob("*") if f.is_file())
        ok(f"exe 与数据目录完整，合计 {tot/1048576:.1f} MB")
else:
    bad("build_win/MemoryCard.exe 不存在")

# ---------- 输出 ----------
print("=" * 70)
for x in oks:
    print("  OK   ", x)
print("-" * 70)
for x in warns:
    print("  WARN ", x)
for x in issues:
    print("  FAIL ", x)
print("=" * 70)
print(f"通过 {len(oks)} / 警告 {len(warns)} / 失败 {len(issues)}")
sys.exit(1 if issues else 0)
