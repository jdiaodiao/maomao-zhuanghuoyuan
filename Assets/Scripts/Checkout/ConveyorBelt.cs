using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 拟真传送带：运动学刚体每物理帧"退回-再前进"，靠摩擦带动商品；
    /// 末端光电传感器被挡住时整条带子停止（和真实收银台一样）。
    /// </summary>
    public class ConveyorBelt : MonoBehaviour
    {
        public Vector3 Direction = Vector3.right;
        public float Speed = 0.32f;
        public Vector3 EyeFrom, EyeTo;   // 光电传感器射线（世界坐标）
        public Renderer BeltRenderer;
        public Renderer EyeLight;

        public bool Running { get; private set; } = true;
        public bool Blocked { get; private set; }

        Rigidbody _rb;
        float _texOffset;
        readonly HashSet<Rigidbody> _riders = new HashSet<Rigidbody>();
        bool _wasRunning = true;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            Blocked = Physics.Linecast(EyeFrom, EyeTo, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                      && hit.rigidbody != null && hit.rigidbody.GetComponent<GroceryItem>() != null;
            Running = !Blocked;

            if (Running)
            {
                Vector3 pos = _rb.position;
                _rb.position = pos - Direction * Speed * Time.fixedDeltaTime;
                _rb.MovePosition(pos);
                if (!_wasRunning)
                    foreach (var r in _riders) if (r != null) r.WakeUp();
            }
            _wasRunning = Running;
        }

        void Update()
        {
            if (Running)
            {
                _texOffset -= Speed * Time.deltaTime / 0.25f;
                if (BeltRenderer != null) BeltRenderer.material.mainTextureOffset = new Vector2(_texOffset, 0);
            }
            if (EyeLight != null)
                EyeLight.sharedMaterial = Mats.Get(Blocked ? new Color(1f, 0.2f, 0.15f) : new Color(0.2f, 1f, 0.3f), 0.8f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody == null) return;
            _riders.Add(c.rigidbody);
            var gi = c.rigidbody.GetComponent<GroceryItem>();
            if (gi != null) gi.OnBelt = true;
        }

        void OnCollisionExit(Collision c)
        {
            if (c.rigidbody == null) return;
            _riders.Remove(c.rigidbody);
            var gi = c.rigidbody.GetComponent<GroceryItem>();
            if (gi != null) gi.OnBelt = false;
        }

        public static Texture2D StripeTexture()
        {
            var tex = new Texture2D(64, 16) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++)
            for (int y = 0; y < 16; y++)
            {
                bool rib = (x % 16) < 2;
                float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.06f;
                float v = (rib ? 0.1f : 0.17f) + n;
                tex.SetPixel(x, y, new Color(v, v, v * 1.05f));
            }
            tex.Apply();
            return tex;
        }
    }
}
