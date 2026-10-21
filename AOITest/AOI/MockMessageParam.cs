using System;
using System.Collections.Generic;

namespace AOITest.AOI
{
    /// <summary>
    /// MessageHandle(object param) 的模拟入参。
    ///
    /// 为什么这么写：
    /// 插件里的取值方式是 param.GetObjectProperty("Type") / ("LaneNo") / ("Msg") / ("ParamValue")，
    /// 而 GetObjectProperty 是 EasyTool.SDK 里的扩展方法，可能走「反射公有属性」也可能走「字典键」，
    /// 所以这里同时实现两种访问方式，保证不管 SDK 内部怎么实现都能取到值：
    ///   1) 反射取属性：MockMessageParam.Type / .LaneNo / .Msg / .ParamValue
    ///   2) 按字典取值：param["Type"] / param["ParamValue"]
    ///
    /// 另外插件里有这么一句：
    ///   Dictionary&lt;string, object&gt; dd = param.GetObjectProperty("ParamValue") as Dictionary&lt;string, object&gt;;
    /// 因此 ParamValue 必须真的存一个 Dictionary&lt;string, object&gt;，否则 as 强转会得到 null，
    /// 插件内部会空引用，窗体也就弹不出来。
    ///
    /// 等价于插件源码 SpecialCaseRun.MessageData（MessageHandleTool.cs 里那个类），
    /// 只是多带了一层字典能力。
    /// </summary>
    public class MockMessageParam : Dictionary<string, object>
    {
        public MockMessageParam()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        private T Get<T>(string key, T defaultValue = default(T))
        {
            object value;
            if (!TryGetValue(key, out value) || value == null)
                return defaultValue;

            if (value is T)
                return (T)value;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>消息类型，例如 MesCustomerButton / SwitchJob / MesChange / IsStopCheck / Login</summary>
        public string Type
        {
            get { return Get<string>("Type"); }
            set { this["Type"] = value; }
        }

        /// <summary>轨道号，0 = 1轨，1 = 2轨</summary>
        public string LaneNo
        {
            get { return Get<string>("LaneNo"); }
            set { this["LaneNo"] = value; }
        }

        /// <summary>附加消息（SwitchJob 用来回传异常信息）</summary>
        public string Msg
        {
            get { return Get<string>("Msg"); }
            set { this["Msg"] = value; }
        }

        /// <summary>业务参数包，插件会 as Dictionary&lt;string, object&gt; 后按 key 取值</summary>
        public object ParamValue
        {
            get { return Get<object>("ParamValue"); }
            set { this["ParamValue"] = value; }
        }
    }
}
