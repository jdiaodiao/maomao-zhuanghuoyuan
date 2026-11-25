using System.Collections;
using UnityEngine;

namespace CatMart
{
    /// <summary>场景搭好后渲染一次反射探针（之后每 20 秒刷新一次，环境基本不变）。</summary>
    public class ProbeRefresher : MonoBehaviour
    {
        IEnumerator Start()
        {
            var probe = GetComponent<ReflectionProbe>();
            yield return null;
            yield return null;
            while (true)
            {
                probe.RenderProbe();
                yield return new WaitForSeconds(20f);
            }
        }
    }
}
