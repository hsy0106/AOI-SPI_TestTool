using AOITest.AOI;
using AOITest.Enum;
using AOITest.IL;
using App.Server;
using Newtonsoft.Json;
using QuickMiddle.SDK;
using SinicTekAOI.SDK;
using System;
using System.Collections.Generic;

using System.IO;
using System.Windows.Forms;


namespace AOITest
{
    public partial class Form1 : Form
    {
        public static string customerDllPath = "MesCustom/Bin/CustomDLL/";
        public string AOICustomeDllPath = App.IniConfigServer.GetValue("Server", "AOIDllPath");
        public string SPICustomeDllPath = App.IniConfigServer.GetValue("Server", "SPIDllPath");
        public static string ILJsonFile = Path.Combine(Application.StartupPath, "IL");
        public string SelectDll = string.Empty;
        public AOITestEntity _AOITest = null;
        
        private XmlConfigServer _XmlConfig;
        private string _modelType;
        private InjectResult _injectResult;
        public Form1()
        {
            InitializeComponent();
        }
        static List<string> Director(string dir)
        {

            DirectoryInfo d = new DirectoryInfo(dir);
            List<string> dllNames = new List<string>();
            FileSystemInfo[] fsinfos = d.GetFileSystemInfos();

            foreach (FileSystemInfo fsinfo in fsinfos)
            {
                if (fsinfo is DirectoryInfo)
                {
                    Director(fsinfo.FullName);
                }
                else
                {
                    Console.WriteLine(fsinfo.FullName); // 完整路径
                    dllNames.Add(fsinfo.Name);
                }
            }

            return dllNames;
        }
        public void LoadMesCustomDll(string root)
        {

            //MesCustom/Bin/CustomDLL/
            //string root = Path.Combine(Application.StartupPath, "MesCustom", "Bin", "CustomDLL");
            comboBox1.Items.Clear();
            if (!Directory.Exists(root))
            {
                MessageBox.Show("自定义目录不存在：" + root);
                return;
            }

            foreach (string dllPath in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
            {
                // dll 名第一段必须等于所在文件夹名
                string folderName = new DirectoryInfo(Path.GetDirectoryName(dllPath)).Name;
                string firstPart = Path.GetFileNameWithoutExtension(dllPath).Split('.')[0];
                if (!firstPart.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // 直接用 dll 名称作为显示名
                string name = Path.GetFileNameWithoutExtension(dllPath);
                comboBox1.Items.Add(name);
            }

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;
            else
                MessageBox.Show("未扫描到可用的客户 DLL");

        }
        private string TargetClassName
        {
            get { return _modelType == "SPI" ? "SpecialCaseRun.SPI" : "SpecialCaseRun.AOI"; }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadModel();
            //LoadMesCustomDll(AOICustomeDllPath);
            LoadXmlConfig();
            LoadConfigToGrid();
           
        }
       public void LoadModel()
        {
            comboBox2.Items.Clear();
            foreach (ModelType type in ModelType.GetValues(typeof(ModelType)))
            {
                comboBox2.Items.Add(type.ToString());
            }

            // 优先取 config.ini 的 ModelTpye（注意 ini 里这个键本身拼错成 ModelTpye）
            string iniType = App.IniConfigServer.GetValue("Server", "ModelTpye");
            int idx = comboBox2.Items.IndexOf(iniType);
            comboBox2.SelectedIndex = idx >= 0 ? idx : 0;  
        }
        public void LoadXmlConfig()
        {
            _XmlConfig = XmlConfigServer.LoadFromIni();
            textBox1.Text = _XmlConfig?.GetValue("MachineName", "");
        }
        /// <summary>
        /// 读取默认 XML 配置（config.ini 的 [Server] ConfigPath 指向的文件），填充到 dataGridView1。
        /// 列：参数代码 / 参数名称 / 可编辑 / 参数值。
        /// </summary>
        private void LoadConfigToGrid()
        {
            // 仅首次创建列，避免重复添加
            if (dataGridView1.Columns.Count == 0)
            {
                dataGridView1.Columns.Add("colCode", "参数代码");
                dataGridView1.Columns.Add("colName", "参数名称");
                dataGridView1.Columns.Add("colEdit", "可编辑");
                dataGridView1.Columns.Add("colValue", "参数值");
                dataGridView1.Columns["colValue"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dataGridView1.ReadOnly = true;
                dataGridView1.AllowUserToAddRows = false;
                dataGridView1.RowHeadersVisible = false;
            }

            try
            {
                XmlConfigServer config = XmlConfigServer.LoadFromIni();
                dataGridView1.Rows.Clear();
                foreach (var entry in config.Entries)
                {
                    dataGridView1.Rows.Add(entry.Code, entry.Name, entry.IsUserEdit, entry.Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载配置到表格失败：" + ex.Message);
            }
        }

        private void btnCreateConfig_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("请先在上方选择可用的客户 DLL。\n当前：" + SelectDll);
                return;
            }

            try
            {
                // 注入源就是界面上表格显示的那份 XML（config.ini 的 [Server] ConfigPath）
                XmlConfigServer config = XmlConfigServer.LoadFromIni();
                InjectResult r = ConfigInjector.Inject(config, SelectDll);
                _injectResult = r;
                AppendLog(r.Detail());

                // 未识别项不拦：只是 XML 里有、插件参数表里没有，跳过即可
                if (r.Success)
                {
                    string tip = r.HasUnknown
                        ? "\r\n\r\n有 " + r.Unknown.Count + " 项未识别（XML 里有、插件参数表里没有），已跳过，不影响后续调用。"
                        : "";

                    MessageBox.Show(r.Message + "\r\n\r\n注入成功，之后插件用的就是表格里的值。" + tip,
                        "注入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(r.Detail(), "注入完成，但回读校验未通过",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog("注入失败：" + ex);
                MessageBox.Show("注入失败：" + ex.Message, "注入失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// 快捷打开当前目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenCurrentFile_Click(object sender, EventArgs e)
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
        }
        #region AOIMethod
        /// <summary>
        /// LoginCheck
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoginCheck_Click(object sender, EventArgs e)
        {
            var method = AOIMethod.LoginCheck.ToString();
            IlPcbResult result = IlJsonLoader.Load();
            string laneNo = result.PCB.LineNo;
            string JobName = result.PCB.JobName;


        }
        /// <summary>
        /// BarCodeCheck
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBarCodeCheck_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("当前选中的 DLL 不存在或路径无效：\n" + SelectDll);
                return;
            }

            // 先把 XML 的值注入插件，保证插件 InParams 用的就是表格里的值。
            // 未识别项（Unknown）只记日志、继续执行；只有回读校验失败（VerifyFailed）才中止。
            try
            {
                
                AppendLog(_injectResult.Message);

                if (_injectResult.HasUnknown)
                    AppendLog(_injectResult.Detail());      // 有未识别项就把明细打出来，但不 return

                if (!_injectResult.Success)
                {
                    AppendLog("[注入] 回读校验未通过，已中止调用：" + _injectResult.Detail());
                    return;
                }
            }
            catch (Exception ex)
            {
                AppendLog("注入配置失败，已中止调用：" + ex.Message);
                return;
            }

            var method = AOIMethod.BarCodeCheck.ToString();
            IlPcbResult result = IlJsonLoader.Load();
            string barcode = result.PCB?.PCBBarcode;
            try
            {
                BarCodeTree barCodeTree = new BarCodeTree
                {
                    StartTime = result.PCB.PCBStartTime,
                    LaneNo = 1,
                    JobName = result.PCB.JobName,
                    BarCode = result.PCB.PCBBarcode,
                    ExtendBarcode = "",
                    ArrayBarCode = new ArrayBarCode[] { },
                    JobNum = "1",
                    IsMorePart = true,
                    BoardBarCode = new string[] { }

                };
                var res = DllMethodInvoker.Invoke(SelectDll, "SpecialCaseRun.AOI", method, new object[] { barCodeTree });
                var resjson = JsonConvert.SerializeObject(res);
                // LogServer.Info($"-------------{methodInfo.Name}方法已找到  {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}--------------");
                AppendLog(resjson);
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex}");
            }

        }
        /// <summary>
        /// MesOutPut
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MesOutPut_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("当前选中的 DLL 不存在或路径无效：\n" + SelectDll);
                return;
            }

            var barCode = this.strBarCode.Text.Trim();
            if (!string.IsNullOrEmpty(barCode))
            {
                // 紧贴调用前注入，避免插件内部 RefreshConfig() 把之前注入的值冲掉
                try
                {
                    
                    AppendLog(_injectResult.Message);

                    if (_injectResult.HasUnknown)
                        AppendLog(_injectResult.Detail());

                    if (!_injectResult.Success)
                    {
                        AppendLog("[注入] 回读校验未通过，已中止调用：" + _injectResult.Detail());
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("注入配置失败，已中止调用：" + ex.Message);
                    return;
                }

                try
                {
                    IlPcbResult result = IlJsonLoader.Load();
                    //var method = AOIMethod.
                    var res = DllMethodInvoker.Invoke(SelectDll, "SpecialCaseRun.AOI", AOIMethod.MesOutPut.ToString(),new object[] { (BoardPCB)result.PCB });
                    var resJson = JsonConvert.SerializeObject(res);
                    AppendLog(resJson);
                }
                catch (Exception ex)
                {
                    AppendLog($"Error: {ex}");
                }
            }
        }


        private void btnGetBarCodeResult_Click(object sender, EventArgs e)
        {
            
        }
        private void btnSendDataForDefaultMES_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("当前选中的 DLL 不存在或路径无效：\n" + SelectDll);
                return;
            }

            try
            {
                
                AppendLog(_injectResult.Message);

                if (_injectResult.HasUnknown)
                    AppendLog(_injectResult.Detail());

                if (!_injectResult.Success)
                {
                    AppendLog("[注入] 回读校验未通过，已中止调用：" + _injectResult.Detail());
                    return;
                }
            }
            catch (Exception ex)
            {
                AppendLog("注入配置失败，已中止调用：" + ex.Message);
                return;
            }

            try
            {
                IlPcbResult result = IlJsonLoader.Load();

                var res = DllMethodInvoker.Invoke(SelectDll, "SpecialCaseRun.AOI", AOIMethod.SendDataForDefaultMES.ToString(), new object[] { (BoardPCB)result.PCB });
                var resJson = JsonConvert.SerializeObject(res);
                AppendLog(resJson);
            }
            catch(Exception ex)
            {
                AppendLog(ex.ToString());
            }
            
        }

        private void btnGetBadMark_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("当前选中的 DLL 不存在或路径无效：\n" + SelectDll);
                return;
            }

