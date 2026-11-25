using System.Linq;
using System.Text;
using UnityEngine;

namespace CatMart
{
    /// <summary>IMGUI 界面（项目没装 uGUI/TMP，IMGUI 零依赖）。</summary>
    public class HUD : MonoBehaviour
    {
        GameManager G => GameManager.I;
        PlayerController P => G != null ? G.Player : null;

        GUIStyle _label, _title, _small, _panel, _button, _center, _big, _mono;
        Texture2D _bg, _bgLight, _white;
        float _scale = 1;
        int _builtForHeight = -1;

        static readonly Color Orange = U.Hex("#F28C28");

        void BuildStyles()
        {
            _scale = Mathf.Max(0.6f, Screen.height / 1080f);
            if (_builtForHeight == Screen.height && _label != null) return;
            _builtForHeight = Screen.height;

            _bg = Tex(new Color(0.08f, 0.09f, 0.12f, 0.82f));
            _bgLight = Tex(new Color(1f, 0.98f, 0.94f, 0.95f));
            _white = Tex(Color.white);
            var font = Fonts.Main;
            int S(int px) => Mathf.RoundToInt(px * _scale);

            _label = new GUIStyle { font = font, fontSize = S(22), richText = true, wordWrap = true };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = S(17) };
            _small.normal.textColor = new Color(0.85f, 0.87f, 0.9f);
            _title = new GUIStyle(_label) { fontSize = S(34), fontStyle = FontStyle.Bold };
            _title.normal.textColor = Orange;
            _big = new GUIStyle(_label) { fontSize = S(60), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Orange;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _mono = new GUIStyle(_label) { fontSize = S(17), wordWrap = false };
            _mono.normal.textColor = new Color(0.15f, 0.15f, 0.2f);
            _panel = new GUIStyle { padding = new RectOffset(S(18), S(18), S(14), S(14)) };
            _panel.normal.background = _bg;
            _button = new GUIStyle { font = font, fontSize = S(26), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _button.normal.background = Tex(Orange);
            _button.hover.background = Tex(Color.Lerp(Orange, Color.white, 0.2f));
            _button.active.background = Tex(Color.Lerp(Orange, Color.black, 0.2f));
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = Color.white;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        float S(float v) => v * _scale;

        public static bool Hidden;

        void OnGUI()
        {
            if (G == null || Hidden) return;
            BuildStyles();
            switch (G.Phase)
            {
                case GameManager.State.Intro: DrawIntro(); break;
                case GameManager.State.Serving: DrawServing(); break;
                case GameManager.State.Settlement: DrawServingTop(); DrawSettlement(); break;
                case GameManager.State.DaySummary: DrawDaySummary(); break;
            }
            DrawVolume();
        }

        // ------------------------------------------------------------ 开场
        void DrawIntro()
        {
            float w = S(980), h = S(760);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label("猫猫装货员", _big);
            GUILayout.Label("第一章 · CatMart 超市装货员", new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(S(14));
            GUILayout.Label(
                "你是 CatMart 喵喵超市 3 号收银台的装袋员。上游的收银猫扫描好的商品会顺着传送带送到你面前，" +
                "请按顾客的要求，用指定数量和尺寸的<b>塑料袋</b>或<b>纸袋</b>把商品装好。\n\n" +
                "<color=#F2C14E>评分</color>：袋子里的<b>重心要居中</b>（不能一边重一边轻）、袋子<b>规格要对</b>、" +
                "<b>不能压坏</b>商品（压坏要按原价赔！）。尖锐的东西会扎破塑料袋和软商品，玻璃瓶和鸡蛋摔不得。" +
                "大件（整箱水、猫砂…）直接放顾客的购物车。\n\n" +
                $"每天接待 {GameManager.CustomersPerDay} 位顾客，工钱 = 订单总价 × 10% × 评分倍率 − 赔偿。", _label);
            GUILayout.Space(S(10));
            GUILayout.Label(ControlsText(), _small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"第 {G.Day} 天   钱包 {U.Money(G.Money)}", _label);
            GUILayout.FlexibleSpace();
            if (G.Day > 1 || G.Money != 0)
                if (GUILayout.Button("重置存档", _button, GUILayout.Width(S(160)), GUILayout.Height(S(56)))) G.ResetProgress();
            GUILayout.Space(S(12));
            if (GUILayout.Button("开始营业 ▶", _button, GUILayout.Width(S(260)), GUILayout.Height(S(56)))) G.StartDay();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string ControlsText() =>
            "鼠标 看   WASD 移动   左键按住 抓起（松开=轻放，Shift+松开=扔下）   <color=#F2C14E>右键按住 按压</color>（圆圈变红就停）\n" +
            "滚轮 远近   Q/E 水平转   R/F 前后翻   C 侧放   Z 回正   X 收回空袋\n" +
            "<color=#F2C14E><b>V 俯视装袋</b></color>   Tab 小票   B 重心标记   Enter 完成订单   H 隐藏帮助   M 静音  -/= 音量   Esc 释放鼠标";

        // ------------------------------------------------------------ 营业中
        void DrawServingTop()
        {
            // 左上：状态
            var r = new Rect(S(16), S(16), S(430), S(96));
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label($"<b>第 {G.Day} 天</b> · 顾客 {Mathf.Min(G.CustomerIndex + (G.Phase == GameManager.State.Serving ? 1 : 0), GameManager.CustomersPerDay)}/{GameManager.CustomersPerDay}", _label);
            GUILayout.Label($"钱包 <color=#7CFC9A><b>{U.Money(G.Money)}</b></color>", _label);
            GUILayout.EndArea();
        }

        void DrawServing()
        {
            DrawServingTop();
            var o = G.Order;
            if (o == null) return;

            // 左上：顾客要求 + 耐心（不挡住正前方的顾客）
            float w = S(560);
            var rr = new Rect(S(16), S(120), w, S(118));
            GUILayout.BeginArea(rr, _panel);
            GUILayout.Label($"<b>{o.CustomerName}</b> 的要求：<color=#F2C14E><b>{o.BagRequestText}</b></color>" +
                            (o.Items.Any(i => i.Bulky) ? "  <color=#9EC9FF>（大件放推车）</color>" : ""), _label);
            GUILayout.Label($"订单 {o.Items.Count} 件 · 总价 {U.Money(o.ItemsTotal)} · 基础工钱 {U.Money(o.ItemsTotal * Settlement.Commission)}", _small);
            GUILayout.EndArea();
            var bar = new Rect(rr.x + S(18), rr.yMax - S(26), w - S(36), S(12));
            GUI.color = new Color(0.25f, 0.25f, 0.28f);
            GUI.DrawTexture(bar, _white);
            float p = G.Patience01;
            GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.25f), new Color(0.35f, 0.9f, 0.4f), p);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p, bar.height), _white);
            GUI.color = Color.white;

            // 右侧：进度和袋子
            var sb = new StringBuilder();
            sb.AppendLine($"送达 {G.DeliveredCount}/{o.Items.Count}   已装 {G.BaggedCount}" + (G.DamagedCount > 0 ? $"   <color=#FF6B5B>损坏 {G.DamagedCount}</color>" : ""));
            sb.AppendLine(G.Store.Belt.Running ? "<color=#7CFC9A>传送带运行中</color>" : "<color=#FFB347>传送带已停（末端有货）</color>");
            for (int i = 0; i < G.Slots.Length; i++)
            {
                var b = G.Slots[i];
                if (b == null) { sb.AppendLine($"<color=#888888>{i + 1}号位 空</color>"); continue; }
                var s = b.Compute();
                string bal = s.Count == 0 ? "-" : $"{s.Imbalance * 100:0}%";
                string col = s.Imbalance <= 0.15f ? "#7CFC9A" : s.Imbalance <= 0.35f ? "#F2C14E" : "#FF6B5B";
                string kg = s.Overweight ? $"<color=#FF6B5B>{s.Mass:0.0}/{b.Spec.CapacityKg}kg</color>" : $"{s.Mass:0.0}/{b.Spec.CapacityKg}kg";
                sb.AppendLine($"{i + 1}号 {b.Spec.Name} {s.Count}件 {kg} 偏心 <color={col}>{bal}</color>" + (s.Unstable > 0 ? $" <color=#FFB347>高出{s.Unstable}</color>" : "") + (b.Torn ? " <color=#FF6B5B>破</color>" : ""));
            }
            var rp = new Rect(Screen.width - S(470), S(16), S(454), S(186));
            GUILayout.BeginArea(rp, _panel);
            GUILayout.Label(sb.ToString(), _small);
            GUILayout.EndArea();

            // 准星（俯视模式用鼠标指针，不画准星）
            if (P != null && P.TopView)
            {
                float tw = S(980);
                var tr = new Rect((Screen.width - tw) / 2, Screen.height - S(168), tw, S(44));
                GUI.Box(tr, GUIContent.none, _panel);
                GUI.Label(tr, P.Held != null
                    ? "<b>俯视装袋</b> · 移动鼠标摆放 · 松开=轻放到底 · Shift+松开=扔下 · 滚轮 抬高/放低 · Q/E R/F C Z 旋转"
                    : "<b>俯视装袋</b> · 左键拿 · 右键按压 · WASD 平移 · 滚轮 缩放 · 1-4 袋位 · 5 购物车 · V/Esc 返回", _center);
            }
            else if (P != null && P.CursorLocked)
            {
                bool hot = P.LookItem != null || P.LookRack != null || P.LookBell || P.Held != null;
                float cs = S(hot ? 10 : 6);
                GUI.color = hot ? Orange : new Color(1, 1, 1, 0.8f);
                GUI.DrawTexture(new Rect(Screen.width / 2f - cs / 2, Screen.height / 2f - cs / 2, cs, cs), _white);
                GUI.color = Color.white;
            }
            else
            {
                GUI.Label(new Rect(0, Screen.height / 2f + S(40), Screen.width, S(40)), "点击画面继续操作", _center);
            }

            DrawTooltip();

            // 提示
            if (G.Toast != null)
            {
                float tw = S(760);
                var tr = new Rect((Screen.width - tw) / 2, Screen.height * 0.66f, tw, S(52));
                GUI.Box(tr, GUIContent.none, _panel);
                GUI.Label(tr, G.Toast, _center);
            }

            if (G.ShowHelp)
            {
                var hr = new Rect(S(16), Screen.height - S(110), S(1020), S(96));
                GUILayout.BeginArea(hr, _panel);
                GUILayout.Label(ControlsText(), _small);
                GUILayout.EndArea();
            }

            if (G.ShowReceipt) DrawReceipt();
        }

