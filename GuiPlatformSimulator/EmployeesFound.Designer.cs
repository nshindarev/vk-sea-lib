namespace GuiPlatformSimulator
{
    partial class EmployeesFound
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.employeesListGui = new System.Windows.Forms.ListBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSearchNew = new System.Windows.Forms.Button();
            this.btnStartResearch = new System.Windows.Forms.Button();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // employeesListGui
            // 
            this.employeesListGui.FormattingEnabled = true;
            this.employeesListGui.ItemHeight = 16;
            this.employeesListGui.Location = new System.Drawing.Point(12, 43);
            this.employeesListGui.Name = "employeesListGui";
            this.employeesListGui.Size = new System.Drawing.Size(479, 404);
            this.employeesListGui.TabIndex = 0;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(549, 110);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(209, 45);
            this.btnAdd.TabIndex = 1;
            this.btnAdd.Text = "Add Employee";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(549, 174);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(209, 45);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "Delete Employee";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnSearchNew
            // 
            this.btnSearchNew.Location = new System.Drawing.Point(549, 240);
            this.btnSearchNew.Name = "btnSearchNew";
            this.btnSearchNew.Size = new System.Drawing.Size(209, 45);
            this.btnSearchNew.TabIndex = 3;
            this.btnSearchNew.Text = "Search At Point";
            this.btnSearchNew.UseVisualStyleBackColor = true;
            this.btnSearchNew.Click += new System.EventHandler(this.btnSearchNew_Click);
            // 
            // btnStartResearch
            // 
            this.btnStartResearch.Location = new System.Drawing.Point(549, 44);
            this.btnStartResearch.Name = "btnStartResearch";
            this.btnStartResearch.Size = new System.Drawing.Size(209, 45);
            this.btnStartResearch.TabIndex = 4;
            this.btnStartResearch.Text = "Start Research";
            this.btnStartResearch.UseVisualStyleBackColor = true;
            this.btnStartResearch.Click += new System.EventHandler(this.btnStartResearch_Click);
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(549, 303);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(209, 48);
            this.btnUpdate.TabIndex = 5;
            this.btnUpdate.Text = "Update";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            // 
            // EmployeesFound
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(842, 540);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.btnStartResearch);
            this.Controls.Add(this.btnSearchNew);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.employeesListGui);
            this.Name = "EmployeesFound";
            this.Text = "Employees List";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox employeesListGui;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnSearchNew;
        private System.Windows.Forms.Button btnStartResearch;
        private System.Windows.Forms.Button btnUpdate;
    }
}

