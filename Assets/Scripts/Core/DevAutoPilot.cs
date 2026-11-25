using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 开发用自动测试（命令行 -catmartAutotest 启动）：
    /// 跑一单完整流程 + 压坏/摔碎/刺破测试，截图并在日志里输出结果，然后退出。
    /// </summary>
    public class DevAutoPilot : MonoBehaviour
    {
        string _dir;

        IEnumerator Start()
        {
            _dir = Path.Combine(Application.dataPath, "..", "AutotestShots");
            Directory.CreateDirectory(_dir);
            var g = GameManager.I;
            Log($"商品数量 {ItemCatalog.All.Count}");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("0_intro");

            g.ResetProgress();
            g.StartDay();
            var cam = g.Player.Cam;

            // 等商品全部送到
            Time.timeScale = 2f;
            float t = 0;
            while ((!g.Store.Feeder.Done || AnyMoving()) && t < 40f) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1f);
            Log($"订单: {g.Order.Items.Count} 件, 要求 {g.Order.BagRequestText}, 送达 {g.DeliveredCount}, 传送带运行={g.Store.Belt.Running}");
            foreach (var it in g.OrderItems) Log($"  {it.Def.Name} pos={it.transform.position} damaged={it.IsDamaged}");
            g.Player.transform.position = new Vector3(-0.6f, 0, -0.6f);
            SetView(g.Player, -40, 35);
            yield return Shot("1_belt");

            // 按要求取袋子
            foreach (var kv in g.Order.Bags)
                for (int i = 0; i < kv.Value; i++) g.SpawnBag(BagSpec.Get(g.Order.Material, kv.Key));
            yield return null;

            // 把商品分配进袋子：重的先放，放到当前最轻的袋子中心上方
            var bags = g.Slots.Where(b => b != null).ToList();
            var items = g.OrderItems.Where(i => i != null && !i.IsDestroyed).OrderByDescending(i => i.Def.Mass).ToList();
            var load = bags.ToDictionary(b => b, b => 0f);
            foreach (var it in items)
            {
                if (it.Def.Bulky)
                {
                    Place(it, g.Store.Cart.position + new Vector3(0, Layout.CartFloorY + 0.03f, 0));
                }
                else
                {
                    var target = load.OrderBy(kv => kv.Value / kv.Key.Spec.CapacityKg).First().Key;
                    load[target] += it.Def.Mass;
                    var top = HighestPoint(target);
                    Place(it, target.transform.position + new Vector3(0, top + 0.015f, 0), target);
                }
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(2.5f);
            g.Player.transform.position = new Vector3(0, 0, -0.6f);
            SetView(g.Player, 0, 42);
            g.ShowReceipt = true;
            yield return Shot("2_bagged");

            // 俯视装袋视角
            g.ShowReceipt = false;
            g.Player.enabled = true;
            g.Player.SetTopView(true, false);
            g.Player.FocusSlot(0);
            yield return new WaitForSeconds(0.8f);
            foreach (var bg in g.Slots) if (bg != null) { var df = bg.GetComponentInChildren<BagWallDeformer>(); var st2 = bg.Compute(); int ins = VisualPokeOut(bg, out string who); Log($"袋壁形变: {bg.Spec.Name} {st2.Count}件 最大外推 {df.MaxPush * 100:0.0}cm 高出袋口 {st2.Unstable}件 商品画面穿出袋壁 {ins} [{who}]"); }
            yield return Shot("2b_topview");
            // 轨迹测试：把放在袋子旁边台面上的麦片拿进 1 号袋（需要越过袋壁）
            var bag1 = g.Slots[0];
            var extra = ItemFactory.Create(ItemCatalog.Get("canned_soup"), new Vector3(-0.425f, Layout.CounterTop + 0.06f, -0.04f), Quaternion.identity);
            yield return new WaitForSeconds(0.8f);
            g.Player.Grab(extra);
            g.Player.DebugTopTarget = new Vector2(bag1.transform.position.x, bag1.transform.position.z);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("2c_topview_hold");
            yield return new WaitForSeconds(2.5f);
            // 轻放：手带着罐头往下伸，碰到东西才松开
            float yBefore = extra.WorldCom.y;
            g.Player.PlaceGently();
            float pt = 0;
            while (g.Player.Placing && pt < 3f) { pt += Time.deltaTime; yield return null; }
            g.Player.DebugTopTarget = null;
            Log($"轻放测试: 下降 {(yBefore - extra.WorldCom.y) * 100:0.0}cm 用时 {pt:0.00}s 松手时速度 {extra.Rb.linearVelocity.magnitude:0.00}m/s (期望 <0.1)");
            yield return new WaitForSeconds(1.5f);
            Log($"轨迹测试: 罐头从台面越过袋壁放进1号袋 inBag={bag1.Contains(extra)} damaged={extra.IsDamaged} pos={extra.transform.position} (期望 inBag=True)");
            yield return Shot("2d_topview_placed");
            Destroy(extra.gameObject);
            g.Player.SetTopView(false, false);
            g.Player.enabled = false;
            g.ShowReceipt = false;

            g.FinishOrder();
            var r = g.LastResult;
            Log($"结算: 倍率 {r.Multiplier:0.00} (规格{r.ReqFactor} 平衡{r.BalanceFactor:0.00} 耐心{r.PatienceFactor:0.00} 漏装{r.MissingFactor:0.00}) 工钱 {r.Pay:0.00} 赔偿 {r.DamagePenalty:0.00} 实得 {r.Net:0.00} 星 {r.Stars}");
            Log($"  规格: {r.ReqNote}; 漏装: {string.Join(",", r.MissingNames)}; 损坏: {string.Join(",", r.DamagedNames)}");
            foreach (var b in r.Bags) Log($"  袋 {b.Spec.Name} {b.ItemCount}件 {b.Mass:0.0}kg 偏心{b.Imbalance:0.00} x{b.Factor:0.00} {b.Note}");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("3_settlement");

            // ---------------- 物理破坏测试 ----------------
            g.NextCustomer();
            g.Store.Feeder.Stop();
            foreach (var it in g.OrderItems) Destroy(it.gameObject);
            g.OrderItems.Clear();
            yield return null;
            float top0 = Layout.CounterTop;

            // 1) 牛奶压薯片
            var chips = ItemFactory.Create(ItemCatalog.Get("chips"), new Vector3(0.64f, top0 + 0.05f, 0.25f), Quaternion.Euler(90, 0, 0));
            // 2) 鸡蛋从 60cm 摔下
            var eggs = ItemFactory.Create(ItemCatalog.Get("eggs"), new Vector3(-0.2f, top0 + 0.6f, 0.2f), Quaternion.identity);
            var eggs2 = ItemFactory.Create(ItemCatalog.Get("eggs"), new Vector3(0.64f, top0 + 0.12f, 0.0f), Quaternion.identity);
            eggs.Rb.isKinematic = true; eggs2.Rb.isKinematic = true;
            yield return new WaitForSeconds(0.8f);
            eggs.Rb.isKinematic = false; eggs2.Rb.isKinematic = false;
            // 3) 罐头放在面包上（不应压坏罐头，面包 12N 上限，罐头 3.4N 不应压坏）
            var bread = ItemFactory.Create(ItemCatalog.Get("bread"), new Vector3(0.22f, top0 + 0.065f, 0.2f), Quaternion.identity);
            // 4) 塑料袋 + 竹签 + 重物压
            g.SpawnBag(BagSpec.Get(BagMaterial.Plastic, BagSize.Medium)); // 会进第一个空位
            yield return new WaitForSeconds(1.0f);
            var milk = ItemFactory.Create(ItemCatalog.Get("milk_gallon"), new Vector3(0.64f, top0 + 0.3f, 0.25f), Quaternion.identity);
            var soup = ItemFactory.Create(ItemCatalog.Get("canned_soup"), new Vector3(0.22f, top0 + 0.2f, 0.2f), Quaternion.identity);
            var bag = g.Slots.First(b => b != null);
            var skewers = ItemFactory.Create(ItemCatalog.Get("skewers"), bag.transform.position + new Vector3(0, 0.03f, 0), Quaternion.Euler(0, 0, 90 - 90));
            var detergent = ItemFactory.Create(ItemCatalog.Get("detergent"), bag.transform.position + new Vector3(0, 0.25f, 0), Quaternion.identity);
            g.Player.transform.position = new Vector3(0, 0, -0.6f);
            SetView(g.Player, 0, 40);
            yield return new WaitForSeconds(3f);
            Log($"破坏测试: 薯片被牛奶压 damaged={chips.IsDamaged} (期望 True)");
            Log($"破坏测试: 鸡蛋 60cm 摔落 damaged={eggs.IsDamaged} (期望 True)");
            Log($"破坏测试: 鸡蛋 8cm 轻放 damaged={eggs2.IsDamaged} (期望 False)");
            Log($"破坏测试: 面包上放罐头 bread.damaged={bread.IsDamaged} (期望 False, 0.35kg≈3.4N < 12N)");
            Log($"破坏测试: 牛奶 damaged={milk.IsDamaged} 罐头 damaged={soup.IsDamaged} (期望 False)");
            Log($"破坏测试: 竹签+洗衣液压在塑料袋里 torn={bag.Torn} (期望 True)");
            yield return Shot("4_damage");

            // 按压测试：结实的一摞（罐头压在米袋上）按 2 秒不该坏；罐头压在薯片上按一下就会变红并压坏
            var rice = ItemFactory.Create(ItemCatalog.Get("rice"), new Vector3(1.0f, top0 + 0.05f, 0.25f), Quaternion.identity);
            var chips2 = ItemFactory.Create(ItemCatalog.Get("chips"), new Vector3(1.0f, top0 + 0.05f, -0.05f), Quaternion.Euler(90, 0, 0));
            yield return new WaitForSeconds(0.8f);
            var canA = ItemFactory.Create(ItemCatalog.Get("canned_beans"), new Vector3(1.0f, top0 + 0.17f, 0.25f), Quaternion.identity);
            var canB = ItemFactory.Create(ItemCatalog.Get("tissues"), new Vector3(1.0f, top0 + 0.16f, -0.05f), Quaternion.identity);
            yield return new WaitForSeconds(1.0f);
            g.Player.enabled = true;
            float maxRisk = 0;
            g.Player.DebugPress(canA, canA.WorldCom + Vector3.up * 0.05f);
            for (float t2 = 0; t2 < 2f; t2 += Time.deltaTime) { maxRisk = Mathf.Max(maxRisk, g.Player.PressRisk); yield return null; }
            Log($"按压测试: 罐头压米袋 2秒 力 {g.Player.PressForce:0}N 最高风险 {maxRisk:0.00} 米袋坏={rice.IsDamaged} (期望 False)");
            g.Player.DebugEndPress();
            maxRisk = 0;
            Log($"按压前: 薯片坏={chips2.IsDamaged} 薯片受压={chips2.Parts[0].LoadRatio:0.00}");
            g.Player.DebugPress(canB, canB.WorldCom + Vector3.up * 0.05f);
            for (float t2 = 0; t2 < 4f; t2 += Time.deltaTime) { maxRisk = Mathf.Max(maxRisk, g.Player.PressRisk); if (Time.frameCount % 25 == 0) Log($"  t={t2:0.00} F={g.Player.PressForce:0.0} risk={g.Player.PressRisk:0.00} chipsLoad={chips2.Parts[0].Load:0.0} dmg={chips2.IsDamaged}"); yield return null; }
            Log($"按压测试: 抽纸压薯片 力 {g.Player.PressForce:0}N 最高风险 {maxRisk:0.00} 薯片坏={chips2.IsDamaged} (期望 风险停在0.85附近 且 薯片没坏=False)");
            g.Player.DebugEndPress();
            g.Player.enabled = false;

            // 4a) 第一人称手臂自动伸缩：从传送带末端拿东西，低头看面前台面，东西应该收回来放到面前
            yield return ReachTest(g);

            // 4b) 穿模测试：像玩家一样在俯视模式里把薄而大的零食一件件轻放进袋子
            yield return ClipTest(g);

            // 5) 各商品外观总览
            g.NextCustomer();
            g.Store.Feeder.Stop();
            foreach (var it in g.OrderItems) Destroy(it.gameObject);
            g.OrderItems.Clear();
            g.Store.Customer.gameObject.SetActive(false);
            var all = ItemCatalog.All;
            g.Player.enabled = false;
            g.Player.Paws.gameObject.SetActive(false);
            g.ShowHelp = false;
            HUD.Hidden = true;
            // 商品特写：每组 18 件，分两排摆在地上
            const int per = 6, cols = 6;
            for (int group = 0; group * per < all.Count; group++)
            {
                var spawned = new List<GroceryItem>();
                for (int k = 0; k < per && group * per + k < all.Count; k++)
                {
                    var d = all[group * per + k];
                    int cx = k % cols, cz = k / cols;
                    var p = new Vector3(3.0f + cx * 0.32f, 0.005f + d.Size.y / 2, 0.5f + cz * 0.42f);
                    var it = ItemFactory.Create(d, p, Quaternion.Euler(0, (cx - 4) * 4, 0));
                    it.Rb.isKinematic = true;
                    spawned.Add(it);
                }
                g.Player.Cam.transform.position = new Vector3(3.8f, 0.55f, -0.22f);
                g.Player.Cam.transform.rotation = Quaternion.Euler(32, 0, 0);
                yield return new WaitForSeconds(0.2f);
                yield return Shot($"6_items_{group}");
                foreach (var it in spawned) Destroy(it.gameObject);
                yield return null;
            }
            // 全景
            g.Player.Cam.transform.position = new Vector3(0.9f, 1.75f, -1.1f);
            g.Player.Cam.transform.rotation = Quaternion.Euler(24, -25, 0);
            g.Store.Customer.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("7_store");

            Log("自动测试完成");
            Application.Quit();
        }

        IEnumerator ReachTest(GameManager g)
        {
            var p = g.Player;
            p.transform.position = new Vector3(-0.3f, 0, -0.6f);
            p.enabled = true;
            PlayerController.LockCursor(true);
            var box = ItemFactory.Create(ItemCatalog.Get("cereal"), new Vector3(-1.2f, Layout.CounterTop + 0.16f, Layout.LaneZ), Quaternion.identity);
            yield return new WaitForSeconds(0.8f);
            p.DebugLook(-60, 38);           // 看着传送带末端
            yield return null;
            p.Grab(box);
            yield return new WaitForSeconds(0.6f);
            p.DebugLook(0, 50);             // 低头看面前的台面
            yield return new WaitForSeconds(1.2f);
            yield return Shot("4a_reach_hold");
            Vector3 eye = p.Cam.transform.position;
            float dHold = new Vector2(box.WorldCom.x - eye.x, box.WorldCom.z - eye.z).magnitude;
            p.PlaceGently();
            for (float t = 0; t < 3f && p.Placing; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.8f);
            Vector3 c = box.WorldCom;
            bool onTable = c.y < Layout.CounterTop + 0.25f && c.y > Layout.CounterTop && c.z > -0.12f && c.z < 0.5f && Mathf.Abs(c.x) < 0.9f;
            Log($"手臂伸缩测试: 拿着时离眼睛水平 {dHold:0.00}m; 放下后位置 {c} 在面前台面上={onTable} 损坏={box.IsDamaged} (期望 True/False)");
            Destroy(box.gameObject);
            p.enabled = false;
        }

        IEnumerator ClipTest(GameManager g)
        {
            g.NextCustomer();
            g.Store.Feeder.Stop();
            foreach (var it in g.OrderItems) Destroy(it.gameObject);
            g.OrderItems.Clear();
            yield return null;
            g.SpawnBag(BagSpec.Get(BagMaterial.Plastic, BagSize.Medium));
            g.SpawnBag(BagSpec.Get(BagMaterial.Paper, BagSize.Medium));
            var bags = new[] { g.Slots[0], g.Slots[1] };
            string[] ids = { "chips", "tortilla_chips", "cereal", "bread", "cookies" };
            g.Player.enabled = true;
            g.Player.SetTopView(true, false);
            g.Player.FocusSlot(0);
            var placed = new List<GroceryItem>();
            for (int b = 0; b < 2; b++)
            {
                var bag = bags[b];
                for (int k = 0; k < ids.Length; k++)
                {
                    var d = ItemCatalog.Get(ids[k]);
                    // 放在台面靠近玩家一侧，像刚从传送带拿过来
                    var it = ItemFactory.Create(d, new Vector3(0.45f, Layout.CounterTop + d.Size.y / 2 + 0.01f, -0.02f), Quaternion.identity);
                    placed.Add(it);
                    yield return new WaitForSeconds(0.4f);
                    g.Player.Grab(it);
                    var target = new Vector2(bag.transform.position.x + (k % 2 - 0.5f) * 0.05f, bag.transform.position.z);
                    g.Player.DebugTopTarget = target;
                    for (float t = 0; t < 3f; t += Time.deltaTime)
                    {
                        var c = it.WorldCom;
                        if (new Vector2(c.x - target.x, c.z - target.y).magnitude < 0.02f) break;
                        yield return null;
                    }
                    g.Player.PlaceGently();
                    for (float t = 0; t < 3f && g.Player.Placing; t += Time.deltaTime) yield return null;
                    g.Player.DebugTopTarget = null;
                    yield return new WaitForSeconds(0.3f);
                }
            }
            g.Player.SetTopView(false, false);
            g.Player.enabled = false;
            yield return new WaitForSeconds(2f);

            for (int b = 0; b < 2; b++)
            {
                var bag = bags[b];
                var df = bag.GetComponentInChildren<BagWallDeformer>();
                int inside = df.DebugVertsInsideItems(out string who);
                // 物理穿透：商品碰撞体 vs 袋壁碰撞体
                float maxDepth = 0; string deepest = "";
                var bagCols = bag.GetComponentsInChildren<BoxCollider>();
                foreach (var it in placed)
                foreach (var part in it.Parts)
                foreach (var bc in bagCols)
                {
                    if (part.Col == null || !part.Col.enabled) continue;
                    if (Physics.ComputePenetration(part.Col, part.Col.transform.position, part.Col.transform.rotation,
                            bc, bc.transform.position, bc.transform.rotation, out _, out float depth) && depth > maxDepth)
                    { maxDepth = depth; deepest = it.Def.Name; }
                }
                int inBag = placed.Count(it => bag.Contains(it));
                int poke = VisualPokeOut(bag, out string pokeWho);
                Log($"穿模测试 {bag.Spec.Name}: 袋内 {inBag} 件; 商品画面穿出袋壁 {poke} 个顶点 [{pokeWho}]; 袋壁顶点在商品碰撞体内 {inside} [{who}]; 最大物理穿透 {maxDepth * 100:0.0}cm ({deepest})");
            }
            g.Player.transform.position = new Vector3(-0.4f, 0, -0.6f);
            g.Player.Cam.transform.position = new Vector3(-0.4f, 1.25f, -0.45f);
            g.Player.Cam.transform.rotation = Quaternion.Euler(38, 0, 0);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("4c_cliptest");
            foreach (var it in placed) if (it != null) Destroy(it.gameObject);
        }

        /// <summary>诊断：袋内商品的画面顶点有多少穿出了袋壁（袋口以下、超出袋壁 1cm 以上）。</summary>
        static int VisualPokeOut(Bag bag, out string who)
        {
            int count = 0;
            var tally = new Dictionary<string, int>();
            Vector3 h = bag.Spec.Inner * 0.5f;
            foreach (var it in bag.Contents())
            foreach (var mf in it.GetComponentsInChildren<MeshFilter>())
            {
                var v = mf.sharedMesh.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = bag.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                    if (p.y > bag.Spec.Inner.y - 0.01f) continue;
                    float m = 0.01f + bag.Spec.Stretch;
                    if (Mathf.Abs(p.x) > h.x + m || Mathf.Abs(p.z) > h.z + m || p.y < -0.01f)
                    {
                        count++;
                        tally.TryGetValue(it.Def.Name, out int k);
                        tally[it.Def.Name] = k + 1;
                    }
                }
            }
            who = string.Join(", ", tally.Select(kv => $"{kv.Key}:{kv.Value}"));
            return count;
        }

        bool AnyMoving()
        {
            foreach (var it in GameManager.I.OrderItems)
                if (it != null && !it.IsDestroyed && it.Rb.linearVelocity.magnitude > 0.02f) return true;
            return false;
        }

        float HighestPoint(Bag bag)
        {
            float top = 0;
            foreach (var it in bag.Contents())
            {
                foreach (var c in it.GetComponentsInChildren<Collider>())
                    top = Mathf.Max(top, bag.transform.InverseTransformPoint(c.bounds.max).y);
            }
            return top;
        }

        /// <summary>平放（最短边朝上），bottom 为底部落点。</summary>
        void Place(GroceryItem it, Vector3 bottom, Bag bag = null)
        {
            it.Rb.linearVelocity = Vector3.zero;
            it.Rb.angularVelocity = Vector3.zero;
            var s = it.Def.Size;
            // 在 6 种轴向摆法里，选底面放得进袋子、且高度最低的（放不进就选最接近的）
            Vector3[] eulers = { new Vector3(0, 0, 0), new Vector3(0, 90, 0), new Vector3(90, 0, 0), new Vector3(90, 90, 0), new Vector3(0, 0, 90), new Vector3(0, 90, 90) };
            Quaternion rot = Quaternion.identity;
            float h = s.y, bestScore = float.MaxValue;
            foreach (var e in eulers)
            {
                var q = Quaternion.Euler(e);
                Vector3 r = new Vector3(
                    Mathf.Abs((q * Vector3.right).x) * s.x + Mathf.Abs((q * Vector3.up).x) * s.y + Mathf.Abs((q * Vector3.forward).x) * s.z,
                    Mathf.Abs((q * Vector3.right).y) * s.x + Mathf.Abs((q * Vector3.up).y) * s.y + Mathf.Abs((q * Vector3.forward).y) * s.z,
                    Mathf.Abs((q * Vector3.right).z) * s.x + Mathf.Abs((q * Vector3.up).z) * s.y + Mathf.Abs((q * Vector3.forward).z) * s.z);
                float over = 0;
                if (bag != null) over = Mathf.Max(0, r.x - bag.Spec.Inner.x + 0.01f) + Mathf.Max(0, r.z - bag.Spec.Inner.z + 0.01f);
                float score = over * 100 + r.y;
                if (score < bestScore) { bestScore = score; rot = q; h = r.y; }
            }
            var pos = bottom + Vector3.up * (h / 2);
            it.Rb.position = pos;
            it.Rb.rotation = rot;
            it.transform.SetPositionAndRotation(pos, rot);
        }

        void SetView(PlayerController p, float yaw, float pitch)
        {
            p.transform.rotation = Quaternion.Euler(0, yaw, 0);
            p.Cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            p.enabled = false;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log("截图 " + path);
        }

        static void Log(string s) => Debug.Log("[AUTOTEST] " + s);
    }
}
