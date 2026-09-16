using UnityEngine;

namespace Game
{
    /// <summary>
    /// 摄像机跟随（表现层 MonoBehaviour）：平滑跟随玩家。
    /// 默认 offset z=-10，保持 2D 正交摄像机的标准距离。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }
    }
}
