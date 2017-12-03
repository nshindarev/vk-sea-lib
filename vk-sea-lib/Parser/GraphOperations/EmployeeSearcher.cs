using log4net;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vk_sea_lib.DecisionTreeBuild;
using vk_sea_lib.Resources;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;

namespace vk_sea_lib.Parser.GraphOperations
{
    class EmployeeSearcher
    {
        private static ILog logger = LogManager.GetLogger("EmployeeSearcher");
        /**
         *  окрашиваем вершины графа в цвета:
         *  ... "black": уже идентифицирован как сотрудник, уже проанализировали его друзей
         *  ... "grey":  уже идентифицирован как сотрудник, ещё не проанализировали его друзей
         *  ... "white": не идентифицирован как сотрудник компании, анализу не подвергается
         */
        private Dictionary<long, string> colored_vertices;
        private Dictionary<User, string> colored_affiliates;

        /**
         *  перечень указавших текущее место работы в данной компании
         *  стартовая точка для анализа + результаты предварительного анализа группы
         */
        private AdjacencyGraph<long, Edge<long>> coloredSocialGraph;
        private List<User> has_firm_name_employees;
        private DecisionTreeBuilder tree;
        private DataTable inputAffiliatesToTree;

        private List<Post> group_posts;
        private List<Photo> group_photos;

        private Dictionary<string, string> words_in_group;
        private Dictionary<long, int> likes_in_group;

        private string vk_company_page_id;
        private int limit;

        public Dictionary<long, string> getAllBlackStatusedEmp
        {
            get{
                return this.colored_vertices;
            }
        }

        public EmployeeSearcher(List<User> has_firm_name_employees, DecisionTreeBuilder tree, 
                                DataTable inputAffiliatesToTree, List<Post> group_posts, List<Photo> group_photos, int limit)
        {
           
            colored_vertices = new Dictionary<long, string>();

            this.has_firm_name_employees = has_firm_name_employees;
            this.tree = tree;


            this.limit = limit;
            this.inputAffiliatesToTree = inputAffiliatesToTree;
            this.group_photos = group_photos;
            this.group_posts = group_posts;
        }
        
        public void initialize_searcher()
        {
            // инициализируем граф друзей для сотрудников
            this.coloredSocialGraph = new AdjacencyGraph<long, Edge<long>>();

            foreach (User emp in this.has_firm_name_employees)
            {
                colored_vertices.Add(emp.Id, "grey");
                coloredSocialGraph.AddVertex(emp.Id);

                collectAllFriends(emp.Id);
            }

            logger.Debug("SEARCH STARTED: NUMBER OF BLACK VERTICES =" + colored_vertices.Count());
            firstLevelSearcher();
        }

