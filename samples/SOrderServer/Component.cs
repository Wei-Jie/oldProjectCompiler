using System;
using System.ComponentModel;
using System.Data;
using Srvtools;

namespace SOrderServer
{
    public partial class Component : DataModule
    {
        public Component()
        {
            InitializeComponent();
        }

        public Component(IContainer container)
        {
            if (container != null)
            {
                container.Add(this);
            }
            InitializeComponent();
        }

        /// <summary>
        /// 範例 Server 端商業邏輯方法 (供 Client 端透過 CLIUtils.CallMethod 呼叫)
        /// </summary>
        public object[] CheckOrderAmount(object[] objParam)
        {
            decimal amount = 0m;
            if (objParam != null && objParam.Length > 0 && objParam[0] != null)
            {
                decimal.TryParse(objParam[0].ToString(), out amount);
            }

            string msg = string.Format("訂單金額檢查完成: {0:N2}", amount);
            return new object[] { 0, msg };
        }
    }
}