        void DrawTooltip()
        {
            if (P == null) return;
            string text = null;
            var it = P.Held != null ? P.Held : P.LookItem;
            if (it != null)
            {
                var d = it.Def;
                var sb = new StringBuilder();
                sb.Append($"<b>{d.Name}</b>  <color=#AAAAAA>{d.CategoryName}</color>\n");
                sb.Append($"{U.Money(d.Price)} · {d.Mass:0.00} kg · <color=#F2C14E>{d.FragilityText}</color>");
                if (d.MaxSharp >= 0.3f) sb.Append("  <color=#FF8A65>尖锐!</color>");
                if (d.Bulky) sb.Append("  <color=#9EC9FF>大件→推车</color>");
                float maxLoad = it.Parts.Count > 0 ? it.Parts.Max(p => p.LoadRatio) : 0;
                if (it.IsDamaged) sb.Append("\n<color=#FF6B5B>已损坏</color>");
                else if (P.Held == null && G.Slots.Any(bg => bg != null && bg.Contains(it) && bg.AboveRim(it))) sb.Append("\n<color=#FFB347>质心高出袋口，不稳！</color>");
                else if (maxLoad > 0.3f) sb.Append($"\n<color={(maxLoad > 0.8f ? "#FF6B5B" : "#F2C14E")}>受压 {maxLoad * 100:0}%</color>");
                if (P.Held != null) sb.Append("\n<color=#AAAAAA>Q/E R/F 旋转 · C 侧放 · Z 回正 · 滚轮远近</color>");
                text = sb.ToString();
            }
            else if (P.LookBag != null)
            {
                var b = P.LookBag;
                var s = b.Compute();
                text = $"<b>{b.Spec.Name}</b>  承重 {b.Spec.CapacityKg} kg\n{s.Count} 件 · {s.Mass:0.0} kg · 重心偏移 {s.Imbalance * 100:0}%" +
                       (b.Torn ? "\n<color=#FF6B5B>已被刺破</color>" : "") + (s.Count == 0 ? "\n<color=#AAAAAA>按 X 收回</color>" : "");
            }
            else if (P.LookRack != null)
            {
                var s = P.LookRack.Spec;
                text = $"<b>{s.Name}</b>（点击取一个）\n内尺寸 {s.Inner.x * 100:0}×{s.Inner.y * 100:0}×{s.Inner.z * 100:0} cm · 承重 {s.CapacityKg} kg" +
                       (s.Material == BagMaterial.Plastic ? "\n<color=#FF8A65>怕尖锐物品</color>" : "\n<color=#AAAAAA>耐扎，但不能超重</color>");
            }
            else if (P.LookBell)
            {
                text = "<b>猫铃</b>：装好了就按一下，完成这一单 (Enter)";
            }
            if (text == null) return;
            float w = S(560);
            // 第一人称：准星右下；俯视：固定在右下角，不挡住正在摆放的位置
            var r = P.TopView
                ? new Rect(Screen.width - w - S(16), Screen.height - S(300), w, S(130))
                : new Rect(Screen.width / 2f + S(24), Screen.height / 2f + S(24), w, S(130));
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label(text, _small);
            GUILayout.EndArea();
        }

        void DrawReceipt()
        {
            var lines = G.Store.Receipt.FullText();
            float w = S(420);
            int count = G.Store.Receipt.Lines.Count;
            float h = Mathf.Min(Screen.height - S(380), S(60) + count * S(24));
            var r = new Rect(S(16), S(250), w, h);
            var st = new GUIStyle(_panel);
            st.normal.background = _bgLight;
            GUILayout.BeginArea(r, st);
            GUILayout.Label(lines, _mono);
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------ 结算
        void DrawSettlement()
        {
            var res = G.LastResult;
            if (res == null) return;
            float w = S(820), h = S(780);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label($"订单结算  {new string('★', res.Stars)}{new string('☆', 3 - res.Stars)}", _title);
            GUILayout.Space(S(6));
            var sb = new StringBuilder();
            sb.AppendLine($"订单总价 {U.Money(res.ItemsTotal)} × 提成 10% = 基础工钱 <b>{U.Money(res.BasePay)}</b>");
            sb.AppendLine();
            sb.AppendLine($"规格系数  ×{res.ReqFactor:0.00}   <color=#AAAAAA>{res.ReqNote}</color>");
            sb.AppendLine($"平衡系数  ×{res.BalanceFactor:0.00}");
            foreach (var b in res.Bags)
            {
                string c = b.Factor >= 1.15f ? "#7CFC9A" : b.Factor >= 0.9f ? "#F2C14E" : "#FF6B5B";
                sb.AppendLine($"    · {b.Spec.Name}  {b.ItemCount}件 {b.Mass:0.0}kg  偏心 {b.Imbalance * 100:0}%  <color={c}>×{b.Factor:0.00} {b.Note}</color>");
            }
            sb.AppendLine($"耐心系数  ×{res.PatienceFactor:0.00}");
            sb.AppendLine($"漏装系数  ×{res.MissingFactor:0.00}" + (res.Missing > 0 ? $"   <color=#FF6B5B>漏装 {res.Missing} 件: {string.Join("、", res.MissingNames.Take(4))}</color>" : ""));
            sb.AppendLine();
            sb.AppendLine($"总倍率 <b>×{res.Multiplier:0.00}</b>  →  工钱 <color=#7CFC9A><b>{U.Money(res.Pay)}</b></color>");
            if (res.DamagedNames.Count > 0)
                sb.AppendLine($"赔偿 <color=#FF6B5B><b>-{U.Money(res.DamagePenalty)}</b></color>   <color=#AAAAAA>{string.Join("、", res.DamagedNames.Take(5))}</color>");
            sb.AppendLine();
            string netCol = res.Net >= 0 ? "#7CFC9A" : "#FF6B5B";
            sb.AppendLine($"<size={Mathf.RoundToInt(S(32))}>本单实得 <color={netCol}><b>{U.Money(res.Net)}</b></color></size>");
            GUILayout.Label(sb.ToString(), _label);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            string next = G.CustomerIndex >= GameManager.CustomersPerDay ? "今日结算 ▶" : "下一位顾客 ▶";
            if (GUILayout.Button(next, _button, GUILayout.Width(S(280)), GUILayout.Height(S(58)))) G.NextCustomer();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawDaySummary()
        {
            float w = S(720), h = S(560);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label($"第 {G.Day} 天 打烊啦", _title);
            var sb = new StringBuilder();
            float total = 0;
            int stars = 0;
            for (int i = 0; i < G.DayResults.Count; i++)
            {
                var res = G.DayResults[i];
                total += res.Net;
                stars += res.Stars;
                sb.AppendLine($"顾客 {i + 1}   {new string('★', res.Stars)}{new string('☆', 3 - res.Stars)}   {U.Money(res.Net)}" +
                              (res.DamagedNames.Count > 0 ? $"   <color=#FF6B5B>损坏 {res.DamagedNames.Count}</color>" : ""));
            }
            sb.AppendLine();
            sb.AppendLine($"今日收入 <b>{U.Money(total)}</b>   星星 {stars}/{G.DayResults.Count * 3}");
            sb.AppendLine($"钱包 <color=#7CFC9A><b>{U.Money(G.Money)}</b></color>");
            string rank = stars >= G.DayResults.Count * 3 - 1 ? "金牌装袋猫！" : stars >= G.DayResults.Count * 2 ? "合格的装袋猫" : "店长在看着你喵…";
            sb.AppendLine($"\n评价：<color=#F2C14E><b>{rank}</b></color>");
            GUILayout.Label(sb.ToString(), _label);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("开始下一天 ▶", _button, GUILayout.Width(S(280)), GUILayout.Height(S(58)))) G.EndDayAndContinue();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawVolume()
        {
            var bgm = BgmPlayer.I;
            if (bgm == null) return;
            string s = bgm.Muted ? "♪ 静音 (M)" : $"♪ {Mathf.RoundToInt(bgm.Volume * 100)}%";
            GUI.Label(new Rect(Screen.width - S(150), Screen.height - S(36), S(140), S(30)), s, _small);
        }
    }
}
