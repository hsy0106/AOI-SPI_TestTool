using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOITest.IL
{
    public class IlPcbResult
    {
        public string Key { get; set; }
        public int Type { get; set; }
        public PcbInfo PCB { get; set; }
    }

    public class PcbInfo
    {
        public long JobID { get; set; }
        public string JobName { get; set; }
        public DateTime JobModfiyDate { get; set; }
        public DateTime PCBStartTime { get; set; }
        public DateTime PCBEndTime { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double SizeX { get; set; }
        public double SizeY { get; set; }
        public string PCBBarcode { get; set; }
        public string CarrierBarcode { get; set; }
        public List<ArrayInfo> ArrayList { get; set; }
        public string LineNo { get; set; }
        public int LaneNo { get; set; }
        public int Res { get; set; }
        public string Side { get; set; }

        public static explicit operator SinicTekAOI.SDK.BoardPCB(PcbInfo v)
        {
            if (v == null) return null;

            var pcb = new SinicTekAOI.SDK.BoardPCB
            {
                BarCode = v.PCBBarcode,
                CarrierBarcode = v.CarrierBarcode,
                JobID = (int)v.JobID,
                JobName = v.JobName,
                LaneNo = v.LaneNo,
                LineNo = v.LineNo,
                StartTime = v.PCBStartTime,
                EndTime = v.PCBEndTime,
                Length = v.SizeX,
                Width = v.SizeY,
                Side = string.IsNullOrWhiteSpace(v.Side) ? "A" : v.Side,
                Result = (SinicTekAOI.SDK.JudgeResult)v.Res,
                Operator = "admin",
                StationID = SinicTekAOI.SDK.StationType.Default,
                ArrayPCBs = new List<SinicTekAOI.SDK.ArrayPCB>()
            };

            foreach (var a in v.ArrayList ?? new List<ArrayInfo>())
            {
                pcb.ArrayPCBs.Add(new SinicTekAOI.SDK.ArrayPCB
                {
                    ID = a.ArrayIndex,
                    ArrayIndex = a.ArrayIndex,
                    ArrayName = a.ArrayName,
                    BarCode = a.ArrayBarcode,
                    Result = (SinicTekAOI.SDK.JudgeResult)a.Res,
                    Components = new List<SinicTekAOI.SDK.Component>()
                });
            }
            return pcb;
        }
    }

    public class ArrayInfo
    {
        public int ArrayIndex { get; set; }
        public string ArrayName { get; set; }
        public string ArrayBarcode { get; set; }
        public int ArrayGroupIndex { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double SizeX { get; set; }
        public double SizeY { get; set; }
        public int Type { get; set; }
        public int Res { get; set; }
        public List<ResItem> ResList { get; set; }
        public List<int> FovIndexList { get; set; }
        public List<ComponentInfo> ComponentList { get; set; }
    }

    public class ResItem
    {
        public int res { get; set; }
        public string time { get; set; }      
        public int op { get; set; }
        public string userid { get; set; }    // 可能是 null
    }

    public class ComponentInfo
    {
        public int ComponentIndex { get; set; }
        public List<int> FovIndexList { get; set; }
        public ComponentImage Image { get; set; }
        public List<object> PinList { get; set; }
        public List<WindowInfo> WindowList { get; set; }
    }

    public class ComponentImage
    {
        public List<ImgItem> ImgList { get; set; }
        public double Wpx { get; set; }
        public double Hpx { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Wmm { get; set; }
        public double Hmm { get; set; }
        public double Angle { get; set; }
        public double ZM { get; set; }
        public double ZM3D { get; set; }
        public int Light { get; set; }
    }

    public class ImgItem
    {
        public string FImg { get; set; }
        public string UImg { get; set; }
        public string P3D { get; set; }
        public string U3D { get; set; }
        public int Light { get; set; }
        public double Angle { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Wmm { get; set; }
        public double Hmm { get; set; }
    }

    public class WindowInfo
    {
        public int WindowIndex { get; set; }
        public int PinIndex { get; set; }
        public double Angle { get; set; }
        public double CenterXByPCB { get; set; }
        public double CenterYByPCB { get; set; }
        public int Res { get; set; }
        public List<ResItem> ResList { get; set; }
        public int Defect { get; set; }
        public string DefectShowName { get; set; }
        public int Algorithm { get; set; }
        public AlgDefectValue AlgDefectValue { get; set; }
        public List<string> DetectionList { get; set; }   // "ALG_SPEC_Similarity$60.00$0.00$1$ENM..."
        public int Light { get; set; }
        public int WindowType { get; set; }
        public List<OtherDefectValue> OtherDefectValue { get; set; }
        public int AlgFuncTypeId { get; set; }
        public double CenterXByJob { get; set; }
        public double CenterYByJob { get; set; }
        public Pos CrrPos { get; set; }
        public Pos OffsetPos { get; set; }
        public DateTime SInsTime { get; set; }
        public DateTime EInsTime { get; set; }
    }

    public class AlgDefectValue
    {
        public int ComponentIndex { get; set; }
        public int WindowIndex { get; set; }
        public List<ValueItem> ValueList { get; set; }
        public int Algorithm { get; set; }
        public int Defect { get; set; }
        public int AlgFuncTypeId { get; set; }
    }

    public class ValueItem
    {
        public int AlgDefectSpecIndex { get; set; }
        public int BaseAlgResIndex { get; set; }
        public double Value { get; set; }
        public object PerValue { get; set; }   
        public object ShowValue { get; set; }
        public int ValueType { get; set; }
        public int Res { get; set; }
    }

    public class OtherDefectValue
    {
        public List<string> DefectSpecList { get; set; }          // ["37","20.00","0.00"]
        public List<int> DefectSpecValueTypeList { get; set; }
        public int enmJudgeRes { get; set; }
    }

    public class Pos { public double X { get; set; } public double Y { get; set; } }
}
