using System;
using System.Data;
using System.Windows.Forms;
using Srvtools;

namespace CBankClient
{
    public partial class FormBank : InfoForm
    {
        public FormBank()
        {
            InitializeComponent();
        }

        public void SubmitOrder(decimal amount)
        {
            object[] ret = CLIUtils.CallMethod("SOrderServer", "CheckOrderAmount", new object[] { amount });
            if (ret != null && ret.Length > 1 && Convert.ToInt32(ret[0]) == 0)
            {
                this.Text = string.Format("銀行訂單處理 - {0} (登入者: {1})", ret[1], CLIUtils.fLoginUser);
            }
        }
    }
}
