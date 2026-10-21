namespace Plugin.Abstractions.Entity
{

    public enum PluginState
    {
        /// <summary>初始状态</summary>
        None = 0,

        /// <summary>已初始化完成，待启动</summary>
        Ready,

        /// <summary>正在启动</summary>
        Starting,

        /// <summary>运行中 </summary>
        Running,

        /// <summary>正在停止 </summary>
        Stopping,

        /// <summary>失败 </summary>
        Faulted
    }
}
