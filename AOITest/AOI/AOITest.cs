using SinicTekAOI.SDK;
using SinicTek.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickMiddle.SDK;
using Plugin_Tool;
using System.Windows.Forms;
using System.IO;
using AOITest.Enum;

namespace AOITest.AOI
{
    public class AOITestEntity
    {


        public BoardPCB board;
        public BoardType boarType;
        public AppSetting appSetting;
        public List<ArrayPCB> ArrayPCBs;
        public BarCodeTree barCodeTree;
        public List<ArrayGroup> ArrayGroups;

        public string dll;
        public bool AOIMethod(string method, object[] paramter)
        {
            bool isSuccess = false;

            //string dll = Path.Combine(Application.StartupPath, "MesCustom", "Bin", "CustomDLL", "你的插件.dll");

            try
            {
                object result = DllTool.RunMethod(method, dll, paramter);

                // 方法返回 bool 就以它为准；返回 void 就算执行成功
                if (result is bool b)
                    isSuccess = b;
                else
                    isSuccess = true;
            }
            catch (Exception ex)
            {
                isSuccess = false;
                Console.WriteLine("执行失败：" + ex.Message);
            }

            return isSuccess;
        }

    }
}