        /// <summary>
        /// рекурсивный метод, вызывается после инициализации
        /// first level = потому что ищем только друзей сотрудников
        /// </summary>
        private void firstLevelSearcher()
        {

            // на входе только черные и серые вершины. 
            // 1) собираем белые
            foreach (KeyValuePair<long, string> affiliate in colored_vertices)
            {
                if (affiliate.Value.Equals("grey"))
                {
                    collectAllFriends(affiliate.Key);
                }
            }
            foreach (KeyValuePair<long, string> affiliate in colored_vertices)
            {
                List<long> white_affiliates = new List<long>();
                if (affiliate.Value.Equals("white"))
                {
                    white_affiliates.Add(affiliate.Key);
                }

                classifyWhiteVertices(ref white_affiliates);

                /**
                 *  проверяем, что лимит еще не достигнут
                 */ 
                int black_counter = 0;
                foreach (string s in colored_vertices.Values)
                {
                    if (s.Equals("black")) black_counter++;
                }

                if (white_affiliates.Count() > 0 && black_counter<limit)
                {
                    foreach(long id in white_affiliates)
                    {
                        colored_vertices[id] = "grey";
                    }
                    foreach(KeyValuePair<long, string> color in colored_vertices)
                    {
                        long key = color.Key;
                        if (color.Value.Equals("white")) colored_vertices.Remove(color.Key);
                        else if (color.Value.Equals("grey"))
                        {
                            colored_vertices[color.Key] = "black";
                            logger.Debug("new employee: id=" + color.Key);
                        }
                    }
                    firstLevelSearcher();
                }
            }

        }
        private void classifyWhiteVertices(ref List<long> white_affiliates)
        {

            List<User> white_affiliates_objects = new List<User>();
            foreach (long id in white_affiliates)
            {
                User user = VkApiHolder.Api.Users.Get(id, ProfileFields.All);
                white_affiliates_objects.Add(user);
            }


            //clear and fill in affiliates to input in classifier
            this.inputAffiliatesToTree.Rows.Clear();

            foreach (User white_affiliate in white_affiliates_objects)
            {
                DataRow row = this.inputAffiliatesToTree.NewRow();

                row[0] = white_affiliate.Id;

                row[1] = 0;
                row[2] = 0;
                row[3] = 0;
                row[4] = 0;
                row[5] = 0;
                row[6] = 0;

                row[7] = white_affiliate.FirstName;
                row[8] = white_affiliate.LastName;

                inputAffiliatesToTree.Rows.Add(row);
            }

            analyzeBufferParams(white_affiliates_objects, group_posts, group_photos);

            DataTable symbols = tree.codebook.Apply(inputAffiliatesToTree);
            foreach (DataRow row in symbols.Rows)
            {
                double r1 = Convert.ToDouble(row[1]);
                double r3 = Convert.ToDouble(row[3]);
                double r4 = Convert.ToDouble(row[4]);
                double r5 = Convert.ToDouble(row[5]);

                int is_employee = this.tree.func(new double[] { r1, r3, r4, r5 });

                if (is_employee == 0)
                {
                    try
                    {
                        //Из-за того, что непонятно как сравнивать Users конвертируем их в список id, удаляем там лишние и сохраняем
                        Thread.Sleep(100);

                        white_affiliates.Remove((long)row[0]);
                    }
                    catch (TooManyRequestsException req_ex)
                    {
                        Thread.Sleep(100);
                    }

                }
            }
        }
        private void collectAllFriends(long curEmployee)
        {
            List<User> vertexFriends = new List<User>();

            try
            {
                Thread.Sleep(100);
                vertexFriends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                {
                    UserId = Convert.ToInt32(curEmployee),
                    Order = FriendsOrder.Hints,
                    Fields = (ProfileFields)(ProfileFields.Domain)

                }).ToList<User>();
            }
            catch (TooManyRequestsException ex)
            {
                Thread.Sleep(300);
                logger.Error("Too many requests exception");
            }

            foreach (User affiliate in vertexFriends)
            {
                /**
                 *  на данных итерациях не рассматриваем друга, если мы его уже находили как друга другого сотрудника
                 *  так как полностью связи между сотрудниками заполняются на более позднем этапе.
                 */ 
                if (!colored_vertices.ContainsKey(affiliate.Id))
                {
                    this.colored_vertices.Add(affiliate.Id, "white");
                }
            }
        }


        /**
         * 
         *  Заполняем DataTable для белых вершин (чтобы их классифицировать)
         * 
         * 
         */

        /**
         * анализируем параметры для буфера
         */

        private void analyzeBufferParams(List<User> affiliates, List<Post> group_posts, List<Photo> group_photos)
        {
            // заполняем onWeb параметр
            searchInGroupPosts(affiliates);

            // заполняем likesCounter параметр
            searchInGroupLikes(group_posts, group_photos);

            // анализируем топологию сети для буфера
            analyzeNetworkTopology(affiliates);
        }

