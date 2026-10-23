using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MemoryCard.EditorTools
{
    /// <summary>
    /// 安全构建入口。
    ///
    /// 【解决的实际问题】
    /// 直接调用 BuildPipeline.BuildPlayer 时，若编辑器正处在以下三种状态之一，构建会**立刻失败**：
    ///   1. 正在编译脚本（domain reload 未完成）→ 报 "Error building Player because scripts are compiling"
    ///   2. 正在导入资源
    ///   3. 处于 Play 模式 → Build 按钮本身不可用
    /// 本工具在构建前把这三种状态全部等干净，再执行构建，从根上消除这类时序失败。
    ///
    /// 【注意】等待过程必须用 EditorApplication.delayCall 轮询，**不能**用 Thread.Sleep 阻塞主线程：
    /// 脚本编译的收尾（程序集加载、domain reload）需要主线程回到事件循环才能完成，
    /// 阻塞主线程会让 isCompiling 永远为 true，形成死等。
    /// </summary>
    public static class BuildTools
    {
        private const string WebGLOutputFolder = "webgl";
        private const string WindowsOutputFolder = "build_win";
        private const string WindowsExeName = "MemoryCard.exe";
        private const string DefaultScene = "Assets/Main.unity";
        private const string WebGLLogPath = "Logs/build-webgl.txt";
        private const string WindowsLogPath = "Logs/build-win64.txt";

        /// <summary>delayCall 每帧回调一次；1200 次约等于 20 秒以上（低帧率时更长）</summary>
        private const int MaxWaitTicks = 1200;

        [MenuItem("Tools/Memory Card/Build Windows x64 (Safe)", priority = 9)]
        private static void BuildWindowsMenu()
        {
            EnsureTargetThenBuild(false, BuildTarget.StandaloneWindows64);
        }

        [MenuItem("Tools/Memory Card/Build WebGL (Safe)", priority = 10)]
        private static void BuildWebGLMenu()
        {
            EnsureTargetThenBuild(false, BuildTarget.WebGL);
        }

        /// <summary>
        /// 批处理入口（无界面）：
        /// Unity.exe -quit -batchmode -projectPath "..." -executeMethod MemoryCard.EditorTools.BuildTools.BuildWebGLBatch
        /// </summary>
        public static void BuildWebGLBatch()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogError("[Build] 批处理模式下检测到编辑器仍在编译/导入，已中止。请先让编辑器完成编译后重跑。");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EnsureTargetThenBuild(true, BuildTarget.WebGL);
        }

        /// <summary>
        /// 批处理入口（无界面）：Unity.exe -quit -batchmode -projectPath "..." -executeMethod MemoryCard.EditorTools.BuildTools.BuildWindowsBatch
        /// </summary>
        public static void BuildWindowsBatch()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogError("[Build] 批处理模式下检测到编辑器仍在编译/导入，已中止。请先让编辑器完成编译后重跑。");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EnsureTargetThenBuild(true, BuildTarget.StandaloneWindows64);
        }

        private static void EnsureTargetThenBuild(bool batchMode, BuildTarget target)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log($"[Build] 当前平台为 {EditorUserBuildSettings.activeBuildTarget}，正在切换到 {target}（会触发资源重导入，耗时较长）…");
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                if (!switched)
                {
                    Debug.LogError($"[Build] 切换 {target} 平台失败，构建中止。请确认该模块已安装（Hub → 添加模块）。");
                    if (batchMode && Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
            }

            WaitUntilIdle(batchMode, 0, () => DoBuild(target));
        }

        /// <summary>
        /// 非阻塞等待：编译中 / 导入中 / 播放模式 三者任一为真就下一个 editor 帧再看。
        /// </summary>
        private static void WaitUntilIdle(bool batchMode, int tick, Action onIdle)
        {
            bool busy = EditorApplication.isCompiling
                        || EditorApplication.isUpdating
                        || EditorApplication.isPlayingOrWillChangePlaymode;

            if (!busy)
            {
                EditorUtility.ClearProgressBar();
                onIdle();
                return;
            }

            if (tick > MaxWaitTicks)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Build] 等待编辑器空闲超时（tick={tick}）。当前状态：isCompiling={EditorApplication.isCompiling} " +
                               $"isUpdating={EditorApplication.isUpdating} isPlaying={EditorApplication.isPlayingOrWillChangePlaymode}。构建已中止。");
                if (batchMode && Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // 播放模式直接停掉，否则永远等不到空闲
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            if (!batchMode)
            {
                EditorUtility.DisplayProgressBar("Memory Card",
                    "等待编辑器空闲（编译 / 导入 / 退出播放模式）…", 0.15f);
            }

            EditorApplication.delayCall += () => WaitUntilIdle(batchMode, tick + 1, onIdle);
        }

        private static void DoBuild(BuildTarget target)
        {
            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError($"[Build] 构建场景列表为空，且找不到 {DefaultScene}，构建中止。");
                return;
            }

            bool isWindows = target == BuildTarget.StandaloneWindows64;
            string folder = isWindows ? WindowsOutputFolder : WebGLOutputFolder;
            string outputDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), folder));

            // WebGL 的 locationPathName 是目录；Windows 必须是 .exe 的完整路径
            string location = isWindows
                ? Path.Combine(outputDir, WindowsExeName)
                : outputDir;

            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            };

            string label = isWindows ? "Windows x64（单文件 exe）" : "WebGL";
            Debug.Log($"[Build] 开始构建 {label} → {location}\n[Build] 场景：{string.Join(", ", scenes)}");
            float startTime = (float)EditorApplication.timeSinceStartup;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            float elapsed = (float)EditorApplication.timeSinceStartup - startTime;
            BuildSummary summary = report.summary;

            var sb = new StringBuilder();
            sb.AppendLine($"===== Memory Card · {label} 构建报告 =====");
            sb.AppendLine($"时间        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"结果        : {summary.result}");
            sb.AppendLine($"耗时        : {elapsed:F1} s");
            sb.AppendLine($"输出        : {location}");
            sb.AppendLine($"场景        : {string.Join(", ", scenes)}");
            sb.AppendLine($"构建体积    : {summary.totalSize / 1024f / 1024f:F2} MB（Unity 统计口径）");
            sb.AppendLine($"错误 / 警告 : {summary.totalErrors} / {summary.totalWarnings}");

            if (summary.result == BuildResult.Succeeded)
            {
                AppendDiskBreakdown(sb, outputDir);
            }

            string text = sb.ToString();
            Debug.Log(text);

            try
            {
                string logPath = Path.Combine(Directory.GetCurrentDirectory(),
                    isWindows ? WindowsLogPath : WebGLLogPath);
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.WriteAllText(logPath, text, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Build] 构建报告写盘失败：{e.Message}");
            }

            if (summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(isWindows ? location : outputDir);
            }
        }

        private static string[] CollectScenes()
        {
            var list = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene != null && scene.enabled) list.Add(scene.path);
            }
            if (list.Count == 0 && File.Exists(DefaultScene)) list.Add(DefaultScene);
            return list.ToArray();
        }

        /// <summary>统计磁盘实际产物，按体积降序，便于写进实验报告。</summary>
        private static void AppendDiskBreakdown(StringBuilder sb, string outputDir)
        {
            if (!Directory.Exists(outputDir)) return;

            var files = new List<FileInfo>();
            long total = 0;
            foreach (string path in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(path);
                files.Add(fi);
                total += fi.Length;
            }

            files.Sort((a, b) => b.Length.CompareTo(a.Length));

            sb.AppendLine();
            sb.AppendLine($"磁盘产物    : {files.Count} 个文件，共 {total / 1024f / 1024f:F2} MB");
            sb.AppendLine("体积明细（前 8）：");
            int shown = 0;
            foreach (FileInfo fi in files)
            {
                if (shown++ >= 8) break;
                string relative = fi.FullName.Substring(outputDir.Length).TrimStart('\\', '/');
                sb.AppendLine($"  {fi.Length / 1024f,9:F1} KB  {relative}");
            }
        }
    }
}
