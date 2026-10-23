using UnityEngine;

namespace MemoryCard
{
    /// <summary>
    /// 洗牌工具类：Fisher–Yates 洗牌（无偏、均匀分布）。
    /// 不挂载在任何物体上，纯静态工具。
    ///
    /// 对应课件第 10 页洗牌算法：
    ///   1. 遍历整个 numbers 数组，取当前索引 i；
    ///   2. 从当前索引 i 到最后一个元素之间取随机数 r；
    ///   3. 交换 i 与 r 两个位置的元素值；
    ///   4. 直到 i 为最后一个元素的索引。
    /// </summary>
    public static class ShuffleUtil
    {
        /// <summary>就地洗牌（原地打乱传入的数组）。</summary>
        public static void Shuffle<T>(T[] array)
        {
            if (array == null || array.Length <= 1) return;

            for (int i = 0; i < array.Length; i++)
            {
                // Random.Range(int, int) 的上界是【开区间】，取 array.Length 才能覆盖到最后一个元素
                int r = Random.Range(i, array.Length);

                // 交换 i 与 r
                T tmp = array[i];
                array[i] = array[r];
                array[r] = tmp;
            }
        }
    }
}
