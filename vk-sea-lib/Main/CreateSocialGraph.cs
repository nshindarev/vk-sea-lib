using QuickGraph;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vk_sea_lib.DecisionTreeBuild;
using vk_sea_lib.Parser;
using vk_sea_lib.Resources;
using VkNet.Enums.Filters;
using VkNet.Model;

namespace vk_sea_lib.Main
{
    public class CreateSocialGraph
    {
        private string access_token;
        private string user_id;

        //обучающая выборка, функция и результирующий граф 
        public AdjacencyGraph<long, Edge<long>> empSocialGraph;

        private DataTable trainingDataset;
        private Func<double[], int> func;

        public EmployeesSearcher searcher;

        public CreateSocialGraph(string access_token, string user_id)
        {
            this.access_token = access_token;
            this.user_id = user_id;
        }

        public void createSocialGraph()
        {
            //собираем обучающую выборку
            //CollectingTrainingDataset collector = new CollectingTrainingDataset("Петер-Сервис", "57902527");

            CollectingTrainingDataset collector = new CollectingTrainingDataset("Кодельная", "116186911");
            collector.parseInformation();
            this.trainingDataset = collector.training_dataset;

            //обучаем классификатор
            DecisionTreeBuilder dt = new DecisionTreeBuilder(collector.training_dataset);
            dt.studyDT();

            //собираем оставшиеся страницы
            this.searcher = new EmployeesSearcher(dt, collector.companyName, collector.vkPageId);
            this.searcher.findAllEmployees();

            this.empSocialGraph = searcher.EmployeesSocialGraph;
            Console.WriteLine();
        }
        public List<long> searchEmpAtPoint(long newPointId, List<long> curEmp)
        {
            CollectingTrainingDataset collector = new CollectingTrainingDataset("Кодельная", "116186911");
            collector.parseInformation();
            this.trainingDataset = collector.training_dataset;

            //обучаем классификатор
            DecisionTreeBuilder dt = new DecisionTreeBuilder(collector.training_dataset);
            dt.studyDT();

            this.searcher = new EmployeesSearcher(dt, collector.companyName, collector.vkPageId);

            User newPointUser = VkApiHolder.Api.Users.Get(newPointId, ProfileFields.All);
           return this.searcher.collectFriendsEmployees(newPointUser, searcher.group_posts, searcher.group_photos, ref curEmp);
        }
    }
}
