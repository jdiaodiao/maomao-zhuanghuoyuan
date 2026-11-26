using System.Collections.Generic;
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
        Vector3 _fpTarget;
        bool _hasFpTarget;
        /// <summary>第一人称时手臂最远能伸出的水平距离（米）。</summary>
        const float MaxArm = 1.5f;
        bool _hasTopTarget;

        const int IgnoreRaycastLayer = 2;
        const float EyeHeight = 1.5f;
        static readonly Vector2 TopMin = new Vector2(-1.35f, -0.05f), TopMax = new Vector2(2.1f, 0.5f);

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
            // 暂停中：什么都不处理（手里的东西保持拿着）
            if (GameManager.I != null && GameManager.I.Paused) { ClearLook(); return; }
            if (!InputEnabled)
            {
                if (Held != null) Release();
                if (TopView) SetTopView(false, false);
                ClearLook();
                return;
            }

            if (Input.GetKeyDown(KeyCode.V)) SetTopView(!TopView, true);

            // 按压持续中：力度逐渐加大，计算附近商品的危险程度
            if (_pressItem != null)
            {
                if (!(Input.GetMouseButton(1) || _debugPressing) || _pressItem.IsDestroyed || !_pressItem.gameObject.activeInHierarchy) EndPress();
                else
                {
                    PressRisk = NearbyRisk(PressPoint);
                    if (PressRisk < RiskStop)
                    {
                        // 先慢后快：0.5 秒≈1N，1 秒≈5N，3 秒到最大力
                        _pressTime += Time.deltaTime;
                        PressForce = Mathf.Min(PressMax, PressMax * Mathf.Pow(_pressTime / PressRampTime, 2));
                    }
                    else
                    {
                        // 圆圈变红：不再加力，并稍微松一点，避免把东西压坏
                        PressForce = Mathf.Max(0, PressForce - PressForce * 0.6f * Time.deltaTime);
                        _pressTime = PressRampTime * Mathf.Sqrt(PressForce / PressMax);
                    }
                }
            }

            if (TopView) UpdateTopView();
            else if (!UpdateFirstPerson()) return;

            // ---------------- 交互（两种视角共用）----------------
            if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0 && !Placing && _pressItem == null)
            {
                if (LookItem != null && !LookItem.IsDestroyed) Grab(LookItem);
                else if (LookRack != null) GameManager.I.SpawnBag(LookRack.Spec);
                else if (LookBell) GameManager.I.FinishOrder();
                else if (LookBag != null) GameManager.I.RemoveBag(LookBag);   // 点一下空袋子 = 放回袋架（拿错了）
                else if (Paws != null && !TopView) Paws.Swipe();
            }
            // 松开左键 = 轻放（手带着商品慢慢往下伸，碰到东西才松开）；按住 Shift 松开 = 直接扔下
            if (Input.GetMouseButtonUp(0) && Held != null && !Placing)
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) Release();
                else BeginPlace();
            }

            // 右键按住 = 用爪子往下按压（只在空手时）
            if (Input.GetMouseButtonDown(1) && Held == null && LookItem != null && !LookItem.IsDestroyed) BeginPress(LookItem, _lookPoint);

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
            if (Input.GetKeyDown(KeyCode.Escape)) { GameManager.I.SetPaused(true); return false; }
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

            // 拿着东西：手臂自动伸缩——商品悬停在视线所指的表面上方（看近处手就缩回来，看远处就伸出去）
            _hasFpTarget = false;
            if (Held != null && !Placing)
            {
                _lift = Mathf.Clamp(_lift + Input.mouseScrollDelta.y * 0.02f, 0.015f, 0.35f);
                var fpRay = new Ray(Cam.transform.position, Cam.transform.forward);
                if (Physics.Raycast(fpRay, out var aim, Reach + 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 eye = Cam.transform.position;
                    Vector2 xz = new Vector2(aim.point.x, aim.point.z);
                    Vector2 eyeXZ = new Vector2(eye.x, eye.z);
                    // 手够不到太远：水平距离限制在 MaxArm 以内
                    Vector2 off = xz - eyeXZ;
                    if (off.magnitude > MaxArm) xz = eyeXZ + off.normalized * MaxArm;
                    _fpTarget = CarryTarget(xz);
                    _hasFpTarget = true;
                }
            }
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
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                _topFocus = new Vector2(Layout.CartCenter.x, Layout.CartCenter.z);
            _topFocus = Vector2.Max(TopMin, Vector2.Min(TopMax, _topFocus));

            float scroll = Input.mouseScrollDelta.y;
            if (Held != null) _lift = Mathf.Clamp(_lift + scroll * 0.02f, 0.015f, 0.35f);
            else _topHeight = Mathf.Clamp(_topHeight - scroll * 0.06f, 0.4f, 1.4f);

            var ray = Cam.ScreenPointToRay(Input.mousePosition);
            UpdateLook(ray, 6f);

            // 测试用：直接指定目标位置
            if (DebugTopTarget.HasValue && Held != null) { _topTarget = CarryTarget(DebugTopTarget.Value); _hasTopTarget = true; return; }

            // 拿着东西：目标点 = 鼠标指向的表面 + 商品半高 + 抬起高度
            _hasTopTarget = false;
            if (Held != null && Physics.Raycast(ray, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _topTarget = CarryTarget(new Vector2(hit.point.x, hit.point.z));
                _hasTopTarget = true;
            }
        }

        /// <summary>
        /// "手的轨迹"：沿当前位置→目标位置采样，用商品整个底面向下探测最高障碍（袋子边沿、其他商品）。
        /// 商品底部低于障碍时先原地抬起，越过后再平移，到了目标上方再降下去。
        /// </summary>
        Vector3 CarryTarget(Vector2 targetXZ)
        {
            GetHeldBounds(out Bounds b);
            Vector3 com = Held.WorldCom;
            Vector2 curXZ = new Vector2(com.x, com.z);
            Vector2 half = new Vector2(b.extents.x, b.extents.z) * 0.9f;
            float bottomOffset = com.y - b.min.y;

            // 路径上的最高表面（含起点和终点）
            float need = 0;
            const int samples = 6;
            for (int i = 0; i <= samples; i++)
                need = Mathf.Max(need, SurfaceHeight(Vector2.Lerp(curXZ, targetXZ, i / (float)samples), half));
            float targetSurface = SurfaceHeight(targetXZ, half);

            float carryY = need + bottomOffset + Mathf.Max(_lift, 0.02f);
            bool far = (targetXZ - curXZ).magnitude > 0.025f;
            if (far)
            {
                // 还没越过障碍 → 先抬起来，水平不动
                if (b.min.y < need + 0.008f) return new Vector3(curXZ.x, carryY + 0.01f, curXZ.y);
                return new Vector3(targetXZ.x, carryY, targetXZ.y);
            }
            // 已到目标上方 → 降到目标表面上方 _lift 的高度
            return new Vector3(targetXZ.x, targetSurface + bottomOffset + _lift, targetXZ.y);
        }

        void GetHeldBounds(out Bounds b)
        {
            bool first = true;
            b = new Bounds(Held.WorldCom, Vector3.one * 0.05f);
            foreach (var p in Held.Parts)
            {
                if (p.Col == null || !p.Col.enabled) continue;
                if (first) { b = p.Col.bounds; first = false; } else b.Encapsulate(p.Col.bounds);
            }
        }

        /// <summary>某个水平位置、给定底面尺寸下，最高的支撑面高度。</summary>
        static float SurfaceHeight(Vector2 xz, Vector2 halfXZ)
        {
            var origin = new Vector3(xz.x, 3f, xz.y);
            var half = new Vector3(Mathf.Max(halfXZ.x, 0.01f), 0.005f, Mathf.Max(halfXZ.y, 0.01f));
            if (Physics.BoxCast(origin, half, Vector3.down, out var hit, Quaternion.identity, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }


        // ================================================================ 轻放
        /// <summary>正在轻放：手带着商品慢慢往下伸，碰到东西就松开。</summary>
        public bool Placing { get; private set; }
        Vector2 _placeXZ;
        float _placeTime;
        int _placeStill;

        /// <summary>测试/外部调用：轻放手里的商品。</summary>
        public void PlaceGently() => BeginPlace();
        /// <summary>测试用：开始按压某件商品。</summary>
        public void DebugPress(GroceryItem item, Vector3 point) { _debugPressing = true; BeginPress(item, point); }
        bool _debugPressing;
        public void DebugEndPress() { _debugPressing = false; EndPress(); }

        void BeginPlace()
        {
            if (Held == null) return;
            Placing = true;
            var com = Held.WorldCom;
            _placeXZ = new Vector2(com.x, com.z);
            _placeTime = 0;
            _placeStill = 0;
        }

        void FinishPlace()
        {
            if (Held == null) { Placing = false; return; }
            var rb = Held.Rb;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Placing = false;
            Release();
            Sfx.Play("thud", 0.25f);
        }

        // ================================================================ 按压
        public const float PressMax = 45f;     // 猫爪最大按压力（N）
        const float PressRampTime = 3f;        // 从 0 加到最大力需要的时间（秒，先慢后快）
        /// <summary>风险达到这个值（圆圈变红）就停止加力。</summary>
        public const float RiskStop = 0.85f;
        float _pressTime;
        GroceryItem _pressItem;
        Vector3 _pressLocal;
        public float PressForce { get; private set; }
        /// <summary>附近商品中最高的承压比例（0~1+），用于圆圈颜色。</summary>
        public float PressRisk { get; private set; }
        public bool Pressing => _pressItem != null;
        public Vector3 PressPoint => _pressItem != null ? _pressItem.transform.TransformPoint(_pressLocal) : Vector3.zero;

        void BeginPress(GroceryItem item, Vector3 point)
        {
            _pressItem = item;
            _pressLocal = item.transform.InverseTransformPoint(point);
            PressForce = 0;
            _pressTime = 0;
            PressRisk = 0;
            item.Rb.WakeUp();
        }

        void EndPress()
        {
            _pressItem = null;
            PressForce = 0;
            if (Paws != null) Paws.PressPoint = null;
        }

        /// <summary>按压点附近（同一个袋子/一摞）所有商品里，最危险的部件承压比例。</summary>
        static float NearbyRisk(Vector3 p)
        {
            float risk = 0;
            foreach (var it in GroceryItem.Active)
            {
                if (it.IsDamaged || it.IsDestroyed) continue;
                Vector3 c = it.WorldCom;
                if (new Vector2(c.x - p.x, c.z - p.z).magnitude > 0.3f || c.y > p.y + 0.05f) continue;
                foreach (var part in it.Parts) risk = Mathf.Max(risk, part.LoadRatio);
            }
            return risk;
        }

        public static Color RiskColor(float risk) =>
            risk < 0.5f ? Color.Lerp(new Color(0.3f, 0.95f, 0.4f), new Color(1f, 0.85f, 0.15f), risk / 0.5f)
            : risk < 0.85f ? Color.Lerp(new Color(1f, 0.85f, 0.15f), new Color(1f, 0.45f, 0.1f), (risk - 0.5f) / 0.35f)
            : new Color(1f, 0.15f, 0.1f);

        // ================================================================ 圆圈（第一人称和俯视都有）
        GameObject _ring;
        MeshRenderer _ringR;
        readonly Dictionary<int, Material> _ringMats = new Dictionary<int, Material>();

        Material RingMat(Color c)
        {
            int key = Mathf.RoundToInt(c.r * 20) * 10000 + Mathf.RoundToInt(c.g * 20) * 100 + Mathf.RoundToInt(c.b * 20);
            if (!_ringMats.TryGetValue(key, out var m)) { m = Mats.Emissive(c, c * 1.3f); _ringMats[key] = m; }
            return m;
        }

        void UpdateRing()
        {
            if (_ring == null)
            {
                _ring = new GameObject("黄色圆圈");
                var v = new VisualSet();
                v.Add(Shapes.Torus(0.5f, 0.045f, 48, 6), RingMat(Color.yellow));
                _ringR = v.Spawn(_ring.transform, "环", default, null, false).GetComponent<MeshRenderer>();
            }
            Vector3? at = null;
            float r = 0.05f;
            Color col = new Color(1f, 0.85f, 0.15f);
            if (Pressing)
            {
                at = PressPoint;
                r = Mathf.Lerp(0.03f, 0.07f, PressForce / PressMax);
                col = RiskColor(PressRisk);
            }
            else if (Held != null)
            {
                Vector3 com = Held.WorldCom;
                if (Physics.Raycast(com, Vector3.down, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    at = hit.point;
                    r = Mathf.Clamp(Mathf.Max(Held.Def.Size.x, Held.Def.Size.z) * 0.6f, 0.03f, 0.25f);
                    // 下方商品的承压情况也会体现在圆圈颜色上
                    float risk = NearbyRisk(hit.point + Vector3.up * 0.02f);
                    col = risk > 0.5f ? RiskColor(risk) : new Color(1f, 0.85f, 0.15f);
                }
            }
            if (!at.HasValue) { _ring.SetActive(false); return; }
            _ring.SetActive(true);
            _ring.transform.position = at.Value + Vector3.up * 0.004f;
            _ring.transform.localScale = new Vector3(r, 1, r);
            _ringR.sharedMaterial = RingMat(col);
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
            UpdateRing();
            if (Paws != null) Paws.PressPoint = Pressing ? PressPoint : (Vector3?)null;
        }

        void ClearLook()
        {
            LookItem = null; LookBag = null; LookRack = null; LookBell = false;
        }

        Vector3 _lookPoint;

        void UpdateLook(Ray ray, float dist)
        {
            ClearLook();
            if (!Physics.Raycast(ray, out var hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            _lookPoint = hit.point;
            LookItem = hit.collider.GetComponentInParent<GroceryItem>();
            if (LookItem != null) return;
            LookRack = hit.collider.GetComponent<BagRackButton>();
            if (LookRack != null) return;
            LookBell = hit.collider.GetComponent<FinishBell>() != null;
            if (LookBell) return;
            LookBag = hit.collider.GetComponentInParent<Bag>();
        }

        /// <summary>自动测试用：代替鼠标指定俯视目标 (x,z)。</summary>
        public Vector2? DebugTopTarget;

        /// <summary>测试用：设置第一人称视角。</summary>
        public void DebugLook(float yaw, float pitch) { _yaw = yaw; _pitch = pitch; transform.rotation = Quaternion.Euler(0, yaw, 0); }

        public void FocusSlot(int i) => _topFocus = new Vector2(Layout.BagSlotX[i], Layout.BagSlotZ);

        public void Grab(GroceryItem item)
        {
            Held = item;
            item.Held = true;
            var rb = item.Rb;
            rb.useGravity = false;
            rb.WakeUp();
            // 叫醒附近睡着的商品：被拿走的东西原来压在它们上面，压力要重新算
            foreach (var o in GroceryItem.Active)
                if (o != item && o.Rb != null && (o.transform.position - item.transform.position).sqrMagnitude < 0.6f * 0.6f) o.Rb.WakeUp();
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
            Placing = false;
            if (Paws != null) Paws.HoldTarget = null;
        }

        void FixedUpdate()
        {
            // ---- 按压：爪子在按压点施加向下的力（也算作该部件的承压）
            if (_pressItem != null && PressForce > 0)
            {
                Vector3 p = PressPoint;
                _pressItem.Rb.AddForceAtPosition(Vector3.down * PressForce, p, ForceMode.Force);
                ItemPart nearest = null; float best = float.MaxValue;
                foreach (var part in _pressItem.Parts)
                {
                    if (part.Col == null || !part.Col.enabled) continue;
                    float dd = (part.Col.ClosestPoint(p) - p).sqrMagnitude;
                    if (dd < best) { best = dd; nearest = part; }
                }
                if (nearest != null) { nearest.LastPoint = p; nearest.LastNormal = Vector3.up; nearest.AddLoad(PressForce); }
            }

            if (Held == null) return;
            if (Held.IsDestroyed || !Held.gameObject.activeInHierarchy) { Placing = false; Release(); return; }
            var rb = Held.Rb;
            Vector3 target;
            if (Placing)
            {
                // 轻放：保持水平位置，慢慢往下（约 0.25m/s），停住了就松开
                _placeTime += Time.fixedDeltaTime;
                Vector3 com = rb.worldCenterOfMass;
                target = new Vector3(_placeXZ.x, com.y - 0.03f, _placeXZ.y);
                // 落到实处：下方出现支撑接触（连续几帧），或者已经停住不动
                bool supported = Time.time - Held.LastSupportTime < 0.03f;
                if (_placeTime > 0.06f && (supported || rb.linearVelocity.y > -0.02f)) _placeStill++; else _placeStill = 0;
                if (_placeStill >= 3 || _placeTime > 2f) { FinishPlace(); return; }
            }
            else if (TopView) target = _hasTopTarget ? _topTarget : rb.worldCenterOfMass;
            else target = _hasFpTarget ? _fpTarget : Cam.transform.position + Cam.transform.forward * _holdDist;
            Vector3 err = target - rb.worldCenterOfMass;

            // 卡住太远自动松手（俯视时鼠标移动快，放宽一些）
            if (!Placing && err.magnitude > 1.5f) { Release(); return; }

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
