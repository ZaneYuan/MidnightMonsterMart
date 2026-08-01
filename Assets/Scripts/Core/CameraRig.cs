using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 摄像机跟随 — 设计文档 §16 阶段一。
    /// 正交尺寸略小于整店，因此有实际的跟随感，同时钳制在店铺边界内，
    /// 玩家永远看不到店外的黑边。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;

        [Header("跟随")]
        public float smoothing = 6f;
        public float orthographicSize = 7.6f;

        Camera _camera;
        Vector3 _current;

        public void Initialize(Camera cam, Transform followTarget)
        {
            _camera = cam;
            target = followTarget;

            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.04f, 0.03f, 0.08f);
            _camera.nearClipPlane = -20f;
            _camera.farClipPlane = 20f;

            _current = Clamp(target != null ? target.position : StoreCenter());
            ApplyPosition(_current);
        }

        void LateUpdate()
        {
            if (_camera == null) return;

            Vector3 desired = target != null ? target.position : StoreCenter();
            desired = Clamp(desired);

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _current = Vector3.Lerp(_current, desired, t);
            ApplyPosition(_current);
        }

        void ApplyPosition(Vector3 p) => transform.position = new Vector3(p.x, p.y, -10f);

        static Vector3 StoreCenter()
            => new Vector3(GameConfig.GridWidth * 0.5f, GameConfig.GridHeight * 0.5f, 0f);

        Vector3 Clamp(Vector3 p)
        {
            float halfH = _camera.orthographicSize;
            float halfW = halfH * Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));

            float minX = halfW;
            float maxX = GameConfig.GridWidth - halfW;
            float minY = halfH;
            float maxY = GameConfig.GridHeight - halfH;

            // 视野比店还大时直接居中
            float x = minX > maxX ? GameConfig.GridWidth * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            float y = minY > maxY ? GameConfig.GridHeight * 0.5f : Mathf.Clamp(p.y, minY, maxY);

            return new Vector3(x, y, 0f);
        }
    }
}
