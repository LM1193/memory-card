using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MemoryCard.EditorTools
{
    /// <summary>
    /// 工程自检：把「只能靠肉眼在 Inspector 里逐个点开看」的配置错误一次性查出来。
    ///
    /// 检查项：
    ///   1. 场景中缺失脚本（Missing Script）的物体
    ///   2. 三个管理器是否各存在且唯一
    ///   3. 管理器的必需序列化引用是否为空（空引用是运行时 NullReferenceException 的最主要来源）
    ///   4. 牌面素材数组是否完整、牌数是否为偶数（必须成对才能全部配对）
    ///   5. Card 预制体的组件是否齐全
    ///   6. 构建设置里是否包含主场景
    ///   7. WebGL 模块是否可用
    ///   8. 卡牌素材的 Pixels Per Unit 是否与 BoardManager 的世界尺寸约定一致
    ///   9. 相机正交尺寸是否完整覆盖牌阵
    ///
    /// 结论会同时输出到 Console 与 Logs/validate-report.txt。
    /// </summary>
    public static class ProjectValidator
    {
        private const string LogRelativePath = "Logs/validate-report.txt";

        private sealed class Result
        {
            public readonly List<string> Pass = new List<string>();
            public readonly List<string> Warn = new List<string>();
            public readonly List<string> Fail = new List<string>();

            public void Ok(string msg) => Pass.Add(msg);
            public void Warning(string msg) => Warn.Add(msg);
            public void Error(string msg) => Fail.Add(msg);

            public bool IsPass => Fail.Count == 0;
        }

        [MenuItem("Tools/Memory Card/Validate Project", priority = 11)]
        public static void Run()
        {
            var r = new Result();
            Scene scene = SceneManager.GetActiveScene();

            CheckMissingScripts(r, scene);
            CheckManagers(r);
            CheckPuzzleData(r);
            CheckAudio(r);

            var board = Object.FindObjectOfType<BoardManager>();
            CheckCardPrefab(r, board);
            CheckBuildScenes(r, scene);
            CheckWebGLModule(r);
            CheckSpriteImport(r, board);
            CheckCameraFraming(r, board);

            string report = Compose(r, scene);
            Debug.Log(report);

            try
            {
                string logPath = Path.Combine(Directory.GetCurrentDirectory(), LogRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.WriteAllText(logPath, StripRichText(report), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Validate] 报告写盘失败：{e.Message}");
            }

            if (r.IsPass) Debug.Log("[Validate] 结论：PASS —— 工程配置无阻塞性问题。");
            else Debug.LogError($"[Validate] 结论：FAIL —— 存在 {r.Fail.Count} 项阻塞性问题，详见上方报告。");
        }

        // ---------------------------------------------------------------- 检查项

        private static void CheckMissingScripts(Result r, Scene scene)
        {
            if (!scene.IsValid())
            {
                r.Error("没有已打开的场景，无法检查。请先打开 Assets/Main.unity。");
                return;
            }

            int missing = 0;
            var detail = new StringBuilder();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c != null) continue;
                    missing++;
                    detail.Append($"     · {GetPath(go.transform)}\n");
                }
            }

            if (missing == 0) r.Ok($"场景 `{scene.name}` 无缺失脚本。");
            else r.Error($"场景 `{scene.name}` 存在 {missing} 个 Missing Script：\n{detail}");
        }

        private static void CheckManagers(Result r)
        {
            CheckSingle<GameManager>(r, "GameManager");
            CheckSingle<BoardManager>(r, "BoardManager");
            CheckSingle<UIManager>(r, "UIManager");
        }

        private static void CheckSingle<T>(Result r, string label) where T : Component
        {
            T[] found = Object.FindObjectsOfType<T>();
            if (found.Length == 0) r.Error($"场景中找不到 {label}。");
            else if (found.Length > 1) r.Warning($"场景中存在 {found.Length} 个 {label}，请确认只有一个。");
            else r.Ok($"{label} 存在且唯一。");
        }

        private static void CheckPuzzleData(Result r)
        {
            var gm = Object.FindObjectOfType<GameManager>();
            if (gm != null)
            {
                var so = new SerializedObject(gm);
                CheckRefs(r, so, "GameManager", "board", "ui", "music");
            }

            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                CheckRefs(r, so, "UIManager",
                    "timeText", "scoreText", "movesText", "resultText", "bestText",
                    "startPanel", "winPanel", "startButton", "restartButton");
            }

            var board = Object.FindObjectOfType<BoardManager>();
            if (board == null) return;

            var bso = new SerializedObject(board);
            CheckRefs(r, bso, "BoardManager", "backSprite", "cardPrefab");

            SerializedProperty images = bso.FindProperty("images");
            if (images == null || !images.isArray)
            {
                r.Error("BoardManager.images 字段缺失（脚本被改动？）。");
            }
            else
            {
                if (images.arraySize == 0) r.Error("BoardManager.images 为空，没有牌面素材。");
                for (int i = 0; i < images.arraySize; i++)
                {
                    if (images.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        r.Error($"BoardManager.images[{i}] 为空。");
                }
                if (images.arraySize > 0) r.Ok($"BoardManager.images 共 {images.arraySize} 张，均已赋值。");
            }

            SerializedProperty rows = bso.FindProperty("rows");
            SerializedProperty cols = bso.FindProperty("cols");
            if (rows != null && cols != null)
            {
                int total = rows.intValue * cols.intValue;
                if (total % 2 != 0) r.Error($"rows × cols = {total} 为奇数，无法两两配对。");
                else if (total == 0) r.Error("rows 或 cols 为 0，牌阵无牌。");
                else r.Ok($"牌阵 {rows.intValue} × {cols.intValue} = {total} 张，共 {total / 2} 对。");

                if (images != null && images.arraySize > 0 && total / 2 > images.arraySize)
                    r.Warning($"需要 {total / 2} 种牌面，但 images 只有 {images.arraySize} 张，将出现数组越界。");
            }
        }

        /// <summary>
        /// 音频系统检查：BGM 必须独立 AudioSource + 开启 Loop，且导入设置不能是压缩格式。
        /// 这一项直接对应实验要求第 6 条「循环播放背景音乐」。
        /// </summary>
        private static void CheckAudio(Result r)
        {
            var mp = Object.FindObjectOfType<MusicPlayer>();
            if (mp == null)
            {
                r.Error("场景中找不到 MusicPlayer（背景音乐）。请把 MusicPlayer.cs 挂到 Music 物体上。");
                return;
            }

            var so = new SerializedObject(mp);
            CheckRefs(r, so, "MusicPlayer", "source", "bgm");

            var src = mp.GetComponent<AudioSource>();
            if (src == null)
            {
                r.Error("MusicPlayer 所在物体上没有 AudioSource 组件。");
            }
            else
            {
                if (!src.loop) r.Error("背景音乐 AudioSource 没有勾选 Loop —— 播放一遍就会停，不满足「循环播放」。");
                else r.Ok("背景音乐 AudioSource 已勾选 Loop（循环播放）。");

                if (src.clip == null) r.Error("背景音乐 AudioSource 未指定 AudioClip。");

                // BGM 与音效必须分属两个 AudioSource，否则 PlayOneShot 会打断循环
                var gm = Object.FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    var sfxSrc = gm.GetComponent<AudioSource>();
                    if (sfxSrc == null) r.Warning("GameManager 上没有 AudioSource，Sfx 只能在运行时自动添加。");
                    else if (sfxSrc == src) r.Error("BGM 与音效共用同一个 AudioSource，音效会打断背景音乐。");
                    else r.Ok("BGM 与音效使用两个独立 AudioSource，互不打断。");
                }
            }

            // 导入设置：WAV 应保持 PCM / Decompress On Load，压缩格式在循环点可能产生间隙
            var clip = so.FindProperty("bgm").objectReferenceValue as AudioClip;
            if (clip != null)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null)
                {
                    r.Warning($"无法读取 {path} 的导入设置。");
                }
                else
                {
                    AudioImporterSampleSettings s = ai.defaultSampleSettings;
                    if (s.loadType != AudioClipLoadType.DecompressOnLoad)
                        r.Warning($"{path} 的 Load Type 是 {s.loadType}，建议改为 Decompress On Load（循环点更稳）。");
                    else if (s.compressionFormat != AudioCompressionFormat.PCM)
                        r.Warning($"{path} 的压缩格式是 {s.compressionFormat}，WAV 建议保持 PCM 以免循环点出现间隙。");
                    else
                        r.Ok($"BGM 导入设置正确：{path}，PCM / Decompress On Load，时长 {clip.length:F1} s。");
                }
            }
        }

        private static void CheckRefs(Result r, SerializedObject so, string owner, params string[] fields)
        {
            var missing = new List<string>();
            foreach (string field in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) { missing.Add($"{field}(字段不存在)"); continue; }
                if (p.objectReferenceValue == null) missing.Add(field);
            }

            if (missing.Count == 0) r.Ok($"{owner} 的 {fields.Length} 个必需引用均已赋值。");
            else r.Error($"{owner} 存在空引用：{string.Join(", ", missing)}");
        }

        private static void CheckCardPrefab(Result r, BoardManager board)
        {
            if (board == null) return;

            var so = new SerializedObject(board);
            SerializedProperty prefabProp = so.FindProperty("cardPrefab");
            if (prefabProp == null || prefabProp.objectReferenceValue == null)
            {
                r.Error("BoardManager.cardPrefab 为空，运行时无法生成卡牌。");
                return;
            }

            Object asset = prefabProp.objectReferenceValue;
            GameObject prefab = asset as GameObject;
            if (prefab == null && asset is Component component) prefab = component.gameObject;

            if (prefab == null)
            {
                r.Error("cardPrefab 引用的对象无法解析为 GameObject。");
                return;
            }

            PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(prefab);
            if (prefabType == PrefabAssetType.NotAPrefab)
                r.Warning($"cardPrefab 引用的是场景对象 `{prefab.name}`，没有指向 Prefabs 目录下的预制体资产。");

            var issues = new List<string>();
            if (prefab.GetComponent<Card>() == null) issues.Add("Card 脚本");
            if (prefab.GetComponent<SpriteRenderer>() == null) issues.Add("SpriteRenderer");
            if (prefab.GetComponent<BoxCollider2D>() == null) issues.Add("BoxCollider2D");

            Rigidbody2D rb = prefab.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                issues.Add("Rigidbody2D 为 Dynamic（卡牌会因重力下落）");

            if (issues.Count == 0) r.Ok($"Card 预制体 `{prefab.name}` 组件齐全（Card + SpriteRenderer + BoxCollider2D）。");
            else r.Error($"Card 预制体 `{prefab.name}` 缺少或配置错误：{string.Join(", ", issues)}");
        }

        private static void CheckBuildScenes(Result r, Scene scene)
        {
            var enabled = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s != null && s.enabled) enabled.Add(s.path);

            if (enabled.Count == 0)
            {
                r.Error("Build Settings 中没有任何启用的场景，构建出来会是空包。");
                return;
            }

            if (scene.IsValid() && !enabled.Contains(scene.path))
                r.Warning($"当前场景 `{scene.path}` 不在构建列表中。");
            else if (scene.IsValid())
                r.Ok($"构建列表包含当前场景：{string.Join(", ", enabled)}。");
            else
                r.Ok($"构建列表：{string.Join(", ", enabled)}。");
        }

        private static void CheckWebGLModule(Result r)
        {
            // 交付物要求 exe，所以 Windows 模块是硬性条件；WebGL 只是备用的网页版
            bool win = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            if (win) r.Ok("Windows 构建模块可用（可导出 exe）。");
            else r.Error("Windows 模块缺失：Hub → 安装 → 该版本齿轮 → 添加模块 → Windows Build Support (Mono/IL2CPP)。");

            bool webgl = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (webgl) r.Ok("WebGL 构建模块可用（网页版备用）。");
            else r.Warning("WebGL 模块缺失，仅影响网页版，不影响 exe 交付。");

            var target = EditorUserBuildSettings.activeBuildTarget;
            r.Ok($"当前活动平台为 {target}（构建工具会在构建前自动切换到目标平台）。");
        }

        private static void CheckSpriteImport(Result r, BoardManager board)
        {
            if (board == null) return;

            var so = new SerializedObject(board);
            SerializedProperty back = so.FindProperty("backSprite");
            SerializedProperty images = so.FindProperty("images");

            var sprites = new List<Sprite>();
            if (back != null && back.objectReferenceValue != null) sprites.Add(back.objectReferenceValue as Sprite);
            if (images != null && images.isArray)
                for (int i = 0; i < images.arraySize; i++)
                    sprites.Add(images.GetArrayElementAtIndex(i).objectReferenceValue as Sprite);

            if (sprites.Count == 0) return;

            var badPpu = new List<string>();
            foreach (Sprite s in sprites)
            {
                if (s == null) continue;
                string path = AssetDatabase.GetAssetPath(s);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
                    badPpu.Add($"{s.name}(PPU={importer.spritePixelsPerUnit})");
            }

            if (badPpu.Count == 0) r.Ok($"{sprites.Count} 张卡牌精灵 PPU 均为 100，与 1.5 × 2 世界尺寸约定一致。");
            else r.Warning($"以下精灵 PPU 不是 100，实际世界尺寸会偏离预期：{string.Join(", ", badPpu)}");
        }

        private static void CheckCameraFraming(Result r, BoardManager board)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                r.Error("场景中找不到 MainCamera（Camera.main 为空）。");
                return;
            }
            if (!cam.orthographic)
            {
                r.Warning("主相机不是正交投影，2D 牌桌会出现透视变形。");
                return;
            }
            if (board == null) return;

            Vector2 boardSize = board.GetBoardWorldSize();
            Vector2 center = new Vector2(
                (board.GetPosition(0, 0).x + board.GetPosition(board.Rows - 1, board.Cols - 1).x) * 0.5f,
                (board.GetPosition(0, 0).y + board.GetPosition(board.Rows - 1, board.Cols - 1).y) * 0.5f);

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            bool vertical = boardSize.y <= halfHeight * 2f;
            bool horizontal = boardSize.x <= halfWidth * 2f;

            if (vertical && horizontal)
                r.Ok($"相机取景覆盖牌阵：牌阵 {boardSize.x:F2} × {boardSize.y:F2}，视野 {halfWidth * 2f:F2} × {halfHeight * 2f:F2}（aspect {cam.aspect:F2}）。");
            else
                r.Warning($"相机取景可能裁掉卡牌：牌阵 {boardSize.x:F2} × {boardSize.y:F2}，视野 {halfWidth * 2f:F2} × {halfHeight * 2f:F2}。" +
                          $"牌阵中心 ({center.x:F2}, {center.y:F2})，相机位置 ({camPos.x:F2}, {camPos.y:F2})。");

            float offsetX = Mathf.Abs(camPos.x - center.x) + boardSize.x * 0.5f;
            float offsetY = Mathf.Abs(camPos.y - center.y) + boardSize.y * 0.5f;
            if (offsetX > halfWidth + 0.01f || offsetY > halfHeight + 0.01f)
                r.Warning($"牌阵中心与相机中心偏差过大（Δx={offsetX - boardSize.x * 0.5f:F2}, Δy={offsetY - boardSize.y * 0.5f:F2}），请检查相机位置。");
        }

        // ---------------------------------------------------------------- 输出

        private static string Compose(Result r, Scene scene)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== Memory Card · 工程自检报告 =====");
            sb.AppendLine($"时间     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"场景     : {(scene.IsValid() ? scene.path : "（未打开）")}");
            sb.AppendLine($"结论     : {(r.IsPass ? "PASS" : "FAIL")}   通过 {r.Pass.Count} / 警告 {r.Warn.Count} / 失败 {r.Fail.Count}");
            sb.AppendLine();

            if (r.Fail.Count > 0)
            {
                sb.AppendLine("【阻塞性问题】");
                foreach (string s in r.Fail) sb.AppendLine($"  ✗ {s}");
                sb.AppendLine();
            }
            if (r.Warn.Count > 0)
            {
                sb.AppendLine("【警告】");
                foreach (string s in r.Warn) sb.AppendLine($"  ! {s}");
                sb.AppendLine();
            }
            sb.AppendLine("【通过项】");
            foreach (string s in r.Pass) sb.AppendLine($"  ✓ {s}");

            return sb.ToString();
        }

        private static string StripRichText(string text)
        {
            return text.Replace("<b>", "").Replace("</b>", "").Replace("<color=#cc7a00>", "").Replace("</color>", "");
        }

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }
    }
}
