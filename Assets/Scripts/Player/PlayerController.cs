using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 第一人称猫爪控制。
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
        Quaternion _holdRot = Quaternion.identity;   // 相对于水平朝向的持有姿态
        int _heldOriginalLayer;

        public GroceryItem LookItem { get; private set; }
        public Bag LookBag { get; private set; }
        public BagRackButton LookRack { get; private set; }
        public bool LookBell { get; private set; }

        const int IgnoreRaycastLayer = 2;

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

        void Update()
        {
            if (!InputEnabled)
            {
                if (Held != null) Release();
                ClearLook();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (!CursorLocked)
            {
                if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0) LockCursor(true);
                ClearLook();
                return;
            }

            // 视角
            _yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * MouseSensitivity, -40f, 85f);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            Cam.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);

            // 移动（小范围）
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

            UpdateLook();

            // 交互
            if (Input.GetMouseButtonDown(0))
            {
                if (LookItem != null && !LookItem.IsDestroyed) Grab(LookItem);
                else if (LookRack != null) GameManager.I.SpawnBag(LookRack.Spec);
                else if (LookBell) GameManager.I.FinishOrder();
                else if (Paws != null) Paws.Swipe();
            }
            if (Input.GetMouseButtonUp(0) && Held != null) Release();

            if (Held != null)
            {
                _holdDist = Mathf.Clamp(_holdDist + Input.mouseScrollDelta.y * 0.06f, 0.35f, 1.6f);
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

        void ClearLook()
        {
            LookItem = null; LookBag = null; LookRack = null; LookBell = false;
        }

        void UpdateLook()
        {
            ClearLook();
            var ray = new Ray(Cam.transform.position, Cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            LookItem = hit.collider.GetComponentInParent<GroceryItem>();
            if (LookItem != null) return;
            LookRack = hit.collider.GetComponent<BagRackButton>();
            if (LookRack != null) return;
            LookBell = hit.collider.GetComponent<FinishBell>() != null;
            if (LookBell) return;
            LookBag = hit.collider.GetComponentInParent<Bag>();
        }

        void Grab(GroceryItem item)
        {
            Held = item;
            item.Held = true;
            var rb = item.Rb;
            rb.useGravity = false;
            rb.WakeUp();
            _holdDist = Mathf.Clamp(Vector3.Distance(Cam.transform.position, item.WorldCom), 0.35f, 1.2f);
            _holdRot = Quaternion.Inverse(Quaternion.Euler(0, _yaw, 0)) * rb.rotation;
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
            if (Paws != null) Paws.HoldTarget = null;
        }

        void FixedUpdate()
        {
            if (Held == null) return;
            if (Held.IsDestroyed || !Held.gameObject.activeInHierarchy) { Release(); return; }
            var rb = Held.Rb;
            Vector3 target = Cam.transform.position + Cam.transform.forward * _holdDist;
            Vector3 err = target - rb.worldCenterOfMass;

            // 卡住太远自动松手
            if (err.magnitude > 0.7f) { Release(); return; }

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
            Quaternion targetRot = Quaternion.Euler(0, _yaw, 0) * _holdRot;
            Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 0.01f && !float.IsNaN(axis.x))
                rb.angularVelocity = Vector3.ClampMagnitude(axis * angle * Mathf.Deg2Rad * 10f, 7f);
            else rb.angularVelocity = Vector3.zero;
        }
    }
}
