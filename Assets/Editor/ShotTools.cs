using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryCard.EditorTools
{
    /// <summary>
    /// 截图工具：把**运行时真实画面**（含 Canvas 上的 HUD）渲染到 PNG，用于实验报告配图。
    ///
    /// 【为什么不直接 Camera.Render() 取图】
    /// 1. HUD 是 ScreenSpaceOverlay 画布，Overlay 图层不经过任何相机，
    ///    Camera.Render() 出来的图只有牌桌和卡牌，看不到 TIME / SCORE / MOVES。
    ///    做法：截图瞬间把画布临时改成 ScreenSpaceCamera 并挂到一台临时相机上，
    ///    让 UI 与 3D 内容一起被渲染到 RenderTexture，截完恢复 Overlay。
    /// 2. SpriteRenderer 换图后需要走到**帧末**才进入渲染批处理。
    ///    在同一个方法调用里立刻 Render() 拿到的仍是上一帧的画面（翻开的牌不会出现），
    ///    因此截图必须在 WaitForEndOfFrame 之后执行。
    ///
    /// 用法：ShotTools.RequestCapture("docs/screenshots/xx.png")，
    ///       下一个真实帧结束时截图，结果写进 Logs/shot-last.txt。
    /// </summary>
    public static class ShotTools
    {
        private const int ShotWidth = 1280;
        private const int ShotHeight = 720;
        private const string ResultLog = "Logs/shot-last.txt";

        private static string _pending;
        private static ShotRunner _runner;

        public static void RequestCapture(string filePath)
        {
            _pending = filePath;
            if (_runner == null)
            {
                var go = new GameObject("~ShotRunner");
                _runner = go.AddComponent<ShotRunner>();
            }
        }

        public static string LastResult =>
            File.Exists(ResultLog) ? File.ReadAllText(ResultLog) : "(还没有截图记录)";

        /// <summary>真正的截图实现：调用时已在帧末，画面即当前状态。</summary>
        internal static string Render(string filePath)
        {
            Camera main = Camera.main;
            if (main == null) return "Main Camera 不存在";

            var canvas = Object.FindObjectOfType<Canvas>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCam = canvas.worldCamera;
            float oldPlane = canvas.planeDistance;

            var rt = new RenderTexture(ShotWidth, ShotHeight, 24);
            var shotCamGo = new GameObject("~ShotCamera");
            var shotCam = shotCamGo.AddComponent<Camera>();

            try
            {
                shotCam.CopyFrom(main);
                shotCam.targetTexture = rt;
                shotCam.aspect = ShotWidth / (float)ShotHeight;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = shotCam;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();

                shotCam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                byte[] png = tex.EncodeToPNG();
                string full = Path.GetFullPath(filePath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
                Object.DestroyImmediate(tex);

                return $"{Path.GetFileName(filePath)} 已保存（{png.Length / 1024} KB）";
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                canvas.planeDistance = oldPlane;
                shotCam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(shotCamGo);
            }
        }

        /// <summary>帧末驱动：只在运行时（播放模式）由 RequestCapture 临时创建。</summary>
        public class ShotRunner : MonoBehaviour
        {
            private void Update()
            {
                if (_pending == null) return;
                string path = _pending;
                _pending = null;
                StartCoroutine(CaptureAtEndOfFrame(path));
            }

            private IEnumerator CaptureAtEndOfFrame(string path)
            {
                yield return new WaitForEndOfFrame();
                string result;
                try { result = Render(path); }
                catch (System.Exception e) { result = "截图失败：" + e.Message; }
                File.WriteAllText(ResultLog, result);
                Debug.Log("[Shot] " + result);
            }
        }
    }
}
