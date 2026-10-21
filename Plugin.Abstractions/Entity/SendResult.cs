namespace Plugin.Abstractions.Entity
{

    public class SendResult
    {
        /// <summary>
        /// 是否成功发送/收到应答
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果说明：成功时的应答摘要，或失败时的错误原因
        /// </summary>
        public string Message { get; set; }
    }
}
