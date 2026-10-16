namespace SOrderServer
{
    partial class Component
    {
        private System.ComponentModel.IContainer components = null;
        private Srvtools.InfoCommand cmdOrders;
        private Srvtools.UpdateComponent ucOrders;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.cmdOrders = new Srvtools.InfoCommand(this.components);
            this.ucOrders = new Srvtools.UpdateComponent(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.cmdOrders)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucOrders)).BeginInit();
            // 
            // cmdOrders
            // 
            this.cmdOrders.CommandText = "SELECT * FROM Orders";
            this.cmdOrders.EEPAlias = "ERPS";
            // 
            // ucOrders
            // 
            this.ucOrders.SelectCmd = this.cmdOrders;
            this.ucOrders.ServerModify = true;
            ((System.ComponentModel.ISupportInitialize)(this.cmdOrders)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucOrders)).EndInit();
        }

        #endregion
    }
}