        private void searchInGroupPosts(List<User> affiliates)
        {

            System.Net.ServicePointManager.Expect100Continue = false;
            // IDeclension declension = Morpher.Factory.Russian.Declension;

            string filterExpression;
            string sortOrder;

            foreach (User affiliate in affiliates)
            {
                bool match_found = false;

                List<string> surname_diclensions = makeSurnameValuesToSearch(affiliate.LastName);
                foreach (string surname_in_dimension in surname_diclensions)
                {
                    if (words_in_group.ContainsValue(surname_in_dimension)) match_found = true;
                }


                if (match_found)
                {
                    filterExpression = "vk_id = '" + affiliate.Id + "'";
                    sortOrder = "vk_id DESC";
                    DataRow[] users_found_surname = inputAffiliatesToTree.Select(filterExpression, sortOrder, DataViewRowState.Added);

                    foreach (DataRow row in users_found_surname)
                    {
                        row[1] = 1;
                    }

                }
            }
        }
        private void searchInGroupLikes(List<Post> group_posts, List<Photo> group_photos)
        {
            string filterExpression, sortOrder;

            foreach (KeyValuePair<long, int> likes_by_user in this.likes_in_group)
            {
                filterExpression = "vk_id = '" + likes_by_user.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = inputAffiliatesToTree.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[3] = likes_by_user.Value;
                    logger.Debug("liked " + row[3] + " posts by affiliate " + row[0]);

                }
            }
        }
        private void analyzeNetworkTopology(IList<User> affiliates)
        {
            Dictionary<User, List<User>> datasetfriends = new Dictionary<User, List<User>>();


            // собираем друзей пользователя
            foreach (User user in affiliates)
            {
                try
                {
                    Thread.Sleep(100);
                    var affiliate_friends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                    {
                        UserId = Convert.ToInt32(user.Id),
                        Order = FriendsOrder.Hints,
                        Fields = (ProfileFields)(ProfileFields.Domain)

                    }).ToList<User>();
                    datasetfriends.Add(user, affiliate_friends);
                }
                catch (TooManyRequestsException ex)
                {
                    Thread.Sleep(300);
                }
                catch (VkApiException e)
                {
                    Thread.Sleep(300);
                }

            }


            int totalCount;
            var followers = VkApiHolder.Api.Groups.GetMembers(out totalCount, new GroupsGetMembersParams
            {
                GroupId = this.vk_company_page_id
            }).ToList<User>();


            /**
             *  matchesFound --- 
             *      key -> User
             *      val -> список его друзей состоящих в группе
             */

            var matchesFound = searchFollowingMatches(followers, datasetfriends);

            string filterExpression, sortOrder;
            foreach (KeyValuePair<long, List<int>> user_to_update_id in matchesFound)
            {
                filterExpression = "vk_id = '" + user_to_update_id.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = inputAffiliatesToTree.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[5] = (user_to_update_id.Value.Count - 1);
                }
            }
        }

        private List<string> makeSurnameValuesToSearch(string surname)
        {
            surname = surname.ToLower();
            List<string> surname_declensions = new List<string>();

            try
            {
                Morpher.Russian.IDeclension declension = Morpher.Factory.Russian.Declension;

                surname_declensions.Add(declension.Parse(surname).Nominative);
                surname_declensions.Add(declension.Parse(surname).Genitive);
                surname_declensions.Add(declension.Parse(surname).Dative);
                surname_declensions.Add(declension.Parse(surname).Accusative);
                surname_declensions.Add(declension.Parse(surname).Instrumental);
                surname_declensions.Add(declension.Parse(surname).Prepositional);

                surname_declensions.Add(surname);
            }
            catch (Exception ex)
            {
                surname_declensions.Add(surname);
            }
            return surname_declensions;
        }
        /**
        *  ________________________________
        *  Методы анализа параметров буфера
        *  ________________________________
        *  
        */

        private static string[] GetWords(string input)
        {
            MatchCollection matches = Regex.Matches(input, @"\b[\w']*\b");

            var words = from m in matches.Cast<Match>()
                        where !string.IsNullOrEmpty(m.Value)
                        select TrimSuffix(m.Value);

            return words.ToArray();
        }
        private static string TrimSuffix(string word)
        {
            int apostropheLocation = word.IndexOf('\'');
            if (apostropheLocation != -1)
            {
                word = word.Substring(0, apostropheLocation);
            }

            return word;
        }

        private Dictionary<long, List<int>> searchFollowingMatches(List<int> group_followers_ids, Dictionary<long, List<int>> dataset_ids)
        {
            Dictionary<long, List<int>> rez = new Dictionary<long, List<int>>();

            group_followers_ids.Sort();
            foreach (KeyValuePair<long, List<int>> entry in dataset_ids)
            {
                entry.Value.Sort();

                rez.Add(entry.Key, GetSimilarID(entry.Value, group_followers_ids));
                logger.Debug("for affiliate" + entry.Key + " found " + entry.Value.Count());

                // TODO: CHECK STRING BELOW
                // GetSimilarID(entry.Value, group_followers_ids).ForEach(i => Console.Write("{0}\t", i));
            }
            return rez;
        }
        private Dictionary<long, List<int>> searchFollowingMatches(List<User> group_followers, Dictionary<User, List<User>> dataset)
        {
            List<int> followers_ids = new List<int>();
            Dictionary<long, List<int>> dataset_ids = new Dictionary<long, List<int>>();

            foreach (User user in group_followers)
            {
                followers_ids.Add((int)user.Id);
            }
            foreach (KeyValuePair<User, List<User>> entry in dataset)
            {
                List<int> _friends = new List<int>();
                foreach (User user in entry.Value)
                {
                    _friends.Add((int)user.Id);
                }
                try
                {
                    dataset_ids.Add(entry.Key.Id, _friends);
                }
                catch (Exception ex)
                {
                    logger.Error("exception occured during network topology analyze");
                    logger.Error(ex.Message);
                }
            }

            return searchFollowingMatches(followers_ids, dataset_ids);
        }
        private List<int> GetSimilarID(IEnumerable<int> list1, IEnumerable<int> list2)
        {
            return (from item in list1 from item2 in list2 where (item == item2) select item).ToList();
        }
    }
}
