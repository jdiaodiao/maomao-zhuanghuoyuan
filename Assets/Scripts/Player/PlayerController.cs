using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 玩家控制：第一人称猫爪 + 俯视装袋模式（V 切换）。
    /// 抓取用"有上限的弹簧力"驱动商品：手劲有限，按压脆弱商品一样会压坏。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public Camera Cam;
        public PawRig Paws;
        public float MouseSensitivity = 2.2f;
        public float MoveSpeed = 1.4f;
        public float Reach = 2.3f;

        public bool InputEnabled = true;

        float _yaw, _pitch = 35f;
        public GroceryItem Held { get; private set; }
        float _holdDist = 0.65f;
        Quaternion _holdRot = Quaternion.identity;   // 相对于视角水平朝向的持有姿态
        int _heldOriginalLayer;

        public GroceryItem LookItem { get; private set; }
        public Bag LookBag { get; private set; }
        public BagRackButton LookRack { get; private set; }
        public bool LookBell { get; private set; }

        // ---------------- 俯视模式 ----------------
        /// <summary>是否处于俯视装袋模式。</summary>
        public bool TopView { get; private set; }
        /// <summary>0 = 第一人称，1 = 俯视（过渡动画用）。</summary>
        public float ViewBlend { get; private set; }
        Vector2 _topFocus = new Vector2(0, Layout.BagSlotZ);   // 俯视焦点 (x, z)
        float _topHeight = 0.85f;                               // 镜头离台面高度
        float _lift = 0.06f;                                    // 拿着商品时离下方表面的高度
        Vector3 _topTarget;
        bool _hasTopTarget;

        const int IgnoreRaycastLayer = 2;
        const float EyeHeight = 1.5f;
        static readonly Vector2 TopMin = new Vector2(-1.35f, -0.05f), TopMax = new Vector2(1.3f, 0.5f);

        void Start()
        {
            _yaw = transform.eulerAngles.y;
            LockCursor(InputEnabled);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        float ViewYaw => TopView ? 0f : _yaw;

        void Update()
        {
            if (!InputEnabled)
            {
                if (Held != null) Release();
                if (TopView) SetTopView(false, false);
                ClearLook();
                return;
            }

            if (Input.GetKeyDown(KeyCode.V)) SetTopView(!TopView, true);

            if (TopView) UpdateTopView();
            else if (!UpdateFirstPerson()) return;

            // ---------------- 交互（两种视角共用）----------------
            if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0)
            {
                if (LookItem != null && !LookItem.IsDestroyed) Grab(LookItem);
                else if (LookRack != null) GameManager.I.SpawnBag(LookRack.Spec);
                else if (LookBell) GameManager.I.FinishOrder();
                else if (Paws != null && !TopView) Paws.Swipe();
            }
            if (Input.GetMouseButtonUp(0) && Held != null) Release();

            if (Held != null)
            {
                float rs = 120f * Time.deltaTime;
                if (Input.GetKey(KeyCode.Q)) _holdRot = Quaternion.AngleAxis(rs, Vector3.up) * _holdRot;
                if (Input.GetKey(KeyCode.E)) _holdRot = Quaternion.AngleAxis(-rs, Vector3.up) * _holdRot;
                if (Input.GetKey(KeyCode.R)) _holdRot = Quaternion.AngleAxis(rs, Vector3.right) * _holdRot;
                if (Input.GetKey(KeyCode.F)) _holdRot = Quaternion.AngleAxis(-rs, Vector3.right) * _holdRot;
                if (Input.GetKeyDown(KeyCode.Z)) _holdRot = Quaternion.identity;
                if (Input.GetKeyDown(KeyCode.C)) _holdRot = Quaternion.AngleAxis(90, Vector3.forward) * _holdRot; // 侧放
            }

            if (Input.GetKeyDown(KeyCode.X) && LookBag != null) GameManager.I.RemoveBag(LookBag);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) GameManager.I.FinishOrder();
            if (Input.GetKeyDown(KeyCode.B)) Bag.ShowMarkers = !Bag.ShowMarkers;
        }

        /// <summary>第一人称输入。返回 false 表示本帧不处理交互（鼠标未锁定）。</summary>
        bool UpdateFirstPerson()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (!CursorLocked)
            {
                if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0) LockCursor(true);
                ClearLook();
                return false;
            }

            _yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * MouseSensitivity, -40f, 85f);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);

            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            if (move.sqrMagnitude > 0)
            {
                Vector3 p = transform.position + transform.rotation * move.normalized * MoveSpeed * Time.deltaTime;
                p.x = Mathf.Clamp(p.x, Layout.PlayerMinXZ.x, Layout.PlayerMaxXZ.x);
                p.z = Mathf.Clamp(p.z, Layout.PlayerMinXZ.y, Layout.PlayerMaxXZ.y);
                transform.position = p;
                if (Paws != null) Paws.Walking = true;
            }
            else if (Paws != null) Paws.Walking = false;

            if (Held != null) _holdDist = Mathf.Clamp(_holdDist + Input.mouseScrollDelta.y * 0.06f, 0.35f, 1.6f);
            UpdateLook(new Ray(Cam.transform.position, Cam.transform.forward), Reach);
            return true;
        }

        /// <summary>俯视模式输入：鼠标指哪拿哪，WASD 平移，滚轮缩放/抬高，1-4 跳到袋位。</summary>
        void UpdateTopView()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { SetTopView(false, true); return; }

            Vector2 pan = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (pan.sqrMagnitude > 0) _topFocus += pan.normalized * (0.5f + _topHeight) * Time.deltaTime;
            for (int i = 0; i < Layout.BagSlotX.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    _topFocus = new Vector2(Layout.BagSlotX[i], Layout.BagSlotZ);
            _topFocus = Vector2.Max(TopMin, Vector2.Min(TopMax, _topFocus));

            float scroll = Input.mouseScrollDelta.y;
            if (Held != null) _lift = Mathf.Clamp(_lift + scroll * 0.02f, 0.015f, 0.35f);
            else _topHeight = Mathf.Clamp(_topHeight - scroll * 0.06f, 0.4f, 1.4f);

            var ray = Cam.ScreenPointToRay(Input.mousePosition);
            UpdateLook(ray, 6f);

            // 拿着东西：目标点 = 鼠标指向的表面 + 商品半高 + 抬起高度
            _hasTopTarget = false;
            if (Held != null && Physics.Raycast(ray, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float halfH = HeldHalfHeight();
                _topTarget = hit.point + Vector3.up * (halfH + _lift);
                _hasTopTarget = true;
            }
        }

        // ---------------- 落点标记（俯视时显示商品正下方会落在哪）----------------
        GameObject _marker;

        void UpdateDropMarker()
        {
            bool show = TopView && Held != null && ViewBlend > 0.9f;
            if (!show) { if (_marker != null) _marker.SetActive(false); return; }
            if (_marker == null)
            {
                _marker = new GameObject("落点标记");
                var v = new VisualSet();
                v.Add(Shapes.Torus(0.5f, 0.035f, 40, 6), Mats.Emissive(new Color(1f, 0.55f, 0.15f), new Color(1.2f, 0.6f, 0.15f)));
                v.Add(Shapes.Disc(0.47f, 40), new Vector3(0, -0.01f, 0), Quaternion.identity, Mats.Glass(new Color(1f, 0.6f, 0.2f), 0.25f, 0.3f));
                v.Spawn(_marker.transform, "环", default, null, false);
            }
            Vector3 com = Held.WorldCom;
            if (Physics.Raycast(com, Vector3.down, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _marker.SetActive(true);
                _marker.transform.position = hit.point + Vector3.up * 0.003f;
                float r = Mathf.Clamp(Mathf.Max(Held.Def.Size.x, Held.Def.Size.z) * 0.6f, 0.03f, 0.25f);
                _marker.transform.localScale = new Vector3(r, 1, r);
            }
            else _marker.SetActive(false);
        }

        float HeldHalfHeight()
        {
            if (Held == null) return 0;
            bool first = true;
            Bounds b = default;
            foreach (var p in Held.Parts)
            {
                if (p.Col == null || !p.Col.enabled) continue;
                if (first) { b = p.Col.bounds; first = false; } else b.Encapsulate(p.Col.bounds);
            }
            return first ? 0.05f : Held.WorldCom.y - b.min.y;
        }

        public void SetTopView(bool on, bool sound)
        {
            if (on == TopView) return;
            // 保持手里商品的朝向不因视角切换而跳变
            float oldYaw = ViewYaw;
            TopView = on;
            _holdRot = Quaternion.Inverse(Quaternion.Euler(0, ViewYaw, 0)) * Quaternion.Euler(0, oldYaw, 0) * _holdRot;
            if (on)
            {
                // 从当前看着的位置附近开始
                float fx = Mathf.Clamp(transform.position.x + Mathf.Sin(_yaw * Mathf.Deg2Rad) * 0.6f, TopMin.x, TopMax.x);
                _topFocus = new Vector2(fx, Layout.BagSlotZ);
                LockCursor(false);
                if (Paws != null) Paws.Walking = false;
            }
            else if (InputEnabled) LockCursor(true);
            if (sound) Sfx.Play("grab", 0.25f);
        }

        void LateUpdate()
        {
            // 相机姿态：第一人称与俯视之间平滑过渡
            ViewBlend = Mathf.MoveTowards(ViewBlend, TopView ? 1f : 0f, Time.deltaTime / 0.35f);
            float s = Mathf.SmoothStep(0, 1, ViewBlend);

            Vector3 fpPos = transform.TransformPoint(new Vector3(0, EyeHeight, 0));
            Quaternion fpRot = Quaternion.Euler(_pitch, _yaw, 0);

            Vector3 focus = new Vector3(_topFocus.x, Layout.CounterTop, _topFocus.y);
            Vector3 topPos = focus + new Vector3(0, _topHeight, -_topHeight * 0.28f);
            Quaternion topRot = Quaternion.LookRotation(focus + new Vector3(0, 0, 0.02f) - topPos, Vector3.forward);

            Cam.transform.SetPositionAndRotation(Vector3.Lerp(fpPos, topPos, s), Quaternion.Slerp(fpRot, topRot, s));
            if (Paws != null) Paws.gameObject.SetActive(ViewBlend < 0.5f);
            UpdateDropMarker();
        }

        void ClearLook()
        {
            LookItem = null; LookBag = null; LookRack = null; LookBell = false;
        }

        void UpdateLook(Ray ray, float dist)
        {
            ClearLook();
            if (!Physics.Raycast(ray, out var hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            LookItem = hit.collider.GetComponentInParent<GroceryItem>();
            if (LookItem != null) return;
            LookRack = hit.collider.GetComponent<BagRackButton>();
            if (LookRack != null) return;
            LookBell = hit.collider.GetComponent<FinishBell>() != null;
            if (LookBell) return;
            LookBag = hit.collider.GetComponentInParent<Bag>();
        }

        public void FocusSlot(int i) => _topFocus = new Vector2(Layout.BagSlotX[i], Layout.BagSlotZ);

        public void Grab(GroceryItem item)
        {
            Held = item;
            item.Held = true;
            var rb = item.Rb;
            rb.useGravity = false;
            rb.WakeUp();
            _holdDist = Mathf.Clamp(Vector3.Distance(Cam.transform.position, item.WorldCom), 0.35f, 1.2f);
            _holdRot = Quaternion.Inverse(Quaternion.Euler(0, ViewYaw, 0)) * rb.rotation;
            _lift = 0.06f;
            _heldOriginalLayer = item.gameObject.layer;
            U.SetLayerRecursive(item.gameObject, IgnoreRaycastLayer);
            Sfx.Play("grab", 0.6f);
            if (Paws != null) Paws.HoldTarget = item;
        }

        public void Release()
        {
            if (Held == null) return;
            var rb = Held.Rb;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 0.8f);
            rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 2f);
            U.SetLayerRecursive(Held.gameObject, _heldOriginalLayer);
            Held.Held = false;
            Held = null;
            _hasTopTarget = false;
            if (Paws != null) Paws.HoldTarget = null;
        }

        void FixedUpdate()
        {
            if (Held == null) return;
            if (Held.IsDestroyed || !Held.gameObject.activeInHierarchy) { Release(); return; }
            var rb = Held.Rb;
            Vector3 target;
            if (TopView) target = _hasTopTarget ? _topTarget : rb.worldCenterOfMass;
            else target = Cam.transform.position + Cam.transform.forward * _holdDist;
            Vector3 err = target - rb.worldCenterOfMass;

            // 卡住太远自动松手（俯视时鼠标移动快，放宽一些）
            if (err.magnitude > (TopView ? 1.5f : 0.7f)) { Release(); return; }

            float m = rb.mass;
            const float k = 320f;
            float c = 2f * Mathf.Sqrt(k);
            Vector3 accel = err * k - rb.linearVelocity * c;
            Vector3 extra = accel * m;
            float maxExtra = 6f + m * 22f;           // 猫爪力气上限
            extra = Vector3.ClampMagnitude(extra, maxExtra);
            // 拿着时关闭了重力，所以向下压的力最多就是猫爪力气上限
            rb.AddForce(extra, ForceMode.Force);

            // 旋转
            Quaternion targetRot = Quaternion.Euler(0, ViewYaw, 0) * _holdRot;
            Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 0.01f && !float.IsNaN(axis.x))
                rb.angularVelocity = Vector3.ClampMagnitude(axis * angle * Mathf.Deg2Rad * 10f, 7f);
            else rb.angularVelocity = Vector3.zero;
        }
    }
}
