using System;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;

namespace Srvtools
{
    /// <summary>
    /// 模擬訊光 EEP Client 端基礎表單類別 InfoForm
    /// </summary>
    public class InfoForm : Form
    {
        public string Active { get; set; }
        public InfoForm()
        {
            Active = "True";
        }
    }

    /// <summary>
    /// 模擬訊光 EEP Server 端基礎資料模組類別 DataModule / InfoDataModule
    /// </summary>
    public class DataModule : Component
    {
    }

    public class InfoDataModule : DataModule
    {
    }

    /// <summary>
    /// 模擬訊光 EEP InfoDataSet 元件
    /// </summary>
    [Serializable]
    public class InfoDataSet : Component, ISupportInitialize
    {
        public string RemoteName { get; set; }
        public bool Active { get; set; }
        public int PacketRecords { get; set; }
        public DataSet RealDataSet { get; set; }

        public InfoDataSet()
        {
            RemoteName = "";
            Active = false;
            PacketRecords = 100;
            RealDataSet = new DataSet();
        }

        public InfoDataSet(IContainer container) : this()
        {
            if (container != null) container.Add(this);
        }

        public void BeginInit() { }
        public void EndInit() { }
    }

    /// <summary>
    /// 模擬訊光 EEP InfoBindingSource 元件
    /// </summary>
    public class InfoBindingSource : BindingSource
    {
        public new string DataMember { get; set; }
        public new object DataSource { get; set; }

        public InfoBindingSource() { }
        public InfoBindingSource(IContainer container)
        {
            if (container != null) container.Add(this);
        }
    }

    /// <summary>
    /// 模擬訊光 EEP Server 端 InfoCommand 元件
    /// </summary>
    [Serializable]
    public class InfoCommand : Component, ISupportInitialize
    {
        public string CommandText { get; set; }
        public string EEPAlias { get; set; }
        public bool MultiSetWhere { get; set; }

        public InfoCommand()
        {
            CommandText = "";
            EEPAlias = "ERPS";
        }

        public InfoCommand(IContainer container) : this()
        {
            if (container != null) container.Add(this);
        }

        public void BeginInit() { }
        public void EndInit() { }
    }

    /// <summary>
    /// 模擬訊光 EEP Server 端 UpdateComponent 元件
    /// </summary>
    [Serializable]
    public class UpdateComponent : Component, ISupportInitialize
    {
        public InfoCommand SelectCmd { get; set; }
        public bool ServerModify { get; set; }

        public UpdateComponent()
        {
            ServerModify = true;
        }

        public UpdateComponent(IContainer container) : this()
        {
            if (container != null) container.Add(this);
        }

        public void BeginInit() { }
        public void EndInit() { }
    }

    /// <summary>
    /// 模擬訊光 EEP Client 工具類別 CLIUtils
    /// </summary>
    public static class CLIUtils
    {
        public static string fLoginUser = "ADMIN";
        public static string fLoginDB = "ERPS";

        public static object[] CallMethod(string moduleName, string methodName, object[] parameters)
        {
            return new object[] { 0, "OK" };
        }

        public static DataSet ExecuteSql(string moduleName, string commandName, string sql, bool isClient, string project)
        {
            return new DataSet();
        }
    }
}
