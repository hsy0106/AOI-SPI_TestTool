using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOITest.Enum
{
    public enum AOIMethod
    {
        /// <summary>
        /// 登录检测
        /// </summary>
        LoginCheck,
        /// <summary>
        /// 条码检测
        /// </summary>
        BarCodeCheck,
        /// <summary>
        ///  获取BadMark
        /// </summary>
        GetBadMark,
        /// <summary>
        /// 发送设备状态
        /// </summary>
        UpdateDeviceStatus,
        /// <summary>
        /// 导出数据
        /// </summary>
        MesOutPut,
        /// <summary>
        /// 设备状态
        /// </summary>
        UISystemState,
        /// <summary>
        /// 心跳
        /// </summary>
        RunEquipStatusThread,
        /// <summary>
        /// 过站
        /// </summary>
        SendDataForDefaultMES

    }
}
