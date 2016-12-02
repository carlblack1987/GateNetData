using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using GateNetData;

namespace GateNetData_Test
{
    public partial class Form1 : Form
    {
        private NetDataOper m_NetDataOper = new NetDataOper();

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int intErrCode = m_NetDataOper.UpdateCashStatus("02");
        }
    }
}
