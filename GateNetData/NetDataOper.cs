using System;
using System.Text;
using System.IO;
using System.Data.SQLite;
using System.Data;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace GateNetData
{
    public class NetDataOper
    {
        #region 错误代码声明

        private const int SUC = 0;// 成功
        private const int ERR_NOINIT = 1;// 没有初始化类
        private const int ERR_DATAINVALID = 2;// 传入参数无效
        private const int ERR_NODBFILE = 3;// 数据文件不存在
        private const int ERR_DBFAIL = 4;// 操作数据失败

        #endregion

        #region 变量

        /// <summary>
        /// 网络数据存储库名称
        /// </summary>
        private string m_DbFilePath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "db\\NetDataInfo.db";

        /// <summary>
        /// 当前通讯流水号
        /// </summary>
        private int m_NowSeqId = 0;

        #endregion

        #region 属性

        private int m_SendDataMaxNum = 3;
        /// <summary>
        /// 发送失败允许最大重发次数
        /// </summary>
        public int SendDataMaxNum
        {
            get { return m_SendDataMaxNum; }
            set { m_SendDataMaxNum = value; }
        }

        #endregion

        #region 业务公共函数接口

        #region 连接包和心跳包封装

        /// <summary>
        /// 封装连接包
        /// </summary>
        /// <param name="vmId"></param>
        /// <param name="vmPwd"></param>
        /// <returns></returns>
        public byte[] PackConnectData(string vmId,string vmPwd)
        {
            byte[] bytConnectData = Encoding.UTF8.GetBytes("*" + vmId + "*" + "000000" + "*" + vmPwd);
            string strConnectData = ByteArrayToHexString(bytConnectData, bytConnectData.Length);

            return PackNetDataToSend("1F", "30",GetSeqId(), strConnectData);
        }

        /// <summary>
        /// 封装心跳包
        /// </summary>
        /// <returns></returns>
        public byte[] PackHeatData()
        {
            return PackNetDataToSend("FF", "",GetSeqId(), "");
        }

        #endregion

        #region 连接初始化后的相关数据汇报

        /// <summary>
        /// 连接初始化后汇报货道编号
        /// </summary>
        /// <param name="totalNum">货道总数量</param>
        /// <param name="paCodeInfo">货道编号组合，如：A1A2</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int InitAsileList(int totalNum, string paCodeInfo)
        {
            int intErrCode = SUC;

            int paCodeLen = 0;

            byte[] paCodeByte = Encoding.UTF8.GetBytes(paCodeInfo);
            paCodeLen = paCodeInfo.Length;

            byte[] packDataByte = new byte[paCodeLen + 3];

            packDataByte[0] = 0x2A;
            packDataByte[1] = 0x32;
            packDataByte[2] = 0x2A;

            for (int i = 0; i < paCodeLen; i++)
            {
                packDataByte[3 + i] = paCodeByte[i];
            }

            // 提交发送
            ////intErrCode = AddNetData("1F", "31", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 连接初始化后汇报货道状态
        /// </summary>
        /// <param name="totalNum">货道总数量</param>
        /// <param name="paStatusInfo">货道状态组合</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int InitAsileStatus(int totalNum, string paStatusInfo)
        {
            int intErrCode = SUC;

            int paCodeLen = 0;

            byte[] paCodeByte = Encoding.UTF8.GetBytes(paStatusInfo);
            paCodeLen = paStatusInfo.Length;

            byte[] packDataByte = new byte[paCodeLen + 1];

            packDataByte[0] = 0x2A;
            //packDataByte[1] = 0x32;
            //packDataByte[2] = 0x2A;

            for (int i = 0; i < paCodeLen; i++)
            {
                packDataByte[1 + i] = paCodeByte[i];
            }

            // 提交发送
            intErrCode = AddNetData("1F", "32", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 连接初始化后汇报货道价格
        /// </summary>
        /// <param name="totalNum">货道总数量</param>
        /// <param name="paStatusInfo">货道价格组合</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int InitPaPrice(int totalNum, string paPriceInfo)
        {
            int intErrCode = SUC;

            byte[] paPriceByte = new byte[totalNum * 2];

            byte[] packDataByte = new byte[paPriceByte.Length + 5];

            packDataByte[0] = 0x2A;
            packDataByte[1] = 0x32;
            packDataByte[2] = 0x2A;
            packDataByte[3] = 0x32;
            packDataByte[4] = 0x2A;

            string[] hexPaPrice = paPriceInfo.Split(',');
            string strPaPrice = "";
            byte[] amountByte = new byte[2];// Encoding.UTF8.GetBytes(amount.ToString());

            if (hexPaPrice.Length >= totalNum)
            {
                for (int i = 0; i < hexPaPrice.Length; i++)
                {
                    strPaPrice = hexPaPrice[i];
                    if (strPaPrice.Length > 0)
                    {
                        amountByte[0] = 0x00;
                        amountByte[1] = 0x00;
                        amountByte = BitConverter.GetBytes(Convert.ToInt32(strPaPrice));
                        packDataByte[5 + i * 2] = amountByte[1];
                        packDataByte[6 + i * 2] = amountByte[0];
                    }
                }
            }

            // 提交发送
            intErrCode = AddNetData("1F", "33", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 连接初始化后汇报部件状态
        /// </summary>
        /// <param name="deviceStatus">部件状态列表，各值之间以*隔开
        /// 门状态、硬币器状态、纸币器状态、掉货检测状态、读卡器状态、
        /// 驱动板0状态、温度传感器状态、驱动板0的温度、
        /// 驱动板1的状态、驱动板1的探头状态、驱动板1的温度
        /// </param>
        /// <returns></returns>
        public int InitDeviceStatus(string deviceStatus)
        {
            int intErrCode = SUC;

            #region 解析各部件状态

            string[] hexDeviceStatus = deviceStatus.Split('*');

            if (hexDeviceStatus.Length > 6)
            {
                byte bytDoorStatus = ConvertStatus_Door(hexDeviceStatus[0]);// 门状态
                byte bytCoinStatus = ConvertStatus_Coin(hexDeviceStatus[1]);// 硬币状态
                byte bytCashStatus = ConvertStatus_Cash(hexDeviceStatus[2]);// 纸币状态
                byte bytDropStatus = ConvertStatus_DropCheck(hexDeviceStatus[3]);// 掉货检测状态
                byte bytCardStatus = ConvertStatus_Card(hexDeviceStatus[4]);// 读卡器状态
                byte bytMainBoardStatus = ConvertStatus_MainBoard(hexDeviceStatus[5]);// 驱动板0状态
                byte bytTmpStatus = ConvertStatus_TmpDevice(hexDeviceStatus[6]);// 温度传感器0状态

                byte bytTmpValue = ConvertTmpValue(bytTmpStatus,hexDeviceStatus[7]);// = Convert.ToByte(hexDeviceStatus[7]);// 驱动板0温度

                #region 组织发送包

                byte[] packDataByte = new byte[12];

                packDataByte[0] = 0x2A;

                packDataByte[1] = bytDoorStatus;

                packDataByte[2] = bytCoinStatus;

                packDataByte[3] = bytCashStatus;

                packDataByte[4] = bytDropStatus;

                packDataByte[5] = bytCardStatus;

                packDataByte[6] = bytMainBoardStatus;

                packDataByte[7] = bytTmpStatus;

                packDataByte[8] = bytTmpValue;

                packDataByte[9] = 0x01;
                packDataByte[10] = 0x01;
                packDataByte[11] = 0x01;

                intErrCode = AddNetData("1F", "34", ByteArrayToHexString(packDataByte, packDataByte.Length));

                #endregion

            }

            #endregion

            return intErrCode;
        }

        /// <summary>
        /// 初始化软件版本信息
        /// </summary>
        /// <param name="softVer">软件版本</param>
        /// <param name="phoneNum">手机号码</param>
        /// <returns></returns>
        public int InitSoftInfo(string softVer,string phoneNum)
        {
            int intErrCode = SUC;

            byte[] packDataByte = Encoding.UTF8.GetBytes("*" + softVer + "+" + phoneNum);

            // 提交发送
            intErrCode = AddNetData("1F", "35", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #endregion

        #region 机器运行及设备状态汇报

        /// <summary>
        /// 保存管理员操作日志信息
        /// </summary>
        /// <param name="userId">管理员帐号或卡号</param>
        /// <param name="operContent">操作内容</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int UpdateOperLog(string vmId,string userId, string operContent)
        {
            int intErrCode = SUC;

            string strOperDate = "";
            string strUserId = userId;

            strOperDate = DateTime.Now.ToString("yyMMddHHmm");

            // 提交发送
            intErrCode = AddNetData("14", "31", "*" + vmId + "*" + userId + "*" +
                operContent + "*" + "YMDHm" + "*");

            return intErrCode;
        }

        /// <summary>
        /// 保存终端参数信息
        /// </summary>
        /// <param name="paraCode">参数编号</param>
        /// <param name="newValue">参数新值</param>
        /// <param name="oldValue">参数旧值</param>
        /// <param name="operUser">设置修改人</param>
        /// <returns>结果 False：失败 True：成功</returns>
        public int UpdateParameter(string paraCode, string newValue, string oldValue, string operUser)
        {
            int intErrCode = SUC;

            #region 检查数据有效性

            if ((string.IsNullOrEmpty(paraCode)) ||
                (string.IsNullOrEmpty(newValue)) ||
                (string.IsNullOrEmpty(oldValue)))
            {
                return ERR_DATAINVALID;
            }

            if (string.IsNullOrEmpty(operUser))
            {
                operUser = "0";
            }

            #endregion

            // 参数编号
            byte[] paraCodeByte = Encoding.UTF8.GetBytes(paraCode);

            // 参数新值
            byte[] newValueByte = Encoding.UTF8.GetBytes(newValue);

            // 参数旧值
            byte[] oldValueByte = Encoding.UTF8.GetBytes(oldValue);

            // 操作人
            byte[] operUserByte = Encoding.UTF8.GetBytes(operUser);

            // 时间
            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();

            // 组织数据包
            byte[] packDataByte = new byte[paraCodeByte.Length + newValueByte.Length +
                oldValueByte.Length +
                operUserByte.Length + dateByte.Length + 5];

            packDataByte[0] = 0x2A;

            for (int i = 0; i < paraCodeByte.Length; i++)
            {
                packDataByte[1 + i] = paraCodeByte[i];
            }

            packDataByte[1 + paraCodeByte.Length] = 0x2A;

            for (int i = 0; i < newValueByte.Length; i++)
            {
                packDataByte[2 + paraCodeByte.Length + i] = newValueByte[i];
            }

            packDataByte[2 + paraCodeByte.Length + newValueByte.Length] = 0x2A;

            for (int i = 0; i < oldValueByte.Length; i++)
            {
                packDataByte[3 + paraCodeByte.Length + newValueByte.Length + i] = oldValueByte[i];
            }

            packDataByte[3 + paraCodeByte.Length + newValueByte.Length + oldValueByte.Length] = 0x2A;

            for (int i = 0; i < operUserByte.Length; i++)
            {
                packDataByte[4 + paraCodeByte.Length + newValueByte.Length + +oldValueByte.Length + i] = operUserByte[i];
            }

            packDataByte[4 + paraCodeByte.Length + newValueByte.Length + oldValueByte.Length + operUserByte.Length] = 0x2A;

            // 时间转换
            for (int i = 0; i < 5; i++)
            {
                packDataByte[5 + paraCodeByte.Length + newValueByte.Length +
                    oldValueByte.Length + operUserByte.Length + i] = dateByte[i];
            }

            intErrCode = AddNetData("11", "40", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 保存机器温度状态
        /// </summary>
        /// <param name="tempNo">温区编号</param>
        /// <param name="status">温度状态 00：正常 01：温度值超范围 02：线路不通或未接温度传感器</param>
        /// <param name="tempValue">温度值</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int UpdateTempStatus(string tempNo, string status, string tempValue)
        {
            int intErrCode = SUC;

            // 提交发送
            byte bytTempNo;
            byte bytTempValue;
            byte bytStatus;

            // 温区编号
            switch (tempNo)
            {
                case "1":
                    bytTempNo = 0x30;
                    break;

                default:
                    bytTempNo = 0x31;
                    break;
            }

            // 温度状态
            bytStatus = ConvertStatus_TmpDevice(status);

            // 温度值
            bytTempValue = ConvertTmpValue(bytStatus, tempValue);

            byte[] packDataByte = new byte[12];

            packDataByte[0] = 0x2A;

            packDataByte[1] = bytTempNo;

            packDataByte[2] = 0x2A;

            packDataByte[3] = bytStatus;

            packDataByte[4] = 0x2A;

            packDataByte[5] = bytTempValue;

            packDataByte[6] = 0x2A;

            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();
            for (int i = 0; i < 5; i++)
            {
                packDataByte[7 + i] = dateByte[i];
            }

            intErrCode = AddNetData("12", "32", ByteArrayToHexString(packDataByte, 12));

            return intErrCode;
        }

        /// <summary>
        /// 保存门控状态
        /// </summary>
        /// <param name="doorStatus">门控状态 0：关闭 1：打开 2：故障</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int UpdateDoorStatus(string doorStatus)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_Door(doorStatus);

            return OperPubEquStatus(bytStatus, "30");
        }

        /// <summary>
        /// 保存刷卡器状态信息
        /// </summary>
        /// <param name="status">刷卡器状态 1：故障 2：正常</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int UpdateCardStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_Card(status);

            return OperPubEquStatus(bytStatus, "44");
        }

        /// <summary>
        /// 保存纸币器状态信息
        /// </summary>
        /// <param name="status">纸币器状态</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int UpdateCashStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_Cash(status);
            
            return OperPubEquStatus(bytStatus, "41");
        }

        /// <summary>
        /// 保存硬币器状态信息
        /// </summary>
        /// <param name="status">硬币器状态</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int UpdateCoinStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_Coin(status);

            return OperPubEquStatus(bytStatus, "37");
        }

        /// <summary>
        /// 保存驱动板状态信息
        /// </summary>
        /// <param name="status">驱动板状态</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int UpdateMainBoardStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_MainBoard(status);

            return OperPubEquStatus(bytStatus, "46");
        }

        /// <summary>
        /// 保存掉货检测状态信息
        /// </summary>
        /// <param name="status">掉货检测状态</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int UpdateDropCheckStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_DropCheck(status);

            return OperPubEquStatus(bytStatus, "47");
        }

        /// <summary>
        /// 保存升降系统状态信息
        /// </summary>
        /// <param name="status">升降系统状态</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int UpdateUpDownStatus(string status)
        {
            // 提交发送
            byte bytStatus = ConvertStatus_UpDown(status);

            return OperPubEquStatus(bytStatus, "49");
        }

        /// <summary>
        /// 保存货道状态信息
        /// </summary>
        /// <param name="status">货道状态 0：正常 1：异常</param>
        /// <param name="paNum">货道号</param>
        /// <returns>结果 False：成功 True：失败</returns>
        public int UpdatePaStatus(string status, string paNum)
        {
            int intErrCode = SUC;

            // 检测数据有效性
            if ((string.IsNullOrEmpty(paNum)) ||
                (string.IsNullOrEmpty(status)))
            {
                return ERR_DATAINVALID;
            }

            // 提交发送

            // 货道
            byte[] paNumByte = Encoding.UTF8.GetBytes(paNum);

            // 货道状态
            byte bytPaStatus = ConvertStatus_Asile(status);

            byte[] packDataByte = new byte[paNumByte.Length + 1 + 5 + 3];

            packDataByte[0] = 0x2A;

            for (int i = 0; i < paNumByte.Length; i++)
            {
                packDataByte[1 + i] = paNumByte[i];
            }

            packDataByte[1 + paNumByte.Length] = 0x2A;

            packDataByte[2 + paNumByte.Length] = bytPaStatus;

            packDataByte[3 + paNumByte.Length] = 0x2A;

            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();
            for (int i = 0; i < 5; i++)
            {
                packDataByte[4 + paNumByte.Length + i] = dateByte[i];
            }

            intErrCode = AddNetData("12", "36", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #region 根据各硬件设备状态码转换为状态字节

        /// <summary>
        /// 根据刷卡器状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_Card(string status)
        {
            byte bytStatus;

            switch (status)
            {
                ////case "00":// 无
                ////case "0":
                ////    bytStatus = 0x00;
                ////    break;
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;

                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据纸币器状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_Cash(string status)
        {
            byte bytStatus;

            switch (status)
            {
                ////case "00":// 无
                ////case "0":
                ////    bytStatus = 0x00;
                ////    break;
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;

                case "03":// 电机故障
                case "3":
                    bytStatus = 0x03;
                    break;
                case "04":// 感应器故障
                case "4":
                    bytStatus = 0x04;
                    break;
                case "05":// 纸币通道卡塞
                case "5":
                    bytStatus = 0x05;
                    break;
                case "06":// ROM校验和错误
                case "6":
                    bytStatus = 0x06;
                    break;
                case "07":// 纸币卡塞在接收通道
                case "7":
                    bytStatus = 0x07;
                    break;
                case "09":// 一个纸币非正常移除
                case "9":
                    bytStatus = 0x09;
                    break;
                case "0A":// 钞箱被拿走
                    bytStatus = 0x0A;
                    break;
                case "FE":// 钞箱已满
                    bytStatus = 0xFE;
                    break;
                case "FF":// 和纸币器断开连接
                    bytStatus = 0xFF;
                    break;
                default:// 故障
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据硬币状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_Coin(string status)
        {
            byte bytStatus;

            switch (status)
            {
                ////case "00":// 无
                ////case "0":
                ////    bytStatus = 0x00;
                ////    break;
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;
                case "05":// 识别硬币面值失败
                case "5":
                    bytStatus = 0x05;
                    break;
                case "06":// 检测到币桶感应器异常
                case "6":
                    bytStatus = 0x06;
                    break;
                case "07":// 两个硬币一起被接受
                case "7":
                    bytStatus = 0x07;
                    break;
                case "08":// 找不到硬币识别头
                case "8":
                    bytStatus = 0x08;
                    break;
                case "09":// 一个储币管卡塞
                case "9":
                    bytStatus = 0x09;
                    break;
                case "0A":// ROM校验和错误
                    bytStatus = 0x0A;
                    break;
                case "0B":// 硬币接收路径错误
                    bytStatus = 0x0B;
                    break;
                case "0E":// 接收通道有硬币卡塞
                    bytStatus = 0x0E;
                    break;
                case "FB":// 硬币盒被取走
                    bytStatus = 0xFB;
                    break;
                case "FC":// 可找零
                    bytStatus = 0xFC;
                    break;
                case "FD":// 零钱不足
                    bytStatus = 0xFD;
                    break;
                case "FF":// 和硬币器断开连接
                    bytStatus = 0xFF;
                    break;
                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据驱动板状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_MainBoard(string status)
        {
            byte bytStatus;

            switch (status)
            {
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;

                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据掉货检测状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_DropCheck(string status)
        {
            byte bytStatus;

            switch (status)
            {
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;
                case "03":// 被遮挡
                case "3":
                    bytStatus = 0x03;
                    break;
                case "FF":// 无连接
                    bytStatus = 0xFF;
                    break;
                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据升降机状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_UpDown(string status)
        {
            byte bytStatus;

            switch (status)
            {
                ////case "00":// 不存在
                ////case "0":
                ////    bytStatus = 0x00;
                ////    break;
                case "02":// 正常
                case "2":
                    bytStatus = 0x02;
                    break;
                case "03":// 升降机位置不在初始位
                case "3":
                    bytStatus = 0x03;
                    break;
                case "04":// 纵向电机可能卡塞
                case "4":
                    bytStatus = 0x04;
                    break;
                case "05":// 接货台不在初始位
                case "5":
                    bytStatus = 0x05;
                    break;
                case "06":// 横向电机卡塞
                case "6":
                    bytStatus = 0x06;
                    break;
                case "07":// 小门电机可能卡塞
                case "7":
                    bytStatus = 0x07;
                    break;
                case "08":// 接货台有货
                case "8":
                    bytStatus = 0x08;
                    break;
                case "09":// 接货电机可能卡塞
                case "9":
                    bytStatus = 0x09;
                    break;
                case "10":// 取货口有货
                    bytStatus = 0x10;
                    break;
                case "11":// 光电管故障
                    bytStatus = 0x11;
                    break;
                case "FF":// 无连接
                    bytStatus = 0xFF;
                    break;
                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据门控状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_Door(string status)
        {
            byte bytStatus;

            switch (status)
            {
                case "00":// 门关
                case "0":
                    bytStatus = 0x01;
                    break;

                case "01":// 门开
                case "1":
                    bytStatus = 0x02;
                    break;

                default:// 异常
                    bytStatus = 0x01;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据温度传感器状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_TmpDevice(string status)
        {
            byte bytStatus;

            switch (status)
            {
                case "00":
                case "0":
                    bytStatus = 0x02;

                    break;

                case "01"://
                case "1":
                    bytStatus = 0x01;
                    break;

                default:
                    bytStatus = 0x03;
                    break;
            }

            return bytStatus;
        }

        /// <summary>
        /// 根据货道状态码转换为状态字节
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private byte ConvertStatus_Asile(string status)
        {
            byte bytPaStatus;
            switch (status)
            {
                case "02":// 正常
                case "2":
                    bytPaStatus = 0x30;
                    break;
                case "03":// 线路不通
                case "3":
                    bytPaStatus = 0x32;
                    break;
                case "04":// 未安装电机
                case "4":
                    bytPaStatus = 0x31;
                    break;
                case "05":// 电机在4秒时限内不能压下微动开关到达相应位置
                case "5":
                    bytPaStatus = 0x34;
                    break;
                case "06":// 电机在4秒时限内不能归位到达相应位置，无掉货检测时可扣费
                case "6":
                    bytPaStatus = 0x35;
                    break;
                case "07":// 驱动IC出错
                case "7":
                    bytPaStatus = 0x36;
                    break;
                case "08":// 电机不在正确位置（电机不在位）
                case "8":
                    bytPaStatus = 0x37;
                    break;
                case "09":// 电机卡塞
                case "9":
                    bytPaStatus = 0x38;
                    break;
                case "0E":// 电机电流超限（电机过流）
                    bytPaStatus = 0x3D;
                    break;
                default:// 故障
                    bytPaStatus = 0x01;
                    break;
            }

            return bytPaStatus;
        }

        #endregion

        #endregion

        #region 货币汇报接口函数

        /// <summary>
        /// 货币汇报
        /// </summary>
        /// <param name="busId">交易号</param>
        /// <param name="moneyType">货币类型 0：硬币 1：纸币</param>
        /// <param name="operType">操作类型 0：收币 1：找零 2：吞币</param>
        /// <param name="amount">货币值</param>
        /// <param name="num">数量</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int OperMoney(string busId, string moneyType, string operType, int amount, int num)
        {
            int intErrCode = SUC; 

            #region 检查数据有效性

            if ((string.IsNullOrEmpty(busId)) ||
                (busId.Length > 9))
            {
                return ERR_DATAINVALID;
            }

            if ((moneyType != "0") &&
                (moneyType != "1"))
            {
                return ERR_DATAINVALID;
            }

            if ((operType != "0") &&
                (operType != "1") &&
                (operType != "2"))
            {
                return ERR_DATAINVALID;
            }

            if ((num < 0) ||
                (amount < 0))
            {
                return ERR_DATAINVALID;
            }

            #endregion

            // 交易号/收支/币值/数量/时间

            // 14 7A 1B 38 
            // 2A 
            // 31 39 38 38 
            // 2A 
            // 31 
            // 2A 
            // 31 30 30 
            // 2A 
            // 31 
            // 2A 
            // 0D 0B 20 02 1C 
            // 0A EF 0F 00 

            // 交易号
            byte[] bytBusId = Encoding.UTF8.GetBytes(busId);

            // 收支类型
            byte bytOperType;
            switch (operType)
            {
                case "0":// 收
                    bytOperType = 0x30;
                    break;

                case "1":// 找
                    bytOperType = 0x31;
                    break;

                case "2":// 吞
                    bytOperType = 0x32;
                    break;

                default:
                    bytOperType = 0x32;
                    break;
            }

            // 币值
            byte[] amountByte = Encoding.UTF8.GetBytes(amount.ToString());

            // 数量
            byte[] bytNum = Encoding.UTF8.GetBytes(num.ToString());

            // 时间
            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();

            // 组织数据包
            byte[] packDataByte = new byte[bytBusId.Length + amountByte.Length
                + dateByte.Length + 2 + 5];

            // 交易号/收支/币值/数量/时间

            packDataByte[0] = 0x2A;

            for (int i = 0; i < bytBusId.Length; i++)
            {
                packDataByte[1 + i] = bytBusId[i];
            }

            packDataByte[1 + bytBusId.Length] = 0x2A;

            packDataByte[2 + bytBusId.Length] = bytOperType;

            packDataByte[3 + bytBusId.Length] = 0x2A;

            for (int i = 0; i < amountByte.Length; i++)
            {
                packDataByte[4 + bytBusId.Length + i] = amountByte[i];
            }

            packDataByte[4 + bytBusId.Length + amountByte.Length] = 0x2A;

            packDataByte[5 + bytBusId.Length + amountByte.Length] = bytNum[0];

            packDataByte[6 + bytBusId.Length + amountByte.Length] = 0x2A;

            for (int i = 0; i < 5; i++)
            {
                packDataByte[7 + bytBusId.Length + amountByte.Length + i] = dateByte[i];
            }

            string strCmdType = "";
            switch (moneyType)
            {
                case "0":// 硬币
                    strCmdType = "38";
                    break;

                case "1":// 纸币
                    strCmdType = "42";
                    break;
            }

            intErrCode = AddNetData("14", strCmdType, ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode ;
        }

        #endregion

        #region 出货汇报接口函数

        /// <summary>
        /// 出货汇报
        /// </summary>
        /// <param name="busId">交易号</param>
        /// <param name="paNum">货道编码</param>
        /// <param name="sellPrice">销售价格</param>
        /// <param name="mcdCode">商品编码</param>
        /// <param name="num">销售数量</param>
        /// <param name="dropResult">掉货判断结果</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int SellGoods(string busId, string paNum, int sellPrice, string mcdCode, int num,string dropResult)
        {
            // 交易号/货道/价格/编码/数量/时间
            // 14 EE 24 36 
            // 2A 
            // 38 36 30 交易号
            // 2A 
            // 41 35 货道
            // 2A 
            // 32 35 30 价格
            // 2A 
            // 30 30 30 30 30 30 30 30 商品编码
            // 2A 
            // 31 数量
            // 2A 
            // 33 掉货判定结果
            // 2A
            // 0D 0B 20 0C 1E 时间
            // 0A 2F 0F 00 

            int intErrCode = SUC;

            #region 检查数据有效性

            if ((string.IsNullOrEmpty(busId)) ||
                (busId.Length > 9) ||
                (string.IsNullOrEmpty(paNum)))
            {
                return ERR_DATAINVALID;
            }

            if ((sellPrice < 0) ||
                (num < 0))
            {
                return ERR_DATAINVALID;
            }

            #endregion

            // 交易号
            byte[] bytBusId = Encoding.UTF8.GetBytes(busId);

            // 货道
            byte[] paNumByte = Encoding.UTF8.GetBytes(paNum);

            // 金额
            byte[] amountByte = Encoding.UTF8.GetBytes(sellPrice.ToString());

            // 商品编码
            if (string.IsNullOrEmpty(mcdCode))
            {
                mcdCode = "00000000";
            }
            byte[] mcdCodeByte = Encoding.UTF8.GetBytes(mcdCode);

            // 数量
            byte[] bytNum = Encoding.UTF8.GetBytes(num.ToString());

            // 掉货判定结果
            byte[] bytDrop = Encoding.UTF8.GetBytes(dropResult);

            // 时间
            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();

            // 组织数据包
            byte[] packDataByte = new byte[bytBusId.Length + paNumByte.Length +
                amountByte.Length + mcdCodeByte.Length +
                bytNum.Length + dateByte.Length + bytDrop.Length + 7];

            // 交易号/货道/价格/编码/数量/时间
            packDataByte[0] = 0x2A;

            // 交易号转换
            for (int i = 0; i < bytBusId.Length; i++)
            {
                packDataByte[1 + i] = bytBusId[i];
            }

            packDataByte[1 + bytBusId.Length] = 0x2A;

            // 货道转换
            for (int i = 0; i < paNumByte.Length; i++)
            {
                packDataByte[2 + bytBusId.Length + i] = paNumByte[i];
            }

            packDataByte[2 + bytBusId.Length + paNumByte.Length] = 0x2A;

            // 金额转换
            for (int i = 0; i < amountByte.Length; i++)
            {
                packDataByte[3 + bytBusId.Length + paNumByte.Length + i] = amountByte[i];
            }

            packDataByte[3 + bytBusId.Length + paNumByte.Length + amountByte.Length] = 0x2A;

            // 商品编码转换
            for (int i = 0; i < mcdCodeByte.Length; i++)
            {
                packDataByte[4 + bytBusId.Length + paNumByte.Length + amountByte.Length + i] = mcdCodeByte[i];
            }

            packDataByte[4 + bytBusId.Length + paNumByte.Length + amountByte.Length + mcdCodeByte.Length] = 0x2A;

            // 数量转换
            for (int i = 0; i < bytNum.Length; i++)
            {
                packDataByte[5 + bytBusId.Length + paNumByte.Length +
                    amountByte.Length + mcdCodeByte.Length + i] = bytNum[i];
            }
            packDataByte[5 + bytBusId.Length + paNumByte.Length + amountByte.Length +
                mcdCodeByte.Length + bytNum.Length] = 0x2A;

            // 掉货判定转换
            for (int i = 0; i < bytDrop.Length; i++)
            {
                packDataByte[6 + bytBusId.Length + paNumByte.Length +
                    amountByte.Length + mcdCodeByte.Length + bytNum.Length + i] = bytDrop[i];
            }
            packDataByte[6 + bytBusId.Length + paNumByte.Length + amountByte.Length +
                mcdCodeByte.Length + bytNum.Length + bytDrop.Length] = 0x2A;

            // 时间转换
            for (int i = 0; i < 5; i++)
            {
                packDataByte[7 + bytBusId.Length + paNumByte.Length +
                    amountByte.Length + mcdCodeByte.Length + bytNum.Length + bytDrop.Length + i] = dateByte[i];
            }

            intErrCode = AddNetData("14", "3A", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode ;
        }

        #endregion

        #region 刷卡汇报接口函数

        /// <summary>
        /// POS刷卡
        /// </summary>
        /// <param name="busId">交易号</param>
        /// <param name="cardType">卡类型</param>
        /// <param name="cardNum">逻辑卡号</param>
        /// <param name="phyNo">物理卡号</param>
        /// <param name="posMoney">刷卡金额</param>
        /// <param name="banFee">扣款后余额</param>
        /// <param name="cardSerNo">刷卡流水号</param>
        /// <param name="cardResult">刷卡结果</param>
        /// <param name="errCode">结果代码</param>
        /// <returns>结果 True：成功 False：失败</returns>
        public int PosCard(string busId, string cardType, string cardNum,string phyNo,
            int posMoney, int banFee,
            string cardSerNo, string cardResult, string errCode)
        {
            // 流水号/卡别/卡号/金额/卡流水号/结果
            // 14 18 2D 49 
            // 2A 
            // 38 37 32 交易号
            // 2A 
            // 32 卡类型
            // 2A 
            // 31 30 30 30 30 30 30 30 36 31 卡号
            // 2A 
            // 32 35 30 2B 33 36 30 30 刷卡金额+扣款后余额
            // 2A 
            // 32 35 刷卡流水号
            // 2A 
            // 30 // 结果
            // 2A 
            // 0D 0B 20 0E 3B 时间
            // 0A 3F 0F 00 

            int intErrCode = SUC;

            #region 检查数据有效性

            if (string.IsNullOrEmpty(cardType))
            {
                cardType = "6";
            }
            if (string.IsNullOrEmpty(cardNum))
            {
                cardNum = "0";
                cardNum = cardNum.PadLeft(10, '0');
            }

            if ((string.IsNullOrEmpty(busId)) ||
                (string.IsNullOrEmpty(cardType)) ||
                (string.IsNullOrEmpty(cardNum)))
            {
                return ERR_DATAINVALID;
            }

            if ((posMoney <= 0) ||
                (banFee < 0))
            {
                return ERR_DATAINVALID;
            }

            if (string.IsNullOrEmpty(phyNo))
            {
                phyNo = "0";
                phyNo = phyNo.PadLeft(10, '0');
            }
            if (string.IsNullOrEmpty(cardSerNo))
            {
                cardSerNo = "0";
            }

            #endregion

            // 交易号
            byte[] bytBusId = Encoding.UTF8.GetBytes(busId);

            // 卡类型
            byte[] cardTypeByte = Encoding.UTF8.GetBytes(cardType);

            // 逻辑卡号/物理卡号
            byte[] cardNumByte = Encoding.UTF8.GetBytes(cardNum + "+" + phyNo);

            // 刷卡金额/扣款金额
            byte[] posMoneyByte = Encoding.UTF8.GetBytes(posMoney.ToString() + "+" + banFee.ToString());

            // 刷卡流水号
            string strCardSerNo = cardSerNo;
            if (string.IsNullOrEmpty(strCardSerNo))
            {
                strCardSerNo = "1";
            }
            byte[] cardSerNoByte = Encoding.UTF8.GetBytes(strCardSerNo);

            // 结果
            byte[] cardResultByte = Encoding.UTF8.GetBytes(cardResult);

            // 时间
            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();

            // 组织数据包
            byte[] packDataByte = new byte[bytBusId.Length + cardTypeByte.Length +
                cardNumByte.Length + 
                posMoneyByte.Length + cardSerNoByte.Length +
                cardResultByte.Length + dateByte.Length + 7];

            // 流水号/卡别/卡号/金额/卡流水号/结果
            packDataByte[0] = 0x2A;

            // 交易号转换
            for (int i = 0; i < bytBusId.Length; i++)
            {
                packDataByte[1 + i] = bytBusId[i];
            }

            packDataByte[1 + bytBusId.Length] = 0x2A;

            // 卡别转换
            for (int i = 0; i < cardTypeByte.Length; i++)
            {
                packDataByte[2 + bytBusId.Length + i] = cardTypeByte[i];
            }

            packDataByte[2 + bytBusId.Length + cardTypeByte.Length] = 0x2A;

            // 卡号转换
            for (int i = 0; i < cardNumByte.Length; i++)
            {
                packDataByte[3 + bytBusId.Length + cardTypeByte.Length + i] = cardNumByte[i];
            } 

            packDataByte[3 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length] = 0x2A;

            // 金额转换
            for (int i = 0; i < posMoneyByte.Length; i++)
            {
                packDataByte[4 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length + i] = posMoneyByte[i];
            }

            packDataByte[4 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length + posMoneyByte.Length] = 0x2A;

            // 卡流水号转换
            for (int i = 0; i < cardSerNoByte.Length; i++)
            {
                packDataByte[5 + bytBusId.Length + cardTypeByte.Length +
                    cardNumByte.Length + posMoneyByte.Length + i] = cardSerNoByte[i];
            }
            packDataByte[5 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length +
                posMoneyByte.Length + cardSerNoByte.Length] = 0x2A;

            // 结果转换
            for (int i = 0; i < cardResultByte.Length; i++)
            {
                packDataByte[6 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length +
                    posMoneyByte.Length + cardSerNoByte.Length + i] = cardResultByte[i];
            }
            packDataByte[6 + bytBusId.Length + cardTypeByte.Length + cardNumByte.Length +
                    posMoneyByte.Length + cardSerNoByte.Length + cardResultByte.Length] = 0x2A;

            // 时间转换
            for (int i = 0; i < 5; i++)
            {
                packDataByte[7 + bytBusId.Length + cardTypeByte.Length +
                    cardNumByte.Length + posMoneyByte.Length +
                    cardSerNoByte.Length + cardResultByte.Length + i] = dateByte[i];
            }

            intErrCode = AddNetData("14", "49", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 武汉通刷卡—成功
        /// </summary>
        /// <param name="busId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public int PosCard_WH_Suc(string busId,string data)
        {
            int intErrCode = SUC;

            #region 检查数据有效性

            if ((string.IsNullOrEmpty(busId)) ||
                (string.IsNullOrEmpty(data)))
            {
                return ERR_DATAINVALID;
            }

            #endregion

            byte[] packDataByte = Encoding.UTF8.GetBytes("*" + busId + "*" + data);
            intErrCode = AddNetData("14", "44", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #endregion

        #region 条形码扫描在线支付接口函数（支付宝付款码、微信刷卡） 2015-01-16

        /// <summary>
        /// 条形码扫描在线支付结果
        /// </summary>
        /// <param name="busId">交易号</param>
        /// <param name="payType">支付类型</param>
        /// <param name="payNum">支付账号</param>
        /// <param name="payCode">条形码</param>
        /// <param name="money">扣款金额</param>
        /// <param name="payResult">支付结果 0：成功 其它：失败</param>
        /// <param name="asileCode">货道号</param>
        /// <param name="sellResult">出货结果 0：成功 其它：失败</param>
        /// <returns></returns>
        public int BarCode_Pay_Result(string busId, string payType, string payNum, string payCode,
            int money,string payResult,string asileCode,string sellResult)
        {
            // 时间（yymmddhhmmss）|交易号|支付类型|支付账号|扣款金额|条形码|扣款操作结果 0 为成功 ，其他为失败|货道号|出货结果。 0 为成功 ，其他为失败
            int intErrCode = SUC;

            string strBodyData = "|" + busId + "|" + payType + "|" + payNum + "|" +
                money + "|" + payCode + "|" + payResult + "|" + asileCode + "|" + sellResult;
            byte[] bytBodyData = Encoding.UTF8.GetBytes(strBodyData);
            int intBodyLen = bytBodyData.Length;

            // 时间
            byte[] dateByte = new byte[6];
            dateByte = HexDateTime_6();

            byte[] packDataByte = new byte[intBodyLen + 8];
            packDataByte[0] = 0x14;
            packDataByte[1] = 0x52;

            for (int i = 0; i < 6; i++)
            {
                packDataByte[2 + i] = dateByte[i];
            }

            for (int i = 0; i < intBodyLen; i++)
            {
                packDataByte[8 + i] = bytBodyData[i];
            }

            intErrCode = AddNetData("21", "01", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 条形码扫描在线冲正
        /// </summary>
        /// <returns></returns>
        public int BarCode_Pay_Return(string busId,string payCode,int money)
        {
            int intErrCode = SUC;

            // *交易号*付款码*金额*时间
            string strBodyData = "*" + busId + "*" + payCode + "*" + money + "*";
            byte[] bytBodyData = Encoding.UTF8.GetBytes(strBodyData);
            int intBodyLen = bytBodyData.Length;

            // 时间
            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();

            byte[] packDataByte = new byte[intBodyLen + 5];
            for (int i = 0; i < intBodyLen; i++)
            {
                packDataByte[i] = bytBodyData[i];
            }

            // 时间转换
            for (int i = 0; i < 5; i++)
            {
                packDataByte[intBodyLen + i] = dateByte[i];
            }

            intErrCode = AddNetData("17", "33", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        /// <summary>
        /// 微信取货码结果提交
        /// </summary>
        /// <param name="busId"></param>
        /// <param name="payCode"></param>
        /// <param name="money"></param>
        /// <param name="payType"></param>
        /// <returns></returns>
        public int WxTakeCode_Pay(string busId,string payCode,int money,string payType)
        {
            int intErrCode = SUC;

            string strBodyData = "*" + busId + "|" + payCode + "|" + money + "|" + payType ;
            byte[] bytBodyData = Encoding.UTF8.GetBytes(strBodyData);
            int intBodyLen = bytBodyData.Length;

            // 时间
            byte[] dateByte = new byte[6];
            dateByte = HexDateTime_6();

            byte[] packDataByte = new byte[intBodyLen + 8];
            packDataByte[0] = 0x14;
            packDataByte[1] = 0x50;

            for (int i = 0; i < 6; i++)
            {
                packDataByte[2 + i] = dateByte[i];
            }

            for (int i = 0; i < intBodyLen; i++)
            {
                packDataByte[8 + i] = bytBodyData[i];
            }

            intErrCode = AddNetData("21", "01", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #endregion

        #region 二维码正扫支付结果反馈 2016-12-02

        /// <summary>
        /// 二维码正扫支付结果接收反馈
        /// </summary>
        /// <param name="busId">交易号</param>
        /// <param name="payType">支付类型</param>
        /// <param name="payNum">支付账号</param>
        /// <param name="payCode">条形码</param>
        /// <param name="money">扣款金额</param>
        /// <param name="payResult">支付结果 0：成功 其它：失败</param>
        /// <param name="asileCode">货道号</param>
        /// <param name="sellResult">出货结果 0：成功 其它：失败</param>
        /// <returns></returns>
        /// BB 01 0C 10 30 2A 30 31 0A 9F DF 00
        public int QRCode_Pay_Reply(string busId, string payType, string payNum, string payCode,
            int money, string payResult, string asileCode, string sellResult)
        {
            // 时间（yymmddhhmmss）|交易号|支付类型|支付账号|扣款金额|条形码|扣款操作结果 0 为成功 ，其他为失败|货道号|出货结果。 0 为成功 ，其他为失败
            int intErrCode = SUC;

            string strBodyData = "|" + busId + "|" + payType + "|" + payNum + "|" +
                money + "|" + payCode + "|" + payResult + "|" + asileCode + "|" + sellResult;
            byte[] bytBodyData = Encoding.UTF8.GetBytes(strBodyData);
            int intBodyLen = bytBodyData.Length;

            // 时间
            byte[] dateByte = new byte[6];
            dateByte = HexDateTime_6();

            byte[] packDataByte = new byte[intBodyLen + 8];
            packDataByte[0] = 0x14;
            packDataByte[1] = 0x52;

            for (int i = 0; i < 6; i++)
            {
                packDataByte[2 + i] = dateByte[i];
            }

            for (int i = 0; i < intBodyLen; i++)
            {
                packDataByte[8 + i] = bytBodyData[i];
            }

            intErrCode = AddNetData("21", "01", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #endregion

        #region 机器维护接口函数

        /// <summary>
        /// 更新货道价格、弹簧圈数、库存
        /// </summary>
        /// <param name="operType">操作类型 5：价格  6：弹簧圈数 7：补货 8：条码</param>
        /// <param name="areaType">范围类型 1：单货道 2：单层 3：整机</param>
        /// <param name="code">设定对象 货道编号、层号、ZZ</param>
        /// <param name="value">设定值</param>
        /// <param name="userCode">管理员逻辑卡号</param>
        /// <returns></returns>
        public int SetAsileInfo(string operType, string areaType,string code, string value, string userCode)
        {
            int intErrCode = SUC;

            #region 检查数据有效性

            if ((operType != "5") && (operType != "6") && (operType != "7") && (operType != "8"))
            {
                return ERR_DATAINVALID;
            }
            if ((areaType != "1") && (areaType != "2") && (areaType != "3"))
            {
                return ERR_DATAINVALID;
            }

            if ((string.IsNullOrEmpty(code)) || (string.IsNullOrEmpty(value)))
            {
                return ERR_DATAINVALID;
            }

            if (string.IsNullOrEmpty(userCode))
            {
                userCode = "0";
            }

            #endregion

            // 类型(5价格6弹簧7补货8条码10补零钱18批结算等)|操作类型(1单货道2单层3整机)|设定对象(货道编码、层号+、ZZ)|设定值|管理员逻辑卡号
           
            // 设定对象
            switch (areaType)
            {
                case "2":// 单层
                    code = code + "+";
                    break;
                case "3":// 整机
                    code = "ZZ";
                    break;
            }

            ////byte[] dateByte = new byte[5];

            // 组织数据包
            byte[] bodyByte = Encoding.UTF8.GetBytes("*" + operType + "*" + areaType + 
                "*" + code + "*" + value + "*" + userCode +
                "*" + DateTime.Now.ToString("yyyyMMddHHmmss"));

            byte[] packDataByte = new byte[bodyByte.Length];

            for (int i = 0; i < bodyByte.Length; i++)
            {
                packDataByte[i] = bodyByte[i];
            }

            // 时间转换
            ////dateByte = HexDateTime();
            ////for (int i = 0; i < 5; i++)
            ////{
            ////    packDataByte[bodyByte.Length + i] = dateByte[i];
            ////}

            intErrCode = AddNetData("11", "39", ByteArrayToHexString(packDataByte, packDataByte.Length));

            return intErrCode;
        }

        #endregion

        #endregion

        #region 网络数据处理操作

        /// <summary>
        /// 保存要发送的网络数据
        /// </summary>
        /// <param name="cmdType">指令类型</param>
        /// <param name="operType">操作类型</param>
        /// <param name="netInfo">要发送的网络包体数据</param>
        /// <param name="time">包体中的时间</param>
        /// <returns>保存结果 True：成功 False：失败</returns>
        private int AddNetData(string cmdType, string operType, string netInfo)
        {
            string strSeqId = "1";

            int intErrCode = SUC;

            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);

                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                #region 获取本次通讯流水号

                cmd.CommandText = "select MaxSeqId from T_NET_CFG limit 1";

                DataSet db = new DataSet();

                SQLiteDataAdapter fd = new SQLiteDataAdapter();
                fd.SelectCommand = cmd;
                fd.Fill(db);

                if (db.Tables.Count > 0)
                {
                    if (db.Tables[0].Rows.Count == 1)
                    {
                        strSeqId = db.Tables[0].Rows[0]["MaxSeqId"].ToString();
                    }
                }

                if (Convert.ToInt32(strSeqId) >= 255)
                {
                    strSeqId = "1";
                }

                #endregion

                #region 保存通信数据

                cmd.CommandText = "insert into T_NET_INFO(Id,CmdType,SeqId,NetInfo,Kind,OperType,SendNum,Time) values(@Id,@CmdType,@SeqId,@NetInfo,@Kind,@OperType,@SendNum,@Time);";

                cmd.Parameters.Add(new SQLiteParameter("Id", null));

                // 指令类型
                cmd.Parameters.Add(new SQLiteParameter("CmdType", cmdType));

                // 交易流水号
                cmd.Parameters.Add(new SQLiteParameter("SeqId", strSeqId));

                // 包体数据
                cmd.Parameters.Add(new SQLiteParameter("NetInfo", netInfo));

                // 是否已发送
                cmd.Parameters.Add(new SQLiteParameter("Kind", "0"));

                // 操作类型
                cmd.Parameters.Add(new SQLiteParameter("OperType", operType));

                // 发送次数
                cmd.Parameters.Add(new SQLiteParameter("SendNum", "0"));

                // 时间
                cmd.Parameters.Add(new SQLiteParameter("Time", DateTime.Now.ToString()));

                int i = cmd.ExecuteNonQuery();

                #endregion

                #region 更新通信流水号

                cmd.CommandText = "update T_NET_CFG set MaxSeqId = @MaxSeqId;";
                cmd.Parameters.Add(new SQLiteParameter("MaxSeqId", (Convert.ToInt32(strSeqId) + 1)));
                i = cmd.ExecuteNonQuery();

                #endregion

                conn.Close();

                return intErrCode;
            }
            catch(Exception ex)
            {
                // 如果保存数据库失败，则保存到日志文件里
                string strErrMsg = ex.Message;
                try
                {
                    // 14  3A  2A 31 37 2A 32 31 30 2A 31 30 30 2A 30 30 30 30 30 30 30 30 2A 31 2A 30 2A 11 0C 0A 14 1B 
                    File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory.ToString() + "log\\NetDataErr.log",
                        DateTime.Now + ": " + cmdType + "  " + operType + "  " + netInfo + "\r\n" +
                    "ErrMsg:" + strErrMsg + "\r\n",
                            Encoding.Default);
                }
                catch
                {
                }
                return ERR_DBFAIL;
            }
        }

        /// <summary>
        /// 获取待发数据总数量
        /// </summary>
        /// <returns>总数量</returns>
        public int GetNetDataCount()
        {
            int intDataCount = 0;

            DataSet db = new DataSet();

            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);
                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = @"select count(1) datacount from T_NET_INFO ";

                SQLiteDataAdapter fd = new SQLiteDataAdapter();
                fd.SelectCommand = cmd;
                fd.Fill(db);
                cmd.Dispose();
                conn.Close();

                if (db.Tables.Count > 0)
                {
                    if (db.Tables[0].Rows.Count > 0)
                    {
                        intDataCount = Convert.ToInt32(db.Tables[0].Rows[0]["datacount"].ToString());
                    }
                }
            }
            catch
            {
                intDataCount = 0;
            }

            return intDataCount;
        }

        /// <summary>
        /// 获取要发送的网络数据
        /// </summary>
        /// <returns></returns>
        public DataSet QueryNetData()
        {
            DataSet db = new DataSet();

            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);
                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = @"select Id,SeqId,NetInfo,CmdType,OperType from T_NET_INFO 
                    where Kind='0' and SendNum <= " + m_SendDataMaxNum + " limit 10";

                SQLiteDataAdapter fd = new SQLiteDataAdapter();
                fd.SelectCommand = cmd;
                fd.Fill(db);
                cmd.Dispose();
                conn.Close();
            }
            catch
            {

            }
            return db;
        }

        /// <summary>
        /// 删除已经发送成功的网络数据
        /// </summary>
        /// <param name="Id">发送序号</param>
        /// <returns>删除结果 True：成功 Flase：失败</returns>
        public int DeleteNetData(int Id)
        {
            int intErrCode = SUC;
            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);
                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = "delete from T_NET_INFO where Id = @Id;";

                cmd.Parameters.Add(new SQLiteParameter("Id", Id));

                int i = cmd.ExecuteNonQuery();

                conn.Close();

                return intErrCode;
            }
            catch
            {
                return ERR_DBFAIL;
            }
        }

        /// <summary>
        /// 保存发送失败的网络数据
        /// </summary>
        /// <param name="Id">序号</param>
        /// <returns></returns>
        public int UpdateNetData(int Id)
        {
            int intErrCode = SUC;
            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);

                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = "update T_NET_INFO set SendNum = SendNum + 1 where Id = @Id;";

                cmd.Parameters.Add(new SQLiteParameter("Id", Id));

                int i = cmd.ExecuteNonQuery();

                conn.Close();

                return intErrCode;
            }
            catch
            {
                return ERR_DBFAIL;
            }
        }

        /// <summary>
        /// 恢复发送失败次数超限的网络数据
        /// </summary>
        /// <returns></returns>
        public int ResetNetData()
        {
            int intErrCode = SUC;
            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);

                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = "update T_NET_INFO set SendNum = 0 where Kind='0' and SendNum > " + m_SendDataMaxNum + "";

                int i = cmd.ExecuteNonQuery();

                conn.Close();

                return intErrCode;
            }
            catch
            {
                return ERR_DBFAIL;
            }
        }

        /// <summary>
        /// 获取通信流水号
        /// </summary>
        /// <returns></returns>
        public string GetSeqId()
        {
            string strSeqId = "0";

            try
            {
                DataSet db = new DataSet();

                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);

                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = "select MaxSeqId from T_NET_CFG limit 1";

                SQLiteDataAdapter fd = new SQLiteDataAdapter();
                fd.SelectCommand = cmd;
                fd.Fill(db);
                cmd.Dispose();

                if (db.Tables.Count > 0)
                {
                    if (db.Tables[0].Rows.Count == 1)
                    {
                        strSeqId = db.Tables[0].Rows[0]["MaxSeqId"].ToString();
                    }
                }

                conn.Close();
            }
            catch
            {

            }

            return strSeqId;
        }

        /// <summary>
        /// 清除所有待发数据
        /// </summary>
        /// <returns></returns>
        public int ClearNetData()
        {
            int intErrCode = SUC;
            try
            {
                SQLiteConnection conn = new SQLiteConnection("Data Source=" + m_DbFilePath);
                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();

                cmd.CommandText = "delete from T_NET_INFO;";

                int i = cmd.ExecuteNonQuery();

                conn.Close();

                return intErrCode;
            }
            catch
            {
                return ERR_DBFAIL;
            }
        }

        #endregion

        #region 私有函数

        /// <summary>
        /// 保存相关硬件设备状态信息
        /// </summary>
        /// <param name="status">硬件设备状态</param>
        /// <param name="equType">硬件类型</param>
        /// <returns>结果 True：成功 False：失败</returns>
        private int OperPubEquStatus(byte status, string equType)
        {
            int intErrCode = SUC;

            // 提交发送
            byte[] packDataByte = new byte[8];

            packDataByte[0] = 0x2A;

            packDataByte[1] = status;

            packDataByte[2] = 0x2A;

            byte[] dateByte = new byte[5];
            dateByte = HexDateTime();
            for (int i = 0; i < 5; i++)
            {
                packDataByte[3 + i] = dateByte[i];
            }

            intErrCode = AddNetData("12", equType, ByteArrayToHexString(packDataByte, 8));

            return intErrCode;
        }

        /// <summary>
        /// 封装要发送的网络数据
        /// </summary>
        /// <param name="cmdType">命令类型</param>
        /// <param name="operType">操作类型</param>
        /// <param name="netInfo">包体内容</param>
        /// <returns></returns>
        public byte[] PackNetDataToSend(string cmdType, string operType,string seqId,string netInfo)
        {
            byte[] SendData = null;
            byte[] forwardMessage = null;

            int intNetInfoLength = 0;

            if (cmdType == "FF")
            {
                // 心跳包，只有三位长度
                SendData = new byte[3];
            }
            else
            {
                // 包体数据
                forwardMessage = HexStringToByteArray(netInfo); //Encoding.UTF8.GetBytes(netInfo); //// 

                intNetInfoLength = forwardMessage.Length;// StringLength(netInfo);

                SendData = new byte[8 + intNetInfoLength];
            }

            try
            {
                // 数据包类型
                SendData[0] = Convert.ToByte(cmdType, 16);

                // 通讯流水号
                SendData[1] = Convert.ToByte(seqId);

                if (cmdType == "FF")
                {
                    SendData[2] = 0x00;
                }
                else
                {
                    // 数据包长度
                    SendData[2] = Convert.ToByte((8 + intNetInfoLength).ToString());

                    // 数据包子类型
                    SendData[3] = Convert.ToByte(operType, 16);

                    for (int i = 0; i < forwardMessage.Length; i++)
                    {
                        SendData[4 + i] = forwardMessage[i];
                    }
                    // 包体结束符
                    SendData[4 + intNetInfoLength] = 0x0A;

                    // 检验和
                    byte bytCheck = SendData[0];
                    for (int i = 1; i < 5 + intNetInfoLength; i++)
                    {
                        bytCheck = (byte)(bytCheck + SendData[i]);
                    }

                    SendData[5 + intNetInfoLength] = (byte)((byte)(bytCheck & 0xF0) + 0x0F);
                    SendData[6 + intNetInfoLength] = (byte)((byte)(bytCheck << 4) + 0x0F);

                    // 结束符
                    SendData[7 + intNetInfoLength] = 0x00;
                }
            }
            catch (Exception ex)
            {
            }
            return SendData;
        }

        /// <summary>
        /// 把字节数组转为十六进制字符串
        /// </summary>
        /// <param name="data">byte(字节型) 字节数组</param>
        /// <param name="length">要转换的长度</param>
        /// <returns>string(字符型)，转换后的十六进制字符串</returns>
        private string ByteArrayToHexString(byte[] data, int length)
        {
            try
            {
                StringBuilder sb = new StringBuilder(length * 3);
                for (int i = 0; i < length; i++)
                {
                    sb.Append(Convert.ToString(data[i], 16).PadLeft(2, '0').PadRight(3, ' '));
                }
                return sb.ToString().ToUpper();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 把16进制字符串转成16进制字节数组
        /// </summary>
        /// <param name="hexString">要转换的16进制字符串</param>
        /// <returns>转换后的16进制字节数组</returns>
        private byte[] HexStringToByteArray(string hexString)
        {
            byte[] hexByte = null;

            string strLastHex = hexString.Substring(hexString.Length - 1, 1);
            if (strLastHex == " ")
            {
                hexString = hexString.Substring(0, hexString.Length - 1);
            }

            string[] hexStringValue = hexString.Split(' ');
            int hexStringLen = hexStringValue.Length;

            string hexTemp = "";
            if (hexStringLen > 3)
            {
                hexByte = new byte[hexStringLen];
                for (int i = 0; i < hexStringLen; i++)
                {
                    hexTemp = hexStringValue[i];
                    if (!string.IsNullOrEmpty(hexTemp))
                    {
                        hexByte[i] = (byte)Convert.ToInt32(hexTemp, 16);
                    }
                }
            }

            return hexByte;
        }

        /// <summary>
        /// 把当前日期转换成字节数组
        /// </summary>
        /// <returns>转换后的字节数组</returns>
        private byte[] HexDateTime()
        {
            byte[] bytDateTime = new byte[5];
            string strDateTime = DateTime.Now.ToString("yyMMddHHmm");

            // 计算日期时间
            bytDateTime[0] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(0, 2)) + 1).ToString());
            bytDateTime[1] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(2, 2)) + 1).ToString());
            bytDateTime[2] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(4, 2)) + 1).ToString());
            bytDateTime[3] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(6, 2)) + 1).ToString());
            bytDateTime[4] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(8, 2)) + 1).ToString());

            return bytDateTime;
        }

        /// <summary>
        /// 把当前日期转换成字节数组
        /// </summary>
        /// <returns>转换后的字节数组</returns>
        private byte[] HexDateTime_6()
        {
            byte[] bytDateTime = new byte[6];
            string strDateTime = DateTime.Now.ToString("yyMMddHHmmss");

            // 计算日期时间
            bytDateTime[0] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(0, 2)) + 1).ToString());
            bytDateTime[1] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(2, 2)) + 1).ToString());
            bytDateTime[2] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(4, 2)) + 1).ToString());
            bytDateTime[3] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(6, 2)) + 1).ToString());
            bytDateTime[4] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(8, 2)) + 1).ToString());
            bytDateTime[5] = Convert.ToByte((Convert.ToInt32(strDateTime.Substring(10, 2)) + 1).ToString());

            return bytDateTime;
        }

        /// <summary>
        /// 对字符串进行MD5加密
        /// </summary>
        /// <param name="sourceStr">要加密的字符串</param>
        /// <returns>加密后的字符串</returns>
        private string Md5(string sourceStr)
        {
            string md5Str = "";

            if (sourceStr == null)
            {
                return "";
            }

            MD5 m = new MD5CryptoServiceProvider();

            byte[] s = m.ComputeHash(Encoding.UTF8.GetBytes(sourceStr));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符

                md5Str = md5Str + s[i].ToString("X2");

            }

            md5Str = md5Str.ToLower();

            return md5Str;
        }

        /// <summary>
        /// 获取字符串长度，中文长度为2
        /// </summary>
        /// <param name="s">输入的字符串</param>
        /// <returns>字符串长度</returns>
        private int StringLength(string s)
        {
            byte[] sarr = System.Text.Encoding.Default.GetBytes(s);
            int len = sarr.Length;

            return len;
        }

        /// <summary>
        /// 转换温度值
        /// </summary>
        /// <param name="tmpStatus"></param>
        /// <param name="tmpValue"></param>
        /// <returns></returns>
        private byte ConvertTmpValue(byte tmpStatus, string tmpValue)
        {
            byte bytTmpValue = 0x01;

            try
            {
                if (tmpStatus == 0x02)
                {
                    // 温度状态正常
                    if (!string.IsNullOrEmpty(tmpValue))
                    {
                        bytTmpValue = Convert.ToByte((Convert.ToInt32(tmpValue) + 1).ToString());
                    }
                }
            }
            catch
            {
                bytTmpValue = 0x01;
            }

            return bytTmpValue;
        }

        #endregion
    }
}
