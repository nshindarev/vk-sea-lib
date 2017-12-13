using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using vk_sea_lib.Main;
using vk_sea_lib.Authorize;

namespace GuiPlatformSimulator
{
    public partial class EmployeesFound : Form
    {
        private CreateSocialGraph creator;
        public EmployeesFound()
        {
            InitializeComponent();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            EmployeeInfoHandler idForm = new EmployeeInfoHandler();
            if (idForm.ShowDialog(this) == DialogResult.OK)
            {
                this.employeesListGui.Items.Add(idForm.txtBox.Text);
            }

            idForm.Close();
            idForm.Dispose();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (this.employeesListGui.SelectedIndex >= 0)
            {
                employeesListGui.Items.Remove(employeesListGui.SelectedItem);

            }
        }

        private void btnSearchNew_Click(object sender, EventArgs e)
        {
            EmployeeInfoHandler idForm = new EmployeeInfoHandler();
            if (idForm.ShowDialog(this) == DialogResult.OK)
            {
                /**
                 *  1) авторизуемся
                 *  2) если 
                 */ 
                try
                {
                    UserAuthorizer auth = new vk_sea_lib.Authorize.UserAuthorizer();
                    auth.authorize();

                    creator = new CreateSocialGraph(UserAuthorizer.access_token, UserAuthorizer.user_id.ToString());

                    List<long> foundEmp = new List<long>();
                    foreach (string item in employeesListGui.Items)
                    {
                        foundEmp.Add((long)Convert.ToInt64(item));
                    }

                    Int64 newId = Convert.ToInt64(idForm.txtBox.Text.ToString());
                    List<long> newEmpFound = creator.searchEmpAtPoint((long)newId, foundEmp);

                    foreach (long newItem in newEmpFound)
                    {
                        if (!this.employeesListGui.Items.Contains(newItem))
                        {
                            this.employeesListGui.Items.Add(newItem);
                        }
                    }

                    MessageBox.Show("found " + newEmpFound.Count() + " new employees");
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            idForm.Close();
            idForm.Dispose();
        }

        private void btnStartResearch_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                UserAuthorizer auth = new vk_sea_lib.Authorize.UserAuthorizer();
                auth.authorize();

                creator = new CreateSocialGraph(UserAuthorizer.access_token, UserAuthorizer.user_id.ToString());
                creator.createSocialGraph();

                foreach (KeyValuePair<long, string> black in creator.searcher.blackEmployeeStatusSetter.getAllBlackStatusedEmp.ToList())
                {
                    if (!this.employeesListGui.Items.Contains(black.Key))
                    {
                        this.employeesListGui.Items.Add(black.Key);
                    }
                };

            });
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (long id in this.creator.searcher.blackEmployeeStatusSetter.colored_vertices.Keys.ToList())
                {
                    this.employeesListGui.Items.Add(id);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
