namespace CBankClient
{
    partial class FormBank
    {
        private System.ComponentModel.IContainer components = null;
        private Srvtools.InfoDataSet idsOrders;
        private Srvtools.InfoBindingSource ibsOrders;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.idsOrders = new Srvtools.InfoDataSet(this.components);
            this.ibsOrders = new Srvtools.InfoBindingSource(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.idsOrders)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ibsOrders)).BeginInit();
            this.SuspendLayout();
            // 
            // idsOrders
            // 
            this.idsOrders.Active = false;
            this.idsOrders.PacketRecords = 100;
            this.idsOrders.RemoteName = "SOrderServer.cmdOrders";
            // 
            // ibsOrders
            // 
            this.ibsOrders.DataMember = "Orders";
            this.ibsOrders.DataSource = this.idsOrders;
            // 
            // FormBank
            // 
            this.ClientSize = new System.Drawing.Size(640, 420);
            this.Name = "FormBank";
            this.Text = "訊光 EEP 銀行訂單維護表單 (VS2005)";
            ((System.ComponentModel.ISupportInitialize)(this.idsOrders)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ibsOrders)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion
    }
}
