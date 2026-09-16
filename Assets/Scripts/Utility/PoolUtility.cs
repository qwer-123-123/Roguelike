using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 对象池接口（工具层）：割草游戏大量同屏敌人/投射物的性能基础。
    /// </summary>
    public interface IPoolUtility : IUtility
    {
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        void Despawn(GameObject instance);
        /// <summary>重开一局时回收所有运行时生成的对象（敌人/投射物/掉落物）。</summary>
        void ReleaseAll();
    }

    /// <summary>
    /// 简单对象池实现：按 prefab 分池，Spawn 复用 / Despawn 回收。
    /// </summary>
    public class PoolUtility : IPoolUtility
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
        private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            GameObject obj;
            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
            }
            else
            {
                obj = Object.Instantiate(prefab, position, rotation);
                instanceToPrefab[obj] = prefab;
            }

            return obj;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                Object.Destroy(instance);
                return;
            }

            instance.SetActive(false);
            pools[prefab].Enqueue(instance);
        }

        public void ReleaseAll()
        {
            foreach (var kvp in new List<KeyValuePair<GameObject, GameObject>>(instanceToPrefab))
            {
                var instance = kvp.Key;
                var prefab = kvp.Value;
                if (instance == null) continue;

                // 只回收仍激活（出现在场景中）的实例；已在池里的 inactive 实例不动，避免重复入队
                if (!instance.activeSelf) continue;

                instance.SetActive(false);
                if (pools.TryGetValue(prefab, out var queue))
                    queue.Enqueue(instance);
            }

            // 保留 instanceToPrefab 映射：否则复用池对象时 S/Despawn 丢失识别，会走 Destroy 破坏对象池
        }
    }
}
