using System;
using System.Text;
using System.Threading.Tasks;
using Accord.MachineLearning.DecisionTrees;
using System.Data;
using System.IO;
using System.Data.OleDb;
using System.Globalization;
using Accord.Statistics.Filters;
using Accord.MachineLearning.DecisionTrees.Learning;
using Accord.Math;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace vk_sea_lib.DecisionTreeBuild
{
    public class DecisionTreeBuilder
    {

        public Expression<Func<double[], int>> expression;
        public Func<double[], int> func;
        public Codification codebook;

        public DecisionTree current_DT;
        private static DataTable training_dataset;
        private String pathToDataset;

        public DecisionTreeBuilder(String path)
        {
            this.pathToDataset = path;
        }
        public DecisionTreeBuilder(DataTable table)
        {
            DecisionTreeBuilder.training_dataset = table;
            DecisionTreeBuilder.training_dataset = CollectionExtensions.OrderRandomly(training_dataset.AsEnumerable()).CopyToDataTable();
        }

        public void studyDT()
        {
            generateCSV(DecisionTreeBuilder.training_dataset);

            // Create a new codification codebook to
            // convert strings into integer symbols
            this.codebook = new Codification(training_dataset);



            DecisionVariable[] attributes =
                {

                new DecisionVariable("on_web",                2),                                 // 2 possible values (0,1)  
                new DecisionVariable("likes_counter",         DecisionVariableKind.Continuous),   // counter_parameter
                new DecisionVariable("followed_by",           2),                                 // 2 possible values (Weak, strong)
                new DecisionVariable("following_matches",     DecisionVariableKind.Continuous)    // counter_parameter
            };

            int classCount = 2; // 2 possible output values: yes or no

            // Create a new instance of the C4.5 algorithm
            current_DT = new DecisionTree(attributes, classCount);
            C45Learning c45learning = new C45Learning(current_DT);

            // Translate our training data into integer symbols using our codebook:
            DataTable symbols = codebook.Apply(training_dataset);

            double[][] inputs = symbols.ToIntArray("on_web", "likes_counter", "followed_by", "following_matches").ToDouble();
            int[] outputs = symbols.ToIntArray("is_employee").GetColumn(0);

            // Learn the training instances!
            c45learning.Run(inputs, outputs);


            // Convert to an expression tree
            this.expression = current_DT.ToExpression();
            Console.WriteLine(GetDebugView(expression));

            // Compiles the expression to IL
            this.func = expression.Compile();


        }
        public void generateCSV(DataTable dt)
        {
            StringBuilder sb = new StringBuilder();

            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName);
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in dt.Rows)
            {
                IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                sb.AppendLine(string.Join(",", fields));
            }

            string destinationPath = @"C:/Users/Shindarev Nikita/Desktop/train_and_test.csv";
            File.WriteAllText(destinationPath, sb.ToString());
        }

        /// <summary>
        /// для получения debug_view
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        private static string GetDebugView(Expression exp)
        {
            if (exp == null)
                return null;

            var propertyInfo = typeof(Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic);
            return propertyInfo.GetValue(exp) as string;
        }

        static DataTable getDataTableFromCsv(string path, bool isFirstRowHeader)
        {
            string header = isFirstRowHeader ? "Yes" : "No";

            string pathOnly = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);

            string sql = @"SELECT * FROM [" + fileName + "]";

            using (OleDbConnection connection = new OleDbConnection(
                      @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + pathOnly +
                      ";Extended Properties=\"Text;HDR=" + header + "\""))
            using (OleDbCommand command = new OleDbCommand(sql, connection))
            using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
            {
                DataTable dataTable = new DataTable();
                dataTable.Locale = CultureInfo.CurrentCulture;
                adapter.Fill(dataTable);
                return dataTable;
            }
        }
    }
    public static class CollectionExtensions

    {

        private static Random random = new Random();

        public static IEnumerable<T> OrderRandomly<T>(this IEnumerable<T> collection)
 
        {

            // Order items randomly

            List<T> randomly = new List<T>(collection);

            while (randomly.Count > 0)

            {

                Int32 index = random.Next(randomly.Count);

                yield return randomly[index];

                randomly.RemoveAt(index);

            }

        } // OrderRandomly

    }
}