            // 先把 XML 的值注入插件，保证插件 InParams 用的就是表格里的值。
            // 未识别项（Unknown）只记日志、继续执行；只有回读校验失败（VerifyFailed）才中止。
            try
            {
               
                AppendLog(_injectResult.Message);

                if (_injectResult.HasUnknown)
                    AppendLog(_injectResult.Detail());      // 有未识别项就把明细打出来，但不 return

                if (!_injectResult.Success)
                {
                    AppendLog("[注入] 回读校验未通过，已中止调用：" + _injectResult.Detail());
                    return;
                }
            }
            catch (Exception ex)
            {
                AppendLog("注入配置失败，已中止调用：" + ex.Message);
                return;
            }

            var method = AOIMethod.GetBadMark.ToString();
            IlPcbResult result = IlJsonLoader.Load();
            string barcode = result.PCB?.PCBBarcode;
            try
            {
                BarCodeTree barCodeTree = new BarCodeTree
                {
                    StartTime = result.PCB.PCBStartTime,
                    LaneNo = 1,
                    JobName = result.PCB.JobName,
                    BarCode = result.PCB.PCBBarcode,
                    ExtendBarcode = "",
                    ArrayBarCode = new ArrayBarCode[] { },
                    JobNum = "1",
                    IsMorePart = true,
                    BoardBarCode = new string[] { }

                };
                var res = DllMethodInvoker.Invoke(SelectDll, "SpecialCaseRun.AOI", method, new object[] { barCodeTree });
                var resjson = JsonConvert.SerializeObject(res);
                // LogServer.Info($"-------------{methodInfo.Name}方法已找到  {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}--------------");
                AppendLog(resjson);
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex}");
            }


        }
        #endregion AOIMethod


        #region SPIMethod
        /// <summary>
        /// MessageHandle
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnMessageHandle_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectDll) || !File.Exists(SelectDll))
            {
                MessageBox.Show("当前选中的 DLL 不存在或路径无效：\n" + SelectDll);
                return;
            }

            try
            {
                object param = BuildMockMessageParam();

                AppendLog($"[MessageHandle] DLL = {SelectDll}");
                AppendLog($"[MessageHandle] 模拟入参 = {JsonConvert.SerializeObject(param)}");

                var res = DllMethodInvoker.Invoke(SelectDll, "SpecialCaseRun.SPI", SPIMethod.MessageHandle.ToString(), new object[] { param });

                AppendLog($"[MessageHandle] 调用完成，返回值 = {(res == null ? "null（void 方法）" : res.ToString())}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex}");

                if (ex is FileNotFoundException || ex is FileLoadException || ex is TypeLoadException)
                {
                    AppendLog("[MessageHandle] 提示：插件目录缺依赖。把客户工程 bin\\Debug 下的 " +
                              "Domain.MES.dll / EasyTool.SDK.dll / DataBean.DLL / Aspose.Cells.DLL " +
                              "复制到 " + Path.GetDirectoryName(SelectDll));
                }

                MessageBox.Show("MessageHandle 调用失败：" + ex.Message +
                                "\n\n完整异常已写入下方日志区。", "调用失败",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion spimethod


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //string root = Path.Combine(Application.StartupPath, "MesCustom", "Bin", "CustomDLL");
            string root = string.Empty;
            if (_modelType == "AOI")
            {
                root = App.IniConfigServer.GetValue("Server", "AOIDllPath");
            }
            else
            {
                root = App.IniConfigServer.GetValue("Server", "SPIDllPath");
            }

            object val = comboBox1.SelectedValue ?? comboBox1.SelectedItem;
            string dllFile = val?.ToString();

            if (string.IsNullOrWhiteSpace(dllFile))
            {
                MessageBox.Show("请先选择 DLL");
                return;
            }

            string firstSeg = Path.GetFileNameWithoutExtension(dllFile).Split('.')[0];

            string fileName = dllFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? dllFile : dllFile + ".dll";
            SelectDll = Path.Combine(root, firstSeg, fileName);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
           object val = comboBox2.SelectedValue ?? comboBox2.SelectedItem;
            _modelType = val?.ToString();
            if(_modelType == "AOI")
            {
                LoadMesCustomDll(AOICustomeDllPath);
            }
            else
            {
                LoadMesCustomDll(SPICustomeDllPath);
            }
        }

        #region 日志显示
        public void AppendLog(string str)
        {
            if (richTextBox1 == null) return;
            LogServer.Info(str);
            richTextBox1.AppendText(string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, str));
        }


        #endregion 日志显示

        /// <summary>
        /// 构造参数
        /// </summary>
        /// <returns></returns>
        private object BuildMockMessageParam()
        {
            const string msgType = "MesCustomerButton";

            string workNo1 = _XmlConfig.GetValue("WorkNo1");
            string workNo2 = _XmlConfig.GetValue("WorkNo2");
            string operatorName = _XmlConfig.GetValue("Operator");
            string lineName1 = _XmlConfig.GetValue("LineName1");
            string lineName2 = _XmlConfig.GetValue("LineName2");

            // 工单号 xml 里没配，就退回 IL 目录里的 JobName，再不行用占位值
            if (string.IsNullOrWhiteSpace(workNo1))
                workNo1 = GetJobNameOr("WO-TEST-0001");
            if (string.IsNullOrWhiteSpace(workNo2))
                workNo2 = workNo1;
            if (string.IsNullOrWhiteSpace(operatorName))
                operatorName = "admin";
            if (string.IsNullOrWhiteSpace(lineName1))
                lineName1 = "LINE-1";
            if (string.IsNullOrWhiteSpace(lineName2))
                lineName2 = "LINE-2";

            // 插件用 as Dictionary<string, object> 取这个包，所以必须真的是 Dictionary<string, object>
            var paramValue = new Dictionary<string, object>
            {
                { "WorkNo1",   workNo1 },       // 工单号 1
                { "WorkNo2",   workNo2 },       // 工单号 2
                { "Operator",  operatorName },  // 操作员
                { "LineName1", lineName1 },     // 线体名 1
                { "LineName2", lineName2 },     // 线体名 2
            };

            return new MockMessageParam
            {
                Type = msgType,
                LaneNo = "0",                   // 0 = 1轨
                Msg = string.Empty,
                ParamValue = paramValue
            };
        }

       

        /// <summary>取 IL 目录里 json 的 JobName，取不到返回默认值。</summary>
        private static string GetJobNameOr(string defaultValue)
        {
            try
            {
                IlPcbResult il = IlJsonLoader.Load();
                if (il != null && il.PCB != null && !string.IsNullOrWhiteSpace(il.PCB.JobName))
                    return il.PCB.JobName;
            }
            catch
            {
                // 忽略，用兜底值
            }
            return defaultValue;
        }
        public class MockMessageParam 
        { 
            public  string Type { get; set; }
            public string LaneNo { get; set; }
            public string Msg { get; set; }
            public Dictionary<string,object> ParamValue { get; set; }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }
    }
}
